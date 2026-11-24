using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Appeals;

/// <summary>
/// إحالة استئناف من رئيس الشعبة لرئيس قسم فرعه (§6.2) — استثناء لمرة واحدة يبقى حتى
/// الحسم (قرار §2.22)، مع تنبيه الرئيس والتدقيق وترجمة سباق التزامن إلى 409 ودية.
/// </summary>
public sealed record ForwardDocumentAppealCommand(
    int AppealId,
    ForwardAppealRequest Request,
    int CallerUserId,
    string? ActorName) : ICommand<AppealDto?>;

public sealed class ForwardDocumentAppealCommandHandler
    : CommandHandlerBase<ForwardDocumentAppealCommand, AppealDto?>
{
    private readonly IAppealRepository _appeals;
    private readonly IUserRepository _users;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public ForwardDocumentAppealCommandHandler(
        IAppealRepository appeals,
        IUserRepository users,
        IHeadAlertOutbox alertOutbox,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IDbExceptionClassifier dbErrors,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<ForwardDocumentAppealCommand, AppealDto?>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _users = users;
        _alertOutbox = alertOutbox;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _dbErrors = dbErrors;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<AppealDto?> HandleCoreAsync(
        ForwardDocumentAppealCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var appeal = await _appeals.GetByIdWithDetailsAsync(command.AppealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        if (appeal.Status != AppealStatusCatalog.Pending)
            throw new ArgumentException("لا يمكن إحالة استئناف لم يبق منظورًا");
        if (appeal.AssignedLawyerId is not null)
            throw new ArgumentException("لا يمكن إحالة استئناف أُسند سلفًا — التراجع قبل الإسناد فقط");
        if (appeal.ForwardState == AppealForwardCatalog.ForwardedToHead)
            throw new ArgumentException("الاستئناف محال سلفًا لرئيس القسم");
        if (request.Version.HasValue && appeal.Version != request.Version.Value)
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الاستئناف وحاول مجددًا");

        var caller = await _users.GetByIdAsync(command.CallerUserId, ct)
            ?? throw new ArgumentException("المستخدم غير موجود");
        if (caller.Role != UserRole.SubHead)
            throw new ArgumentException("الإحالة لرئيس الشعبة فقط");
        var doc = appeal.Document;
        if (doc.BranchId is null || doc.BranchId != caller.BranchId)
            throw new ArgumentException("لا يمكنك إحالة هذا الاستئناف — ليس ضمن فرعك");
        if (doc.ExecutionCircuitId is null || doc.ExecutionCircuit?.SectionId != caller.SectionId || caller.SectionId is null)
            throw new ArgumentException("الدائرة ليست ضمن نطاقك");

        var head = await _users.FindActiveHeadAsync(UserRole.Head, doc.BranchId, null, ct)
            ?? throw new ArgumentException("لا يوجد رئيس قسم مفعّل في الفرع — تعذّرت الإحالة");
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason?.Length > 1000)
            throw new ArgumentException("طول سبب الإحالة يتجاوز الحد الأقصى (1000 حرف)");

        var now = DateTime.UtcNow;
        try
        {
            await _tx.RunAsync(async token =>
            {
                appeal.ForwardedById = caller.Id;
                appeal.ForwardedAt = now;
                appeal.ForwardState = AppealForwardCatalog.ForwardedToHead;
                appeal.ForwardReason = reason;
                appeal.UpdatedAt = now;
                appeal.Version++;
                _appeals.Update(appeal);
                await DocumentAppealRules.EnqueueAppealForwardedAsync(
                    appeal, doc, caller, head, now,
                    ServerClock.CurrentYear(_clock, _timeZone), _alertOutbox, token);
                await _uow.SaveChangesAsync(token);
                await _audit.LogAsync(command.ActorName, "forward_appeal",
                    appeal.DocumentId, appeal.Document.DocumentType,
                    $"يرجى التفضل بإسناد الاستئناف (ملف {appeal.DocumentId} — محال من {caller.FullName})"
                    + (reason is null ? string.Empty : $" — السبب: {reason}"), token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors?.IsConcurrencyViolation(ex) == true)
        {
            // إحالتان متزامنتان حقيقيتان — 409 ودية بدل 500 خام.
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الاستئناف وحاول مجددًا", ex);
        }

        return DocumentAppealService.ToDto(appeal, ServerClock.CurrentYear(_clock, _timeZone));
    }
}

/// <summary>
/// التراجع عن الإحالة قبل الإسناد (§6.5): استرجاع المحيل أو إعادة المستلم —
/// يُصفَّر الاستثناء وتُعاد البدائل للمحيل، مع تنبيه/أرشفة وتدقيق.
/// </summary>
public sealed record RecallAppealForwardCommand(
    int AppealId,
    int CallerUserId,
    string? ActorName) : ICommand<AppealDto?>;

public sealed class RecallAppealForwardCommandHandler
    : CommandHandlerBase<RecallAppealForwardCommand, AppealDto?>
{
    private readonly IAppealRepository _appeals;
    private readonly IUserRepository _users;
    private readonly IHeadAlertService _alerts;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public RecallAppealForwardCommandHandler(
        IAppealRepository appeals,
        IUserRepository users,
        IHeadAlertService alerts,
        IHeadAlertOutbox alertOutbox,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IDbExceptionClassifier dbErrors,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<RecallAppealForwardCommand, AppealDto?>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _users = users;
        _alerts = alerts;
        _alertOutbox = alertOutbox;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _dbErrors = dbErrors;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<AppealDto?> HandleCoreAsync(
        RecallAppealForwardCommand command, CancellationToken ct)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(command.AppealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        if (appeal.Status != AppealStatusCatalog.Pending)
            throw new ArgumentException("لا يمكن التراجع عن إحالة استئناف لم يبق منظورًا");
        if (appeal.AssignedLawyerId is not null)
            throw new ArgumentException("تعذّر التراجع بعد الإسناد — انقل الاستئناف نقلًا عاديًا");
        if (appeal.ForwardState != AppealForwardCatalog.ForwardedToHead || appeal.ForwardedById is null)
            throw new ArgumentException("لا توجد إحالة مفتوحة لهذا الاستئناف");

        var caller = await _users.GetByIdAsync(command.CallerUserId, ct)
            ?? throw new ArgumentException("المستخدم غير موجود");
        var doc = appeal.Document;
        var isForwarder = appeal.ForwardedById == caller.Id;
        var isDivisionHead = caller.Role == UserRole.Head
            && doc.BranchId is not null && doc.BranchId == caller.BranchId;
        if (!isForwarder && !isDivisionHead)
            throw new ArgumentException("التراجع للمحيل أو لرئيس قسم الفرع فقط");
        var forwarderId = appeal.ForwardedById.Value;
        var forwarder = await _users.GetByIdAsync(forwarderId, ct);
        var now = DateTime.UtcNow;

        try
        {
            await _tx.RunAsync(async token =>
            {
                appeal.ForwardState = AppealForwardCatalog.Owned;
                appeal.ForwardedById = null;
                appeal.ForwardedAt = null;
                appeal.ForwardReason = null;
                appeal.UpdatedAt = now;
                appeal.Version++;
                _appeals.Update(appeal);
                await DocumentAppealRules.EnqueueAppealRecalledAsync(
                    appeal, doc, caller, forwarder, now,
                    ServerClock.CurrentYear(_clock, _timeZone), _alertOutbox, _alerts, token);
                await _uow.SaveChangesAsync(token);
                await _audit.LogAsync(command.ActorName, "recall_forward",
                    appeal.DocumentId, appeal.Document.DocumentType,
                    isForwarder
                        ? $"استرجع المحيل إحالته للاستئناف (رقم {appeal.Id})"
                        : $"أعاد رئيس القسم الإحالة (رقم {appeal.Id}) لمحيلها", token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors?.IsConcurrencyViolation(ex) == true)
        {
            // تراجعان متزامنان حقيقيان — 409 ودية بدل 500 خام.
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الاستئناف وحاول مجددًا", ex);
        }

        return DocumentAppealService.ToDto(appeal, ServerClock.CurrentYear(_clock, _timeZone));
    }
}
