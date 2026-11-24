using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Alerts;

/// <summary>
/// إصدار يدوي للتنبيه (رئيس/شعبة لفرعه) — الدور والفرع قرار الخدمة، والملخص المنشأ
/// يُعاد داخل المعاملة نفسها (D3).
/// </summary>
public sealed record CreateManualHeadAlertCommand(
    CreateHeadAlertRequest Request,
    HeadAlertActor Actor,
    string? ActorName) : ICommand<HeadAlertSummaryDto>;

internal sealed class CreateManualHeadAlertCommandHandler
    : CommandHandlerBase<CreateManualHeadAlertCommand, HeadAlertSummaryDto>
{
    private readonly IHeadAlertManualWriteRules _rules;
    private readonly IUserRepository _users;
    private readonly IRepository<Branch> _branches;
    private readonly IHeadAlertRepository _alerts;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public CreateManualHeadAlertCommandHandler(
        IHeadAlertManualWriteRules rules,
        IUserRepository users,
        IRepository<Branch> branches,
        IHeadAlertRepository alerts,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<CreateManualHeadAlertCommand, HeadAlertSummaryDto>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _users = users;
        _branches = branches;
        _alerts = alerts;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<HeadAlertSummaryDto> HandleCoreAsync(
        CreateManualHeadAlertCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var actor = command.Actor;
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);

        // الدور قرار خدمة (D8): المتحكم لم يعد يفحصه.
        if (!RolePermissions.CanCreateAlerts(actor.Role))
            throw new UnauthorizedAccessException("الإصدار اليدوي مخصص لرئيس قسم/شعبة");

        // D11: الكتابة بلا فرع ⇒ 400 (عقد `POST` القائم) لا 403.
        var branchId = _rules.RequireBranchForWrite(actor);

        // تحقّق القاعدة يبقى دفاعًا ثانيًا: حساب معطّل، أو صار دوره غير مخوّل، أو رمز
        // بفرع متقادم — يُرفض 400 كما كان قبل نقل الصلاحية (لا تغيير عقد).
        var user = await _users.GetByIdAsync(actor.UserId, ct);
        if (user is null || !user.IsActive
            || !RolePermissions.CanCreateAlerts(user.Role)
            || user.BranchId != branchId)
            throw new ArgumentException("الإصدار اليدوي مخصص لرئيس قسم/شعبة مفعّل في فرعه");

        var rawMessage = request.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawMessage))
            throw new ArgumentException("نص التنبيه مطلوب");

        // الحدّ على نص المستخدم قبل البادئة، ثم سقف الأمان على النص النهائي.
        var withinLimit = HeadAlertTextLimits.EnsureWithinManualLimit(rawMessage);
        var message = HeadAlertTextLimits.EnsureWithinSafety(
            HeadAlertMessages.ManualMessage(user.Role, withinLimit));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("نص التنبيه مطلوب");

        var branch = await _branches.GetByIdAsync(branchId, ct);
        if (branch is null)
            throw new ArgumentException("الفرع غير موجود");

        var targetType = _rules.ParseManualTargetType(request.TargetType);
        var recipients = await _rules.ResolveRecipientsAsync(targetType, request, branchId, ct);
        if (recipients.Count == 0)
            throw new ArgumentException("لا يوجد مستلمون للتنبيه");

        var now = DateTime.UtcNow;
        var alert = _rules.BuildManualAlert(
            request, actor, branchId, targetType,
            _rules.ManualKindFor(targetType), message, recipients, now);

        // جسم 201 (D3) يجب أن يطابق صفَّ القائمة لاحقًا حقلًا بحقل، ولذلك يُعاد قراءته
        // بالإسقاط الواحد نفسه الذي تبنيه القوائم (لا تحويل الكيان)، **داخل المعاملة**
        // كي لا يبقى صفٌّ مثبَّت يعقبه 500 إن فشل الجلب (فيتكرر الإنشاء من العميل).
        var created = await _tx.RunAsync(async token =>
        {
            await _rules.AddManualAlertAsync(alert, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "create_alert",
                details: $"أصدر تنبيهاً يدوياً في فرع {branch.Name} بنطاق {targetType}: "
                    + HeadAlertTextLimits.PreviewForAudit(message),
                ct: token);

            return await _alerts.GetSummaryAsync(
                    alert.Id, recipientUserId: null, includeCounters: true, token)
                ?? throw new InvalidOperationException("تعذّر جلب ملخص التنبيه بعد إنشائه");
        }, ct);

        return created;
    }
}

/// <summary>حذف انتقائي لمستلم واحد من تنبيهات استئناف (§6.2/§6.5) — أفضل جهد.</summary>
public sealed record RemoveAppealAlertRecipientCommand(
    int AppealId,
    int UserId) : ICommand<int>;

internal sealed class RemoveAppealAlertRecipientCommandHandler
    : CommandHandlerBase<RemoveAppealAlertRecipientCommand, int>
{
    private readonly IHeadAlertRepository _alerts;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;

    public RemoveAppealAlertRecipientCommandHandler(
        IHeadAlertRepository alerts,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IEnumerable<IPipelineBehavior<RemoveAppealAlertRecipientCommand, int>> behaviors)
        : base(behaviors)
    {
        _alerts = alerts;
        _uow = uow;
        _tx = tx;
    }

    protected override async Task<int> HandleCoreAsync(
        RemoveAppealAlertRecipientCommand command, CancellationToken ct)
    {
        var removed = 0;
        await _tx.RunAsync(async token =>
        {
            removed = await _alerts.RemoveAppealRecipientAsync(command.AppealId, command.UserId, token);
            await _uow.SaveChangesAsync(token);
        }, ct);
        return removed;
    }
}
