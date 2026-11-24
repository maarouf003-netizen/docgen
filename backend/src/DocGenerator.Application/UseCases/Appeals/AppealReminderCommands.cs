using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Appeals;

/// <summary>إلغاء تذكير إجراء استئناف — المحامي المتابع فقط، بتدقيق موحّد مع مسار الضبط الفارغ.</summary>
public sealed record ClearAppealReminderCommand(
    int AppealId,
    int ActionId,
    int UserId,
    string? ActorName) : ICommand<bool>;

public sealed class ClearAppealReminderCommandHandler
    : CommandHandlerBase<ClearAppealReminderCommand, bool>
{
    private readonly IAppealRepository _appeals;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public ClearAppealReminderCommandHandler(
        IAppealRepository appeals,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<ClearAppealReminderCommand, bool>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<bool> HandleCoreAsync(
        ClearAppealReminderCommand command, CancellationToken ct)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(command.AppealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        DocumentAppealService.EnsureAssignedFollower(appeal, command.UserId);
        var action = appeal.Actions.FirstOrDefault(a => a.Id == command.ActionId)
            ?? throw new ArgumentException("الإجراء غير موجود");

        await DocumentAppealRules.ClearReminderCoreAsync(
            appeal, action, command.ActorName, _uow, _tx, _audit, ct);
        return true;
    }
}

/// <summary>
/// إنشاء/تعديل تذكير إجراء استئناف (مدة/لون) مستقلًا عن تعديل الإجراء؛
/// الحقلان الفارغان يعنيان الإلغاء بنفس التدقيق الموحّد.
/// </summary>
public sealed record SetAppealReminderCommand(
    int AppealId,
    int ActionId,
    SetAppealReminderRequest Request,
    int UserId,
    string? ActorName) : ICommand<AppealActionDto>;

public sealed class SetAppealReminderCommandHandler
    : CommandHandlerBase<SetAppealReminderCommand, AppealActionDto>
{
    private readonly IAppealRepository _appeals;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public SetAppealReminderCommandHandler(
        IAppealRepository appeals,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<SetAppealReminderCommand, AppealActionDto>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<AppealActionDto> HandleCoreAsync(
        SetAppealReminderCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var appeal = await _appeals.GetByIdWithDetailsAsync(command.AppealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        DocumentAppealService.EnsureAssignedFollower(appeal, command.UserId);
        var action = appeal.Actions.FirstOrDefault(a => a.Id == command.ActionId)
            ?? throw new ArgumentException("الإجراء غير موجود");

        ActionReminderCalculator.ValidateReminder(request.ReminderDuration, request.ReminderColor);
        var reminderDuration = DocumentAppealService.Bounded(request.ReminderDuration, 20, "مدة التذكير");
        var reminderColor = DocumentAppealService.Bounded(request.ReminderColor, 20, "لون التذكير");

        if (reminderDuration is null && reminderColor is null)
        {
            await DocumentAppealRules.ClearReminderCoreAsync(
                appeal, action, command.ActorName, _uow, _tx, _audit, ct);
            return DocumentAppealService.ToActionDto(action);
        }

        action.ReminderDuration = reminderDuration;
        action.ReminderColor = reminderColor;
        // بلا سقوط إلى `CreatedAt`: الأساس null يبقى سقوطًا ديناميكيًا موسومًا (§ الخطة).
        action.ReminderBaseDate ??= ActionDateParser.TryParse(action.ActionDate);

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "set_appeal_reminder",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"ضبط تذكير إجراء على الاستئناف (رقم {appeal.Id}) ({reminderDuration ?? "—"} — {reminderColor ?? "—"})", token);
        }, ct);

        return DocumentAppealService.ToActionDto(action);
    }
}

/// <summary>
/// نقل تذكير إجراء استئناف إلى يوم استحقاق جديد — يكتب `ReminderBaseDate` وحده،
/// **ولا يمسّ `ActionDate`** (مرآة مسار إجراءات الملفات).
/// </summary>
public sealed record RescheduleAppealReminderCommand(
    int AppealId,
    int ActionId,
    string? NewDueDate,
    int UserId,
    string? ActorName) : ICommand<AppealActionDto>;

public sealed class RescheduleAppealReminderCommandHandler
    : CommandHandlerBase<RescheduleAppealReminderCommand, AppealActionDto>
{
    private readonly IAppealRepository _appeals;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public RescheduleAppealReminderCommandHandler(
        IAppealRepository appeals,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<RescheduleAppealReminderCommand, AppealActionDto>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<AppealActionDto> HandleCoreAsync(
        RescheduleAppealReminderCommand command, CancellationToken ct)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(command.AppealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        DocumentAppealService.EnsureAssignedFollower(appeal, command.UserId);
        var action = appeal.Actions.FirstOrDefault(a => a.Id == command.ActionId)
            ?? throw new ArgumentException("الإجراء غير موجود");

        if (string.IsNullOrWhiteSpace(command.NewDueDate))
            throw new ArgumentException("اليوم الجديد مطلوب — استخدم مثال: 1/8/2026");

        var due = DocumentValidator.ParseDateTime(command.NewDueDate.Trim(), "اليوم الجديد")
            ?? throw new ArgumentException("اليوم الجديد مطلوب — استخدم مثال: 1/8/2026");

        if (string.IsNullOrWhiteSpace(action.ReminderDuration)
            && string.IsNullOrWhiteSpace(action.ReminderColor)
            && action.ReminderBaseDate is null)
            throw new ArgumentException("لا يوجد تذكير لإعادة جدولته");

        var trimmedDuration = action.ReminderDuration?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedDuration) && !ActionReminderCalculator.ValidDurations.Contains(trimmedDuration))
            throw new ArgumentException("مدة التذكير غير معروفة — عدّل الإجراء من نافذة الإجراءات");

        // `DateTime` خالص في تاريخ الأساس؛ `ActionDate` واقعة قانونية لا تُعاد كتابتها ولا تُعاد صياغتها.
        var oldBaseDate = action.ReminderBaseDate;
        action.ReminderBaseDate = due.AddDays(-ActionReminderCalculator.DurationDays(trimmedDuration));

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "reschedule_appeal_reminder",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"نقل تذكير إجراء على الاستئناف (رقم {appeal.Id}) إلى {due:yyyy-MM-dd} "
                + $"(تاريخ الأساس: {oldBaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—"} ← {action.ReminderBaseDate:yyyy-MM-dd}) — تاريخ الإجراء لم يتغير",
                token);
        }, ct);

        return DocumentAppealService.ToActionDto(action);
    }
}
