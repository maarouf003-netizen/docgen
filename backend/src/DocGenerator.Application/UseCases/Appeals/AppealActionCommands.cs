using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.Appeals;

/// <summary>
/// إضافة إجراء/ملاحظة على استئناف — المحامي المتابع فقط، مع تعقيم النص والتحقق من
/// الإجراء/التذكير والتدقيق. مرآة قواعد إجراءات الملف.
/// </summary>
public sealed record AddAppealActionCommand(
    int AppealId,
    AddAppealActionRequest Request,
    int UserId,
    string? ActorName) : ICommand<AppealActionDto>;

public sealed class AddAppealActionCommandHandler
    : CommandHandlerBase<AddAppealActionCommand, AppealActionDto>
{
    private readonly IAppealRepository _appeals;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public AddAppealActionCommandHandler(
        IAppealRepository appeals,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<AddAppealActionCommand, AppealActionDto>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<AppealActionDto> HandleCoreAsync(
        AddAppealActionCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var appeal = await _appeals.GetByIdWithDetailsAsync(command.AppealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        DocumentAppealService.EnsureAssignedFollower(appeal, command.UserId);

        // سدّ ثغرة الكتابة الحرة: مسار إجراءات الملفات يرفض المدد/الألوان/التواريخ الميتة
        // (NormalizeAction/NormalizeReminder) بينما كان هذا المسار يقبل أي نص بطول مباح،
        // فتُخزَّن قيم تُفسَّر صفرًا أو بتاريخ الإنشاء بصمت وقت القراءة.
        ActionReminderCalculator.ValidateActionType(request.Type, "نوع الإجراء");
        // مرآة قاعدة إجراءات الملف (`DocumentService.Actions`): الإجراء بلا تاريخ
        // مرفوض (الملاحظة اختيارية) — بدل تخزين تاريخ ميت.
        var effectiveType = (request.Type ?? "action").Trim();
        if (effectiveType == "action" && string.IsNullOrWhiteSpace(request.ActionDate))
            throw new ArgumentException("يجب إدخال تاريخ الإجراء");
        ActionReminderCalculator.ValidateActionDate(request.ActionDate, "تاريخ الإجراء");
        ActionReminderCalculator.ValidateReminder(request.ReminderDuration, request.ReminderColor);

        var reminderDuration = DocumentAppealService.Bounded(request.ReminderDuration, 20, "مدة التذكير");
        var reminderColor = DocumentAppealService.Bounded(request.ReminderColor, 20, "لون التذكير");
        var actionDate = DocumentAppealService.Bounded(request.ActionDate, 50, "تاريخ الإجراء");

        var action = new AppealAction
        {
            AppealId = appeal.Id,
            Type = DocumentAppealService.Bounded(request.Type, 20, "نوع الإجراء") ?? "action",
            // FR-009: تعقيم خادمي عند الكتابة (القائمة البيضاء المركزية) — النص المعروض
            // كـ HTML يُخزَّن معقّمًا أصلًا، وتعقيم العرض يبقى طبقة ثانية.
            Text = DocumentAppealService.RequireText(HtmlInputSanitizer.Sanitize(request.Text), "نص الإجراء"),
            ActionDate = actionDate,
            ReminderDuration = reminderDuration,
            ReminderColor = reminderColor,
            // تاريخ أساس التذكير يُثبَّت مرة واحدة عند الإنشاء — ولا يكتب أي مسار تذكير في ActionDate.
            ReminderBaseDate = ActionReminderCalculator.BaseDateForNewReminder(reminderDuration, reminderColor, actionDate),
            CreatedById = command.UserId,
            CreatedAt = DateTime.UtcNow,
        };

        await _tx.RunAsync(async token =>
        {
            appeal.Actions.Add(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "add_appeal_action",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"أضاف إجراءً على الاستئناف (رقم {appeal.Id})", token);
        }, ct);

        return DocumentAppealService.ToActionDto(action);
    }
}

/// <summary>تعديل إجراء/ملاحظة استئناف — المحامي المتابع فقط (بلا كتابة حقول التذكير).</summary>
public sealed record UpdateAppealActionCommand(
    int AppealId,
    int ActionId,
    UpdateAppealActionRequest Request,
    int UserId,
    string? ActorName) : ICommand<AppealActionDto?>;

public sealed class UpdateAppealActionCommandHandler
    : CommandHandlerBase<UpdateAppealActionCommand, AppealActionDto?>
{
    private readonly IAppealRepository _appeals;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public UpdateAppealActionCommandHandler(
        IAppealRepository appeals,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<UpdateAppealActionCommand, AppealActionDto?>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<AppealActionDto?> HandleCoreAsync(
        UpdateAppealActionCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var appeal = await _appeals.GetByIdWithDetailsAsync(command.AppealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        DocumentAppealService.EnsureAssignedFollower(appeal, command.UserId);
        var action = appeal.Actions.FirstOrDefault(a => a.Id == command.ActionId)
            ?? throw new ArgumentException("الإجراء غير موجود");

        // ذات تحقق الإضافة أعلاه — التعديل يستبدل حقول الإجراء (نوع/نص/تاريخ) wholesale،
        // ولا يكتب أي حقل تذكير: حالة التذكير تُدار من `SetReminderAsync` و«نقل التذكير» حصرًا.
        ActionReminderCalculator.ValidateActionType(request.Type, "نوع الإجراء");
        var effectiveType = (request.Type ?? action.Type ?? "action").Trim();
        if (effectiveType == "action" && string.IsNullOrWhiteSpace(request.ActionDate))
            throw new ArgumentException("يجب إدخال تاريخ الإجراء");
        ActionReminderCalculator.ValidateActionDate(request.ActionDate, "تاريخ الإجراء");

        // FR-013: إبقاء القديم عند الغياب بصيغة صريحة بلا تحذير `CS8601` (الدلالة مطابقة تمامًا).
        var newType = DocumentAppealService.Bounded(request.Type, 20, "نوع الإجراء");
        if (newType is not null)
            action.Type = newType;
        action.Text = DocumentAppealService.RequireText(HtmlInputSanitizer.Sanitize(request.Text), "نص الإجراء");
        action.ActionDate = DocumentAppealService.Bounded(request.ActionDate, 50, "تاريخ الإجراء");

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "update_appeal_action",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"عدّل إجراءً على الاستئناف (رقم {appeal.Id})", token);
        }, ct);

        return DocumentAppealService.ToActionDto(action);
    }
}

/// <summary>حذف إجراء استئناف — المحامي المتابع فقط.</summary>
public sealed record DeleteAppealActionCommand(
    int AppealId,
    int ActionId,
    int UserId,
    string? ActorName) : ICommand<bool>;

public sealed class DeleteAppealActionCommandHandler
    : CommandHandlerBase<DeleteAppealActionCommand, bool>
{
    private readonly IAppealRepository _appeals;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public DeleteAppealActionCommandHandler(
        IAppealRepository appeals,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<DeleteAppealActionCommand, bool>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<bool> HandleCoreAsync(
        DeleteAppealActionCommand command, CancellationToken ct)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(command.AppealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        DocumentAppealService.EnsureAssignedFollower(appeal, command.UserId);
        var action = appeal.Actions.FirstOrDefault(a => a.Id == command.ActionId)
            ?? throw new ArgumentException("الإجراء غير موجود");

        await _tx.RunAsync(async token =>
        {
            appeal.Actions.Remove(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "delete_appeal_action",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"حذف إجراءً من الاستئناف (رقم {appeal.Id})", token);
        }, ct);

        return true;
    }
}
