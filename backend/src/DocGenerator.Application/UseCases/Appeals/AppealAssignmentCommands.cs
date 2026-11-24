using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Appeals;

/// <summary>
/// إسناد استئناف منظور لمحامٍ مختص — رئيس القسم/الشعبة بنطاقه، مع تنبيه المحامي
/// عبر `Outbox` والتدقيق.
/// </summary>
public sealed record AssignDocumentAppealCommand(
    int AppealId,
    AssignAppealRequest Request,
    int UserId,
    string? ActorName) : ICommand<AppealDto?>;

public sealed class AssignDocumentAppealCommandHandler
    : CommandHandlerBase<AssignDocumentAppealCommand, AppealDto?>
{
    private readonly IAppealRepository _appeals;
    private readonly IUserRepository _users;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public AssignDocumentAppealCommandHandler(
        IAppealRepository appeals,
        IUserRepository users,
        IHeadAlertOutbox alertOutbox,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<AssignDocumentAppealCommand, AppealDto?>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _users = users;
        _alertOutbox = alertOutbox;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<AppealDto?> HandleCoreAsync(
        AssignDocumentAppealCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var appeal = await DocumentAppealRules.LoadForHeadActionAsync(
            command.AppealId, command.UserId, _appeals, _users, ct);
        if (appeal.AssignedLawyerId is not null)
            throw new ArgumentException("لا يمكن إسناد استئناف أُسند سلفًا");
        if (request.Version.HasValue && appeal.Version != request.Version.Value)
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الاستئناف وحاول مجددًا");

        var lawyer = await DocumentAppealRules.ResolveTargetLawyerAsync(
            request.AssignedLawyerId, appeal.Document.BranchId, _users, ct);

        appeal.AssignedLawyerId = lawyer.Id;
        appeal.AssignedAt = DateTime.UtcNow;
        appeal.UpdatedAt = DateTime.UtcNow;
        appeal.Version++;

        // التعارض الحقيقي (سباق بعد الفحص) يرميه الرمز (`IsConcurrencyToken`) —
        // المعالج العام يترجمه `409` برسالة ودية (RF-010).
        await _tx.RunAsync(async token =>
        {
            _appeals.Update(appeal);
            await DocumentAppealRules.EnqueueAppealAssignedAsync(
                appeal, lawyer, command.UserId, DateTime.UtcNow,
                ServerClock.CurrentYear(_clock, _timeZone), _alertOutbox, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "assign_appeal",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"أسند الاستئناف (رقم {appeal.Id}) إلى المحامي {lawyer.FullName}", token);
        }, ct);

        return DocumentAppealService.ToDto(appeal, ServerClock.CurrentYear(_clock, _timeZone));
    }
}

/// <summary>نقل استئناف من محامٍ إلى آخر — رئيس القسم/الشعبة بنطاقه.</summary>
public sealed record TransferDocumentAppealCommand(
    int AppealId,
    TransferAppealRequest Request,
    int UserId,
    string? ActorName) : ICommand<AppealDto?>;

public sealed class TransferDocumentAppealCommandHandler
    : CommandHandlerBase<TransferDocumentAppealCommand, AppealDto?>
{
    private readonly IAppealRepository _appeals;
    private readonly IUserRepository _users;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public TransferDocumentAppealCommandHandler(
        IAppealRepository appeals,
        IUserRepository users,
        IHeadAlertOutbox alertOutbox,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<TransferDocumentAppealCommand, AppealDto?>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _users = users;
        _alertOutbox = alertOutbox;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<AppealDto?> HandleCoreAsync(
        TransferDocumentAppealCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var appeal = await DocumentAppealRules.LoadForHeadActionAsync(
            command.AppealId, command.UserId, _appeals, _users, ct);
        if (request.TargetLawyerId == appeal.AssignedLawyerId)
            throw new ArgumentException("الاستئناف مسند لهذا المحامي سلفًا");
        if (request.Version.HasValue && appeal.Version != request.Version.Value)
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الاستئناف وحاول مجددًا");

        var lawyer = await DocumentAppealRules.ResolveTargetLawyerAsync(
            request.TargetLawyerId, appeal.Document.BranchId, _users, ct);

        appeal.AssignedLawyerId = lawyer.Id;
        appeal.AssignedAt = DateTime.UtcNow;
        appeal.UpdatedAt = DateTime.UtcNow;
        appeal.Version++;

        await _tx.RunAsync(async token =>
        {
            _appeals.Update(appeal);
            await DocumentAppealRules.EnqueueAppealAssignedAsync(
                appeal, lawyer, command.UserId, DateTime.UtcNow,
                ServerClock.CurrentYear(_clock, _timeZone), _alertOutbox, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "transfer_appeal",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"نقل الاستئناف (رقم {appeal.Id}) إلى المحامي {lawyer.FullName}", token);
        }, ct);

        return DocumentAppealService.ToDto(appeal, ServerClock.CurrentYear(_clock, _timeZone));
    }
}

/// <summary>نقل كل استئنافات محامٍ منظورًا إلى محامٍ آخر — بنطاق المنفِّذ (§5.5).</summary>
public sealed record TransferAllDocumentAppealsCommand(
    TransferAllAppealsRequest Request,
    int? HeadBranchId,
    int? OwnerSectionId,
    string? ActorName) : ICommand<int>;

public sealed class TransferAllDocumentAppealsCommandHandler
    : CommandHandlerBase<TransferAllDocumentAppealsCommand, int>
{
    private readonly IAppealRepository _appeals;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public TransferAllDocumentAppealsCommandHandler(
        IAppealRepository appeals,
        IUserRepository users,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<TransferAllDocumentAppealsCommand, int>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _users = users;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<int> HandleCoreAsync(
        TransferAllDocumentAppealsCommand command, CancellationToken ct)
    {
        var request = command.Request;
        if (command.HeadBranchId is null)
            throw new ArgumentException("رئيس القسم دون فرع لا يمكنه نقل الاستئنافات");
        if (request.SourceLawyerId == request.TargetLawyerId)
            throw new ArgumentException("لا يمكن نقل الاستئنافات إلى المحامي نفسه");

        var target = await DocumentAppealRules.ResolveTargetLawyerAsync(
            request.TargetLawyerId, command.HeadBranchId.Value, _users, ct);

        // النقل الجملة للمنظورة ضمن نطاق المنفِّذ فقط (§5.5) — القراءة داخل المعاملة
        // (F5: منع الكتابة فوق تغيير متزامن)، وإعادة فحص `Pending` قبل كل كتابة،
        // والرمز `IsConcurrencyToken` على `Version` يترجم السباق `409` ودية عبر
        // المعالج العام — مرآة المسارين المفردين (`AssignAsync`/`TransferAsync`).
        var moved = 0;
        await _tx.RunAsync(async token =>
        {
            // D5: العدّاد يُصفَّر داخل المعاملة — فلو أُعيد تنفيذ الـlambda (إعادة
            // محاولة مستقبلية عند تفعيل `EnableRetryOnFailure`) لا يزدوج العدّ.
            moved = 0;
            var pendingOnly = await _appeals.ListPendingByAssigneeInScopeAsync(
                request.SourceLawyerId, command.HeadBranchId.Value, command.OwnerSectionId, token);
            foreach (var appeal in pendingOnly)
            {
                if (appeal.Status != AppealStatusCatalog.Pending)
                    continue;
                appeal.AssignedLawyerId = target.Id;
                appeal.AssignedAt = DateTime.UtcNow;
                appeal.UpdatedAt = DateTime.UtcNow;
                appeal.Version++;
                _appeals.Update(appeal);
                moved++;
            }
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "transfer_all_appeals",
                details: $"نقل {moved} استئنافًا منظورًا من المحامي (رقم {request.SourceLawyerId}) إلى المحامي {target.FullName}", ct: token);
        }, ct);

        return moved;
    }
}
