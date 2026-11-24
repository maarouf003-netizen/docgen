using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Delegations;

/// <summary>
/// توجيه إنابة خارجية معلّقة لشعبة في الفرع المناب (§7.3) — رئيس قسم الفرع
/// المناب فقط، والشعبة من فرعه ونشطة ولها رئيس مفعّل. يُستبدل تنبيه القسم
/// بتنبيه موجَّه لرئيس الشعبة، ويُعاد للقسم بالتراجع قبل الإسناد.
/// </summary>
public sealed record RedirectDocumentDelegationCommand(
    int DelegationId,
    RedirectDelegationRequest Request,
    int UserId,
    string? ActorName) : ICommand<DelegationDto?>;

internal sealed class RedirectDocumentDelegationCommandHandler
    : CommandHandlerBase<RedirectDocumentDelegationCommand, DelegationDto?>
{
    private readonly IDelegationRepository _delegations;
    private readonly IUserRepository _users;
    private readonly IRepository<Section> _sections;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public RedirectDocumentDelegationCommandHandler(
        IDelegationRepository delegations,
        IUserRepository users,
        IRepository<Section> sections,
        IHeadAlertOutbox alertOutbox,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IDbExceptionClassifier dbErrors,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<RedirectDocumentDelegationCommand, DelegationDto?>> behaviors)
        : base(behaviors)
    {
        _delegations = delegations;
        _users = users;
        _sections = sections;
        _alertOutbox = alertOutbox;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _dbErrors = dbErrors;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<DelegationDto?> HandleCoreAsync(
        RedirectDocumentDelegationCommand command, CancellationToken ct)
    {
        var delegationId = command.DelegationId;
        var request = command.Request;
        var userId = command.UserId;
        var actorName = command.ActorName;
        var delegation = await _delegations.GetByIdWithDetailsAsync(delegationId, ct)
            ?? throw new ArgumentException("الإنابة غير موجودة");
        if (delegation.Status != DelegationStatusCatalog.PendingHead)
            throw new ArgumentException("لا يمكن توجيه إنابة لم تعد معلّقة");
        if (delegation.AssignedLawyerId is not null)
            throw new ArgumentException("لا يمكن توجيه إنابة بعد إسنادها");
        if (delegation.RejectReason is not null)
            throw new ArgumentException("لا يمكن توجيه إنابة مرفوضة — بانتظار تصحيح المحامي");
        if (!delegation.IsExternal)
            throw new ArgumentException("التوجيه للشعبة للإنابات الخارجية فقط");
        if (delegation.RedirectedToSectionId is not null)
            throw new ArgumentException("الإنابة موجَّهة لشعبة سلفًا — تراجع عن التوجيه أولًا");
        if (request.Version.HasValue && delegation.Version != request.Version.Value)
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الإنابة وحاول مجددًا");

        var caller = await _users.GetByIdAsync(userId, ct)
            ?? throw new ArgumentException("المستخدم غير موجود");
        var approvalBranch = delegation.ExternalBranchId
            ?? throw new ArgumentException("الإنابة الخارجية بلا فرع مناب — تعذّر التوجيه");
        if (caller.Role != UserRole.Head || caller.BranchId != approvalBranch)
            throw new ArgumentException("التوجيه للشعبة لرئيس قسم الفرع المناب فقط");

        var section = await _sections.GetByIdAsync(request.SectionId, ct)
            ?? throw new ArgumentException("الشعبة الموجَّه لها غير موجودة");
        if (section.BranchId != approvalBranch)
            throw new ArgumentException("التوجيه لشعبة في الفرع المناب نفسه");
        if (!section.IsActive)
            throw new ArgumentException("الشعبة الموجَّه لها معطلة — اختر شعبة نشطة");
        var subHead = await _users.FindActiveHeadAsync(UserRole.SubHead, approvalBranch, section.Id, ct)
            ?? throw new ArgumentException("الشعبة بلا رئيس مفعّل — عيّن رئيسًا قبل التوجيه");

        try
        {
            await _tx.RunAsync(async token =>
            {
                delegation.RedirectedToSectionId = section.Id;
                delegation.UpdatedAt = DateTime.UtcNow;
                delegation.Version++;
                _delegations.Update(delegation);

                await _alertOutbox.EnqueueSystemAsync(
                    new CreateSystemHeadAlertRequest(
                        HeadAlertKindCatalog.DelegationSentToSubhead,
                        HeadAlertMessages.DelegationSentToSubhead(new(
                            delegation.Id, DocumentDelegationService.SourceLabel(delegation.SourceDocument))),
                        HeadAlertAudience.SingleHead,
                        approvalBranch,
                        userId,
                        DocumentId: delegation.SourceDocumentId,
                        DelegationId: delegation.Id,
                        RecipientUserId: subHead.Id),
                    new HeadAlertSupersedePlan(
                        delegation.Id,
                        AppealId: null,
                        HeadAlertKindCatalog.DelegationKinds,
                        "delegation_redirected"),
                    occurredAt: DateTime.UtcNow,
                    ct: token);

                await _uow.SaveChangesAsync(token);
                await _audit.LogAsync(actorName, "redirect_delegation",
                    delegation.SourceDocumentId, delegation.SourceDocument.DocumentType,
                    $"وجّه الإنابة (رقم {delegation.Id}) لشعبة {section.Name}", token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsConcurrencyViolation(ex))
        {
            // توجيهان متزامنان حقيقيان للإنابة نفسها — 409 ودية بدل 500 خام.
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الإنابة وحاول مجددًا", ex);
        }

        return await _delegations.GetByIdWithDetailsAsync(delegation.Id, ct) is { } reloaded
            ? DocumentDelegationService.ToDto(reloaded, reloaded.SourceDocument, ServerClock.CurrentYear(_clock, _timeZone))
            : null;
    }
}

/// <summary>
/// التراجع عن توجيه الشعبة قبل الإسناد (§7.3) — رئيس قسم فرع الاعتماد
/// (آمن للتعاقب: أي رئيس قسم حالي للفرع نفسه). يُستبدل تنبيه الشعبة بتنبيه القسم.
/// </summary>
public sealed record RecallDelegationRedirectCommand(
    int DelegationId,
    int UserId,
    string? ActorName) : ICommand<DelegationDto?>;

internal sealed class RecallDelegationRedirectCommandHandler
    : CommandHandlerBase<RecallDelegationRedirectCommand, DelegationDto?>
{
    private readonly IDelegationRepository _delegations;
    private readonly IUserRepository _users;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public RecallDelegationRedirectCommandHandler(
        IDelegationRepository delegations,
        IUserRepository users,
        IHeadAlertOutbox alertOutbox,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IDbExceptionClassifier dbErrors,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<RecallDelegationRedirectCommand, DelegationDto?>> behaviors)
        : base(behaviors)
    {
        _delegations = delegations;
        _users = users;
        _alertOutbox = alertOutbox;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _dbErrors = dbErrors;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<DelegationDto?> HandleCoreAsync(
        RecallDelegationRedirectCommand command, CancellationToken ct)
    {
        var delegationId = command.DelegationId;
        var userId = command.UserId;
        var actorName = command.ActorName;
        var delegation = await _delegations.GetByIdWithDetailsAsync(delegationId, ct)
            ?? throw new ArgumentException("الإنابة غير موجودة");
        if (delegation.Status != DelegationStatusCatalog.PendingHead)
            throw new ArgumentException("لا يمكن التراجع عن توجيه إنابة لم تعد معلّقة");
        if (delegation.AssignedLawyerId is not null)
            throw new ArgumentException("تعذّر التراجع بعد الإسناد");
        if (delegation.RedirectedToSectionId is null)
            throw new ArgumentException("لا يوجد توجيه مفتوح لهذه الإنابة");
        if (delegation.RejectReason is not null)
            throw new ArgumentException("لا يمكن التراجع عن توجيه إنابة مرفوضة — بانتظار تصحيح المحامي");

        var caller = await _users.GetByIdAsync(userId, ct)
            ?? throw new ArgumentException("المستخدم غير موجود");
        var approvalBranch = delegation.IsExternal
            ? delegation.ExternalBranchId
            : delegation.SourceDocument.BranchId;
        if (caller.Role != UserRole.Head || caller.BranchId != approvalBranch)
            throw new ArgumentException("التراجع عن التوجيه لرئيس قسم فرع الاعتماد فقط");

        try
        {
            await _tx.RunAsync(async token =>
            {
                delegation.RedirectedToSectionId = null;
                delegation.UpdatedAt = DateTime.UtcNow;
                delegation.Version++;
                _delegations.Update(delegation);

                await _alertOutbox.EnqueueSystemAsync(
                    new CreateSystemHeadAlertRequest(
                        HeadAlertKindCatalog.DelegationPendingApproval,
                        HeadAlertMessages.DelegationPendingApproval(new(
                            delegation.SourceDocument.CreatedBy?.FullName ?? string.Empty,
                            DocumentDelegationService.SourceLabel(delegation.SourceDocument),
                            DocumentDelegationService.AlertCourtAfterDaira(delegation.DelegatedCourt))),
                        HeadAlertAudience.SingleHead,
                        approvalBranch!.Value,
                        userId,
                        DocumentId: delegation.SourceDocumentId,
                        DelegationId: delegation.Id,
                        RecipientUserId: userId),
                    new HeadAlertSupersedePlan(
                        delegation.Id,
                        AppealId: null,
                        HeadAlertKindCatalog.DelegationKinds,
                        "delegation_redirect_recalled"),
                    occurredAt: DateTime.UtcNow,
                    ct: token);

                await _uow.SaveChangesAsync(token);
                await _audit.LogAsync(actorName, "recall_redirect_delegation",
                    delegation.SourceDocumentId, delegation.SourceDocument.DocumentType,
                    $"تراجع عن توجيه الإنابة (رقم {delegation.Id}) للشعبة — عادت لرئيس القسم", token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsConcurrencyViolation(ex))
        {
            // تراجعان متزامنان حقيقيان — 409 ودية بدل 500 خام.
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الإنابة وحاول مجددًا", ex);
        }

        return await _delegations.GetByIdWithDetailsAsync(delegation.Id, ct) is { } reloaded
            ? DocumentDelegationService.ToDto(reloaded, reloaded.SourceDocument, ServerClock.CurrentYear(_clock, _timeZone))
            : null;
    }
}

/// <summary>
/// رفض الدائرة الخطأ برسالة تُعيد المحامي للتصحيح (§7.4) — المعتمد الحالي
/// فقط (نطاق الاعتماد نفسه). تبقى معلّقة بفلتر «مرفوض بانتظار التصحيح»،
/// ويُشعَر محامي المنيب (بطاقة ملفه + تنبيه)، وتحرّرها تعديله.
/// </summary>
public sealed record RejectDocumentDelegationCommand(
    int DelegationId,
    RejectDelegationRequest Request,
    int UserId,
    string? ActorName) : ICommand<DelegationDto?>;

internal sealed class RejectDocumentDelegationCommandHandler
    : CommandHandlerBase<RejectDocumentDelegationCommand, DelegationDto?>
{
    private readonly IDelegationRepository _delegations;
    private readonly IUserRepository _users;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IHeadAlertService _alerts;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public RejectDocumentDelegationCommandHandler(
        IDelegationRepository delegations,
        IUserRepository users,
        IRepository<ExecutionCircuit> circuits,
        IHeadAlertOutbox alertOutbox,
        IHeadAlertService alerts,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IDbExceptionClassifier dbErrors,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<RejectDocumentDelegationCommand, DelegationDto?>> behaviors)
        : base(behaviors)
    {
        _delegations = delegations;
        _users = users;
        _circuits = circuits;
        _alertOutbox = alertOutbox;
        _alerts = alerts;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _dbErrors = dbErrors;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<DelegationDto?> HandleCoreAsync(
        RejectDocumentDelegationCommand command, CancellationToken ct)
    {
        var delegationId = command.DelegationId;
        var request = command.Request;
        var userId = command.UserId;
        var actorName = command.ActorName;
        var delegation = await _delegations.GetByIdWithDetailsAsync(delegationId, ct)
            ?? throw new ArgumentException("الإنابة غير موجودة");
        if (delegation.Status != DelegationStatusCatalog.PendingHead)
            throw new ArgumentException("لا يمكن رفض إنابة لم تعد معلّقة");
        if (delegation.AssignedLawyerId is not null)
            throw new ArgumentException("لا يمكن رفض إنابة بعد إسنادها");
        if (delegation.RejectReason is not null)
            throw new ArgumentException("الإنابة مرفوضة سلفًا — بانتظار تصحيح المحامي");
        if (request.Version.HasValue && delegation.Version != request.Version.Value)
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الإنابة وحاول مجددًا");
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason is null)
            throw new ArgumentException("سبب الرفض مطلوب — يُعرض للمحامي للتصحيح");
        if (reason.Length > 1000)
            throw new ArgumentException("طول سبب الرفض يتجاوز الحد الأقصى (1000 حرف)");

        var caller = await DocumentDelegationRules.LoadHeadCallerAsync(userId, null, _users, ct);
        var callerBranch = caller.BranchId
            ?? throw new ArgumentException("الرئيس دون فرع لا يمكنه رفض الإنابات");
        var (approvalBranch, approvalSection) = await DocumentDelegationRules.ResolveApprovalScopeAsync(delegation, _circuits, ct);
        DocumentDelegationRules.EnsureApprover(caller, callerBranch, approvalBranch, approvalSection);
        var sourceBranch = delegation.SourceDocument.BranchId;

        try
        {
            await _tx.RunAsync(async token =>
            {
                delegation.RejectReason = reason;
                delegation.UpdatedAt = DateTime.UtcNow;
                delegation.Version++;
                _delegations.Update(delegation);

                if (sourceBranch is not null)
                {
                    await _alertOutbox.EnqueueSystemAsync(
                        new CreateSystemHeadAlertRequest(
                            HeadAlertKindCatalog.DelegationRejectedWrongCourt,
                            HeadAlertMessages.DelegationRejectedWrongCourt(new(
                                delegation.Id,
                                DocumentDelegationService.CourtWithExecutionPrefix(delegation.DelegatedCourt),
                                DocumentDelegationService.SourceLabel(delegation.SourceDocument),
                                reason)),
                            HeadAlertAudience.SingleLawyer,
                            sourceBranch.Value,
                            userId,
                            DocumentId: delegation.SourceDocumentId,
                            TargetLawyerId: delegation.CreatedById,
                            DelegationId: delegation.Id,
                            RecipientUserId: delegation.CreatedById),
                        new HeadAlertSupersedePlan(
                            delegation.Id,
                            AppealId: null,
                            HeadAlertKindCatalog.DelegationKinds,
                            "delegation_rejected"),
                        occurredAt: DateTime.UtcNow,
                        ct: token);
                }
                else
                {
                    await _alerts.ArchiveByDelegationAsync(
                        delegation.Id,
                        HeadAlertKindCatalog.DelegationKinds,
                        "delegation_rejected_no_branch",
                        token);
                }

                await _uow.SaveChangesAsync(token);
                await _audit.LogAsync(actorName, "reject_delegation",
                    delegation.SourceDocumentId, delegation.SourceDocument.DocumentType,
                    $"رفض الإنابة (رقم {delegation.Id}) للدائرة الخطأ — السبب: {reason}", token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsConcurrencyViolation(ex))
        {
            // رفضان متزامنان حقيقيان — 409 ودية بدل 500 خام.
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الإنابة وحاول مجددًا", ex);
        }

        return await _delegations.GetByIdWithDetailsAsync(delegation.Id, ct) is { } reloaded
            ? DocumentDelegationService.ToDto(reloaded, reloaded.SourceDocument, ServerClock.CurrentYear(_clock, _timeZone))
            : null;
    }
}
