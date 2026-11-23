using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.ExecutionCircuits;

/// <summary>
/// حذف دائرة تنفيذ نهائيًا (قرار المالك 2026-10-17) — يملك المعاملة وتدقيق `delete_circuit`:
/// الترتيب **الحارس ← الوارد المعلقة ← فصل الوارد ← التطهير ← إزالة الدائرة ← الحفظ ← التدقيق**.
/// العلة التقنية: فصل الوارد قبل التطهير يمنع تعارض أثر التتبّع مع صفوف الإنابة المحذوفة؛
/// وسلة المحذوفات تُطهَّر فيزيائيًا (CASCADE للتوابع، SET NULL للتنبيهات/المرسلات/خطابات
/// المراجعة، والتدقيق بلا FK فيبقى تاريخًا كاملًا — إعلان صريح لا سهو).
/// </summary>
public sealed record DeleteExecutionCircuitCommand(
    int CircuitId,
    int BranchId,
    int? OwnerSectionId,
    string? ActorName) : ICommand<bool>, ITransactionalRequest;

public sealed class DeleteExecutionCircuitCommandHandler
    : CommandHandlerBase<DeleteExecutionCircuitCommand, bool>
{
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IRepository<Branch> _branches;
    private readonly IDocumentRepository _documents;
    private readonly IDelegationRepository _delegations;
    private readonly IHeadAlertRepository _alerts;
    private readonly IRepository<DocumentBaseNumber> _baseNumbers;
    private readonly IDocumentBaseNumberRepository? _baseNumberRepository;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly IReferenceDataCache? _cache;

    public DeleteExecutionCircuitCommandHandler(
        IRepository<ExecutionCircuit> circuits,
        IRepository<Branch> branches,
        IDocumentRepository documents,
        IDelegationRepository delegations,
        IHeadAlertRepository alerts,
        IRepository<DocumentBaseNumber> baseNumbers,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<DeleteExecutionCircuitCommand, bool>> behaviors,
        IDocumentBaseNumberRepository? baseNumberRepository = null,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _circuits = circuits;
        _branches = branches;
        _documents = documents;
        _delegations = delegations;
        _alerts = alerts;
        _baseNumbers = baseNumbers;
        _uow = uow;
        _audit = audit;
        _baseNumberRepository = baseNumberRepository;
        _cache = cache;
    }

    protected override async Task<bool> HandleCoreAsync(
        DeleteExecutionCircuitCommand command, CancellationToken ct)
    {
        ExecutionCircuitRules.RequireBranchWithGovernorate(
            await _branches.GetByIdAsync(command.BranchId, ct));

        var circuit = await _circuits.GetByIdAsync(command.CircuitId, ct)
            ?? throw new ArgumentException("الدائرة غير موجودة");
        ExecutionCircuitRules.EnsureOwned(circuit, command.BranchId, command.OwnerSectionId);
        // حارس الحذف: الأحياء فقط (تعديل البند 41) + الإنابات الواردة المعلقة.
        var counts = await _documents.CountByCircuitsAsync(
            new List<int> { command.CircuitId }, ct);
        counts.TryGetValue(command.CircuitId, out var v);
        if (v.FileCount > 0)
            throw new DocumentConflictException($"لا يمكن حذف الدائرة — أفرغها أولًا (بها {v.FileCount} ملفًا)");
        // C1: متجاوزًا فلتر المصدر المحذوف — المعلقة تحظر أيًا كان حال مصدرها.
        var pendingIncoming = await _delegations.CountPendingIncomingByCircuitIncludingDeletedAsync(
            command.CircuitId, ct);
        if (pendingIncoming > 0)
            throw new DocumentConflictException($"لا يمكن حذف الدائرة — بها {pendingIncoming} إنابة واردة معلقة تستهدفها");
        // التاريخ المجمد (البند 45): الإنابات غير المعلقة التي استهدفت الدائرة تُفك
        // عن المرجع (DelegatedCircuitId=null) مع إبقاء الاسم النصي — وإلا انفجر الحذف
        // بانتهاك FK (Restrict) رغم اجتياز الحارس. المعلقة محظورة أعلاه فلا تصل هنا.
        // C1: متجاوزًا الفلتر أيضًا — إنابة مصدرها محذوف ما زال صفها يشير للدائرة.
        // فصل الوارد قبل التطهير (علة الترتيب المذكورة أعلاه).
        var incoming = await _delegations.ListByDelegatedCircuitIncludingDeletedAsync(
            command.CircuitId, ct);
        var detached = 0;
        var now = DateTime.UtcNow;
        foreach (var g in incoming)
        {
            if (g.Status == DelegationStatusCatalog.PendingHead)
                throw new DocumentConflictException("لا يمكن حذف الدائرة — بها إنابة واردة معلقة تستهدفها");
            g.DelegatedCircuitId = null;
            g.UpdatedAt = now;
            _delegations.Update(g);
            detached++;
        }
        // تطهير سلة المحذوفات نهائيًا (قرار المالك — تعديل البند 41): حذف فيزيائي
        // ضمن معاملة الحذف نفسها؛ والتسلسل الموثوق به: CASCADE للتوابع (الوقوعات/
        // أرقام الأساس/الإسنادات/الأطراف/الكفلاء/الورثة/الأموال/الاستئنافات/إنابات
        // الملف المحذوف)، SET NULL للتنبيهات/المرسلات/خطابات المراجعة — والعلة
        // تسهل اختبارها: أي صف شاذ يفشل بصوت عالٍ داخل المعاملة مع rollback.
        var purged = 0;
        var deletedDocs = await _documents.ListDeletedByCircuitAsync(command.CircuitId, ct);
        var deletedDocIds = deletedDocs.Select(d => d.Id).ToList();
        // FK أرقام الأساس على المستند Restrict (V-18/E17): التطهير الفيزيائي للدائرة
        // يزيل صفوف أرقام الملفات المحذوفة صراحةً قبل حذف المستندات (وإلا انفجر الحذف).
        if (deletedDocIds.Count > 0)
        {
            if (_baseNumberRepository is not null)
            {
                foreach (var deletedDocId in deletedDocIds)
                {
                    var rows = await _baseNumberRepository.ListByDocumentAsync(
                        deletedDocId, includeDeletedDocument: true, ct);
                    foreach (var row in rows)
                        _baseNumberRepository.Remove(row);
                }
            }
            else
            {
                var idSet = deletedDocIds.ToHashSet();
                var baseRows = (await _baseNumbers.ListAsync(ct))
                    .Where(b => idSet.Contains(b.DocumentId))
                    .ToList();
                foreach (var row in baseRows)
                    _baseNumbers.Remove(row);
            }
        }
        var linkedAlerts = await _alerts.ListActiveByCircuitOrDocumentsAsync(
            command.CircuitId, deletedDocIds, ct);
        foreach (var alert in linkedAlerts)
        {
            alert.IsArchived = true;
            alert.ArchivedAt = now;
        }
        foreach (var d in deletedDocs)
        {
            _documents.Remove(d);
            purged++;
        }
        _circuits.Remove(circuit);
        await _uow.SaveChangesAsync(ct);
        _cache?.Invalidate(ReferenceDataGroups.Circuits);
        // عبارة التطهير تُلحق للتدقيق فقط عند purged > 0 (لا «مع» مكررة ولا «تطهير 0»).
        var purgeClause = purged > 0 ? $"، وطُهِّر نهائيًا {purged} ملفًا محذوفًا" : string.Empty;
        await _audit.LogAsync(command.ActorName, "delete_circuit", null, null,
            $"حذف دائرة التنفيذ «{circuit.Name}» مع فك {detached} إنابة مكتملة عن مرجعها (الاسم محفوظ){purgeClause}", ct);
        return true;
    }
}
