using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>
/// نقل كل ملفات محامٍ إلى آخر — يملك مفتاح عدم التكرار (`RF-011`) ومعاملة النواة
/// (تحققات `TOCTOU`، النقل الجماعي الذرّي، سجل تعاقب وتدقيق لكل ملف، مزامنة المناب،
/// وتنبيه المالك الجديد `H2`).
/// <para>
/// **حدود المعاملة:** الحجز يسبق النواة، والنواة تفتح معاملة واحدة، واللقطة تُكتب بعد
/// النجاح والتحرير عند الفشل — بلا وسم `ITransactionalRequest` وبلا تداخل.
/// </para>
/// </summary>
public sealed record TransferAllDocumentsCommand(
    int SourceLawyerId,
    int TargetLawyerId,
    int? ScopeBranchId,
    string? ActorName,
    string? IdempotencyKey = null,
    int? OwnerSectionId = null,
    int? ActorUserId = null) : ICommand<int>;

public sealed class TransferAllDocumentsCommandHandler
    : CommandHandlerBase<TransferAllDocumentsCommand, int>
{
    private readonly IUserRepository _users;
    private readonly IDocumentRepository _documents;
    private readonly IDocumentDelegationService _delegationService;
    private readonly IAuditLogger _audit;
    private readonly ITransactionRunner _tx;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IIdempotencyStore? _idempotency;
    private readonly IExecutionCircuitService? _executionCircuits;

    public TransferAllDocumentsCommandHandler(
        IUserRepository users,
        IDocumentRepository documents,
        IDocumentDelegationService delegationService,
        IAuditLogger audit,
        ITransactionRunner tx,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<TransferAllDocumentsCommand, int>> behaviors,
        IIdempotencyStore? idempotency = null,
        IExecutionCircuitService? executionCircuits = null)
        : base(behaviors)
    {
        _users = users;
        _documents = documents;
        _delegationService = delegationService;
        _audit = audit;
        _tx = tx;
        _clock = clock;
        _timeZone = timeZone;
        _idempotency = idempotency;
        _executionCircuits = executionCircuits;
    }

    protected override async Task<int> HandleCoreAsync(
        TransferAllDocumentsCommand command, CancellationToken ct)
    {
        // RF-011: حجز المفتاح أولًا (لا تحقق مسبق يستحق `400` هنا — التحققات داخل
        // المعاملة عمدًا ضد `TOCTOU`) — النطاق محامي المصدر (النية = نقل ملفاته) —
        // التكرار يُعيد العدد المخزن نفسه.
        var ticket = await IdempotencyGuard.BeginAsync(_idempotency, "documents.transfer-all",
            command.SourceLawyerId, command.IdempotencyKey,
            IdempotencyGuard.Fingerprint(new
            {
                sourceLawyerId = command.SourceLawyerId,
                targetLawyerId = command.TargetLawyerId,
                scopeBranchId = command.ScopeBranchId,
                ownerSectionId = command.OwnerSectionId,
            }), ct);

        int transferred;
        try
        {
            transferred = await TransferAllCoreAsync(command, ct);
        }
        catch
        {
            await IdempotencyGuard.ReleaseAsync(_idempotency, ticket, ct);
            throw;
        }

        // اللقطة بشكل الاستجابة نفسه (`{"transferredCount":N}`) ليُعاد حرفيًا عند التكرار.
        await IdempotencyGuard.CompleteAsync(_idempotency, ticket,
            IdempotencyGuard.Snapshot(new { transferredCount = transferred }), ct: ct);
        return transferred;
    }

    /// <summary>نواة النقل الجماعي (تحققات + نقل + تدقيق) — تُستدعى بعد حجز مفتاح عدم التكرار.</summary>
    private async Task<int> TransferAllCoreAsync(
        TransferAllDocumentsCommand command, CancellationToken ct)
    {
        return await _tx.RunAsync(async token =>
        {
            // كل التحققات داخل المعاملة نفسها لتفادي أي TOCTOU: حالة المصدر والهدف
            // (وجود/دور/تفعيل/فرع) تُقرأ في نفس لقطة المعاملة التي يُنفَّذ فيها النقل،
            // ويُتحقق من تطابق عدد الملفات المتوقعة مع الصفوف المتأثرة فعلياً.
            var source = await _users.GetByIdAsync(command.SourceLawyerId, token);
            if (source is null || source.Role != UserRole.Lawyer)
                throw new ArgumentException("المحامي المطلوب نقل ملفاته غير موجود");
            if (source.Id == command.TargetLawyerId)
                throw new ArgumentException("لا يمكن نقل الملفات إلى المحامي صاحبها");
            if (command.ScopeBranchId.HasValue && source.BranchId != command.ScopeBranchId)
                throw new ArgumentException("لا يمكن نقل ملفات محامٍ من فرع آخر");

            var target = await _users.GetByIdAsync(command.TargetLawyerId, token);
            if (target is null || target.Role != UserRole.Lawyer)
                throw new ArgumentException("المحامي المستهدف غير موجود");
            if (!target.IsActive)
                throw new ArgumentException("المحامي المستهدف غير مفعل");
            if (source.BranchId != target.BranchId)
                throw new ArgumentException("لا يمكن نقل الملفات إلى محامٍ من فرع آخر");

            var files = await _documents.ListByOwnerInScopeAsync(
                command.SourceLawyerId, command.ScopeBranchId, command.OwnerSectionId, token);
            if (files.Count == 0)
                return 0;

            var moved = await _documents.TransferAllOwnerAsync(
                command.SourceLawyerId, target.Id, target.FullName, source.FullName,
                command.ScopeBranchId, command.OwnerSectionId, token);
            if (moved != files.Count)
                throw new DocumentConflictException("تغيّرت بيانات الملفات أثناء النقل الجماعي — أعد المحاولة");

            var now = ServerClock.Now(_clock, _timeZone);
            foreach (var file in files)
            {
                // سجل تعاقب لكل ملف أُحيل إلى المحامي المستهدف.
                await _documents.AddAssignmentAsync(file.Id, AssignmentKindCatalog.Transfer,
                    target.FullName, command.ActorName, DateTime.UtcNow, token);
                await _audit.LogAsync(command.ActorName, "transfer", file.Id, file.DocumentType,
                    $"تم إحالة هذا الملف إلى المحامي: {target.FullName} بتاريخ {now:d/M/yyyy} — المنفذ عليه: {DocumentText.ActorFullName(file)}", token);
            }

            // ملفات منابة ضمن الدفعة: يُزامَن المحامي المختص المخزّن على إناباتها
            // مع المالك الجديد (استعلام واحد + كتابة داخل نفس المعاملة).
            await _delegationService.SyncTargetsTransferredAsync(
                files.Select(f => f.Id).ToArray(), target.Id, command.ActorName, command.ActorUserId, token);

            // H2: التنبيه يتبع المالك — داخل معاملة النقل الجماعي كي لا يبقى تنبيه قديم بلا مالك.
            if (_executionCircuits is not null && command.ActorUserId.HasValue)
                await _executionCircuits.SyncPendingAlertsAfterTransferAsync(
                    command.SourceLawyerId, target.Id, target.BranchId ?? command.ScopeBranchId ?? 0, command.ActorUserId.Value, token);

            return moved;
        }, ct);
    }
}
