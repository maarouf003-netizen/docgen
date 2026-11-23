using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>
/// نقل ملف إلى محامٍ آخر — يملك معاملته وتدقيقه (`transfer`) وسجل التعاقب ومزامنة
/// المناب وتنبيه المالك الجديد (`H2`) كما الأصل.
/// <para>
/// **حدود المعاملة:** كل التحققات داخل المعاملة نفسها عمدًا (ضد `TOCTOU`)، والنقل
/// الذرّي ومزامنة التنبيه داخل المعاملة نفسها — بلا وسم معاملة خارجي.
/// </para>
/// </summary>
public sealed record TransferDocumentCommand(
    int DocumentId,
    int TargetLawyerId,
    string? ActorName,
    int? ActorUserId = null) : ICommand<DocumentResponse>;

public sealed class TransferDocumentCommandHandler
    : CommandHandlerBase<TransferDocumentCommand, DocumentResponse>
{
    private readonly IUserRepository _users;
    private readonly IDocumentRepository _documents;
    private readonly IDocumentDelegationService _delegationService;
    private readonly IAuditLogger _audit;
    private readonly ITransactionRunner _tx;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IExecutionCircuitService? _executionCircuits;

    public TransferDocumentCommandHandler(
        IUserRepository users,
        IDocumentRepository documents,
        IDocumentDelegationService delegationService,
        IAuditLogger audit,
        ITransactionRunner tx,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<TransferDocumentCommand, DocumentResponse>> behaviors,
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
        _executionCircuits = executionCircuits;
    }

    protected override async Task<DocumentResponse> HandleCoreAsync(
        TransferDocumentCommand command, CancellationToken ct)
    {
        return await _tx.RunAsync(async token =>
        {
            // كل التحققات داخل المعاملة نفسها لتفادي أي TOCTOU: حالة الهدف (وجود/دور/تفعيل)
            // تُقرأ في نفس لقطة المعاملة التي يُنفَّذ فيها النقل الذرّي، فلو عُطّل المحامي
            // أو تغيّرت بياناته لحظياً أثناء النقل لن يُنقل الملف إليه.
            var target = await _users.GetByIdAsync(command.TargetLawyerId, token);
            if (target is null || target.Role != UserRole.Lawyer)
                throw new ArgumentException("المحامي المستهدف غير موجود");
            if (!target.IsActive)
                throw new ArgumentException("المحامي المستهدف غير مفعل");

            var doc = await _documents.GetByIdAsync(command.DocumentId, token);
            if (doc is null)
                throw new KeyNotFoundException();

            if (doc.CreatedById == target.Id)
                throw new ArgumentException("لا يمكن نقل الملف إلى المحامي المختص به حاليًا");
            if (target.BranchId != doc.BranchId)
                throw new ArgumentException("لا يمكن نقل الملف إلى محامٍ من فرع آخر");

            var sourceOwnerId = doc.CreatedById;
            var transferred = await _documents.TransferOwnerAsync(
                command.DocumentId, doc.CreatedById, target.Id, target.FullName,
                doc.Lawyer ?? doc.CreatedBy?.FullName ?? string.Empty, token);
            if (transferred is null)
                throw new DocumentConflictException("تغيّر المحامي المختص للملف أثناء النقل — أعد المحاولة");

            // سجل تعاقب: الملف أُحيل إلى المحامي المستهدف بتاريخ النقل.
            await _documents.AddAssignmentAsync(command.DocumentId, AssignmentKindCatalog.Transfer,
                target.FullName, command.ActorName, DateTime.UtcNow, token);

            // الملف المناب: يُزامَن المحامي المختص المخزّن على الإنابة مع المالك الجديد
            // في نفس المعاملة، فتعرض «تشعبات الملف» ونافذة الإتمام المتابع الحالي.
            await _delegationService.SyncTargetsTransferredAsync(
                new[] { command.DocumentId }, target.Id, command.ActorName, command.ActorUserId, token);

            await _audit.LogAsync(command.ActorName, "transfer", command.DocumentId, doc.DocumentType,
                DocumentText.AuditWithActor($"نقل الملف إلى المحامي: {target.FullName}", doc), token);

            // H2: التنبيه يتبع المالك — داخل معاملة النقل كي لا يبقى تنبيه قديم بلا مالك.
            if (_executionCircuits is not null && command.ActorUserId.HasValue)
                await _executionCircuits.SyncPendingAlertsAfterTransferAsync(
                    sourceOwnerId, target.Id, doc.BranchId ?? target.BranchId ?? 0, command.ActorUserId.Value, token);

            return DocumentResponse.FromEntity(transferred, CurrentYear());
        }, ct);
    }

    private int CurrentYear() => ServerClock.CurrentYear(_clock, _timeZone);
}
