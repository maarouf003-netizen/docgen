using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.ExecutionCircuits;

/// <summary>
/// إعادة تسمية دائرة تنفيذ — عملية **جماعية** داخل معاملة واحدة: تحديث اسم الدائرة،
/// وكل ملفاتها (الاسم/النص المعياري/البحث/النسخة + وقعة تسمية لكل ملف)، وإناباتها
/// (`DelegatedCourt`)، وترحيل نص التنبيهات المدمجة عبر `Outbox`، ثم تدقيق حقلي لكل ملف
/// وتدقيق جماعي. تحوّل سباق القيد الفريد إلى `DocumentConflictException` كما الأصل.
/// </summary>
public sealed record RenameExecutionCircuitCommand(
    int CircuitId,
    int BranchId,
    int? OwnerSectionId,
    string? ActorName,
    string Name,
    long? Version,
    int? ActorUserId = null) : ICommand<ExecutionCircuitDto>, ITransactionalRequest;

public sealed class RenameExecutionCircuitCommandHandler
    : CommandHandlerBase<RenameExecutionCircuitCommand, ExecutionCircuitDto>
{
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<Section> _sections;
    private readonly IDocumentRepository _documents;
    private readonly IDelegationRepository _delegations;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly IReferenceDataCache? _cache;

    public RenameExecutionCircuitCommandHandler(
        IRepository<ExecutionCircuit> circuits,
        IRepository<Branch> branches,
        IRepository<Section> sections,
        IDocumentRepository documents,
        IDelegationRepository delegations,
        IRepository<DocumentOccurrence> occurrences,
        IHeadAlertOutbox alertOutbox,
        IUnitOfWork uow,
        IAuditLogger audit,
        IDbExceptionClassifier dbErrors,
        IEnumerable<IPipelineBehavior<RenameExecutionCircuitCommand, ExecutionCircuitDto>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _circuits = circuits;
        _branches = branches;
        _sections = sections;
        _documents = documents;
        _delegations = delegations;
        _occurrences = occurrences;
        _alertOutbox = alertOutbox;
        _uow = uow;
        _audit = audit;
        _dbErrors = dbErrors;
        _cache = cache;
    }

    protected override async Task<ExecutionCircuitDto> HandleCoreAsync(
        RenameExecutionCircuitCommand command, CancellationToken ct)
    {
        ExecutionCircuitRules.RequireBranchWithGovernorate(
            await _branches.GetByIdAsync(command.BranchId, ct));
        var trimmed = ExecutionCircuitRules.NormalizeName(command.Name);
        var norm = ArabicNameNormalizer.Normalize(trimmed);

        try
        {
            var circuit = await _circuits.GetByIdAsync(command.CircuitId, ct)
                ?? throw new ArgumentException("الدائرة غير موجودة");
            ExecutionCircuitRules.EnsureOwned(circuit, command.BranchId, command.OwnerSectionId);
            if (command.Version is not null && circuit.Version != command.Version.Value)
                throw new DocumentConflictException("تعارض تزامن — أعد تحميل الدائرة وحاول مجددًا");
            // فحص ودّي قبل التسمية لبيان مالك الاسم المحجوز؛ القيد الفريد يبقى الحارس النهائي.
            var conflict = ExecutionCircuitRules.FindCircuitByName(
                await _circuits.ListAsync(ct), command.BranchId, norm, command.CircuitId);
            if (conflict is not null)
                throw new DocumentConflictException(
                    await ExecutionCircuitRules.DuplicateCircuitMessageAsync(
                        conflict, command.OwnerSectionId, _sections, ct));
            if (circuit.Name != trimmed)
            {
                var oldName = circuit.Name;
                circuit.Name = trimmed;
                circuit.NameNorm = norm;
                circuit.UpdatedAt = DateTime.UtcNow;
                circuit.Version++;

                // تحديث جماعي فوري لكل الملفات في نفس المعاملة + رفع Version يدويًا + إعادة بناء الثلاثية.
                var now = DateTime.UtcNow;
                var docs = await _documents.ListByCircuitAsync(command.CircuitId, includeDeleted: false, ct);
                var renamedAudit = new List<(Document Doc, Dictionary<string, string?> Before)>();
                foreach (var d in docs)
                {
                    // لقطة «قبل» للتدقيق الحقلي (S5) — قبل أي طفرة.
                    var before = DocumentChangeTracker.Capture(d);
                    d.Court = trimmed;
                    d.CourtNorm = string.IsNullOrWhiteSpace(trimmed) ? null : ArabicNameNormalizer.Normalize(trimmed);
                    d.SearchText = DocumentSearchTextBuilder.Build(d);
                    d.FullData = DocumentSearchTextBuilder.BuildFullData(d);
                    d.UpdatedAt = now;
                    d.Version++;
                    _documents.Update(d);

                    // وقوعّة التسمية على كل ملف متأثر (§5.2) — وإلا مرّت الجماعية بلا أثر ملفي.
                    await _occurrences.AddAsync(new DocumentOccurrence
                    {
                        DocumentId = d.Id,
                        OccurrenceType = OccurrenceTypeCatalog.CircuitRenamed,
                        Source = OccurrenceSourceCatalog.System,
                        EventDate = now,
                        FileNumber = d.FileNumber,
                        FileType = d.FileType,
                        Year = int.TryParse(d.FileYear, out var oy) ? oy : null,
                        FromCircuitName = oldName,
                        ToCircuitName = trimmed,
                        CreatedById = command.ActorUserId ?? circuit.CreatedById,
                        CreatedAt = now,
                        UpdatedAt = now,
                    }, ct);
                    renamedAudit.Add((d, before));
                }
                // إعادة التسمية تُحدث أيضًا DelegatedCourt/Norm في صفوف DocumentDelegation حيث DelegatedCircuitId = الدائرة.
                var delegations = await _delegations.ListByDelegatedCircuitAsync(command.CircuitId, ct);
                foreach (var g in delegations)
                {
                    g.DelegatedCourt = trimmed;
                    g.DelegatedCourtNorm = string.IsNullOrWhiteSpace(trimmed) ? null : ArabicNameNormalizer.Normalize(trimmed);
                    g.UpdatedAt = now;
                    _delegations.Update(g);
                }
                // ترحيل نص التنبيهات المدمجة عبر Outbox داخل المعاملة نفسها:
                // المفتاح الدلالي (الدائرة) هو ما يربط التنبيهات، فلا حاجة
                // للبحث النصي الهش في صفوف HeadAlerts.
                await _alertOutbox.EnqueueCircuitRenamedAsync(
                    command.CircuitId,
                    HeadAlertMessages.CircuitPendingDocuments(new(circuit.Name)),
                    now,
                    ct);
                _circuits.Update(circuit);
                await _uow.SaveChangesAsync(ct);
                _cache?.Invalidate(ReferenceDataGroups.Circuits);
                // تدقيق حقلي لكل ملف (S5) — داخل المعاملة بعد الحفظ الرئيسي.
                foreach (var (doc, before) in renamedAudit)
                    await ExecutionCircuitRules.LogDocumentChangeAsync(
                        _audit, doc, command.ActorName, "rename_circuit",
                        $"أعاد تسمية دائرة الملف ({doc.Id}) من «{oldName}» إلى «{trimmed}»", before, ct);
                await _audit.LogAsync(command.ActorName, "rename_circuit", null, null,
                    $"أعاد تسمية دائرة التنفيذ من «{oldName}» إلى «{trimmed}» مع تحديث {docs.Count} ملفًا و{delegations.Count} إنابة", ct);
            }
            return await ExecutionCircuitRules.BuildDtoAsync(circuit, _documents, _sections, ct);
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            // سباق حقيقي بين فحصين متزامنين: الرسالة العامة تحمل القيد الفرعي نفسه.
            throw new DocumentConflictException(
                $"الدائرة «{trimmed}» موجودة مسبقًا في فرعك — لا يمكن تكرار اسم الدائرة داخل الفرع", ex);
        }
    }
}
