using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentAppealService : IDocumentAppealService
{
    // ── الإجراءات والملاحظات المستقلة ─────────────────────────────────────

    public async Task<List<AppealActionDto>> GetActionsAsync(int appealId, CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        return appeal.Actions
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => ToActionDto(a))
            .ToList();
    }

    public async Task<AppealActionDto> AddActionAsync(
        int appealId,
        AddAppealActionRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        EnsureAssignedFollower(appeal, userId);

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

        var reminderDuration = Bounded(request.ReminderDuration, 20, "مدة التذكير");
        var reminderColor = Bounded(request.ReminderColor, 20, "لون التذكير");
        var actionDate = Bounded(request.ActionDate, 50, "تاريخ الإجراء");

        var action = new AppealAction
        {
            AppealId = appeal.Id,
            Type = Bounded(request.Type, 20, "نوع الإجراء") ?? "action",
            // FR-009: تعقيم خادمي عند الكتابة (القائمة البيضاء المركزية) — النص المعروض
            // كـ HTML يُخزَّن معقّمًا أصلًا، وتعقيم العرض يبقى طبقة ثانية.
            Text = RequireText(HtmlInputSanitizer.Sanitize(request.Text), "نص الإجراء"),
            ActionDate = actionDate,
            ReminderDuration = reminderDuration,
            ReminderColor = reminderColor,
            // تاريخ أساس التذكير يُثبَّت مرة واحدة عند الإنشاء — ولا يكتب أي مسار تذكير في ActionDate.
            ReminderBaseDate = ActionReminderCalculator.BaseDateForNewReminder(reminderDuration, reminderColor, actionDate),
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
        };

        await _tx.RunAsync(async token =>
        {
            appeal.Actions.Add(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "add_appeal_action",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"أضاف إجراءً على الاستئناف (رقم {appeal.Id})", token);
        }, ct);

        return ToActionDto(action);
    }

    public async Task<AppealActionDto?> UpdateActionAsync(
        int appealId,
        int actionId,
        UpdateAppealActionRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        EnsureAssignedFollower(appeal, userId);
        var action = appeal.Actions.FirstOrDefault(a => a.Id == actionId)
            ?? throw new ArgumentException("الإجراء غير موجود");

        // ذات تحقق الإضافة أعلاه — التعديل يستبدل حقول الإجراء (نوع/نص/تاريخ) wholesale،
        // ولا يكتب أي حقل تذكير: حالة التذكير تُدار من `SetReminderAsync` و«نقل التذكير» حصرًا.
        ActionReminderCalculator.ValidateActionType(request.Type, "نوع الإجراء");
        var effectiveType = (request.Type ?? action.Type ?? "action").Trim();
        if (effectiveType == "action" && string.IsNullOrWhiteSpace(request.ActionDate))
            throw new ArgumentException("يجب إدخال تاريخ الإجراء");
        ActionReminderCalculator.ValidateActionDate(request.ActionDate, "تاريخ الإجراء");

        // FR-013: إبقاء القديم عند الغياب بصيغة صريحة بلا تحذير `CS8601` (الدلالة مطابقة تمامًا).
        var newType = Bounded(request.Type, 20, "نوع الإجراء");
        if (newType is not null)
            action.Type = newType;
        action.Text = RequireText(HtmlInputSanitizer.Sanitize(request.Text), "نص الإجراء");
        action.ActionDate = Bounded(request.ActionDate, 50, "تاريخ الإجراء");

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "update_appeal_action",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"عدّل إجراءً على الاستئناف (رقم {appeal.Id})", token);
        }, ct);

        return ToActionDto(action);
    }

    public async Task<bool> DeleteActionAsync(
        int appealId,
        int actionId,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        EnsureAssignedFollower(appeal, userId);
        var action = appeal.Actions.FirstOrDefault(a => a.Id == actionId)
            ?? throw new ArgumentException("الإجراء غير موجود");

        await _tx.RunAsync(async token =>
        {
            appeal.Actions.Remove(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "delete_appeal_action",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"حذف إجراءً من الاستئناف (رقم {appeal.Id})", token);
        }, ct);

        return true;
    }

    public async Task<bool> ClearReminderAsync(
        int appealId,
        int actionId,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        EnsureAssignedFollower(appeal, userId);
        var action = appeal.Actions.FirstOrDefault(a => a.Id == actionId)
            ?? throw new ArgumentException("الإجراء غير موجود");

        await ClearReminderCoreAsync(appeal, action, actorName, ct);
        return true;
    }

    /// <summary>
    /// تفريغ حالة التذكير وتدوينه — التنفيذ الوحيد للإلغاء (يستدعيه `ClearReminderAsync`
    /// ومسار `PUT .../reminder` بالحقلين الفارغين) فلا تختلف رسالة التدقيق بين المسارين.
    /// </summary>
    private async Task ClearReminderCoreAsync(
        DocumentAppeal appeal, AppealAction action, string? actorName, CancellationToken ct)
    {
        ClearReminderFields(action);

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "clear_appeal_reminder",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"ألغى تذكير إجراء على الاستئناف (رقم {appeal.Id})", token);
        }, ct);
    }

    /// <summary>
    /// إنشاء/تعديل تذكير إجراء استئناف قائم (مدة/لون) عبر مسار مستقل عن تعديل الإجراء —
    /// نظير `ExecutionActionService.SetReminderAsync`. يهيّئ `ReminderBaseDate` من تاريخ
    /// الإجراء مرة واحدة فقط، والحقلان الفارغان يعنيان الإلغاء.
    /// </summary>
    public async Task<AppealActionDto> SetReminderAsync(
        int appealId,
        int actionId,
        SetAppealReminderRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        EnsureAssignedFollower(appeal, userId);
        var action = appeal.Actions.FirstOrDefault(a => a.Id == actionId)
            ?? throw new ArgumentException("الإجراء غير موجود");

        ActionReminderCalculator.ValidateReminder(request.ReminderDuration, request.ReminderColor);
        var reminderDuration = Bounded(request.ReminderDuration, 20, "مدة التذكير");
        var reminderColor = Bounded(request.ReminderColor, 20, "لون التذكير");

        if (reminderDuration is null && reminderColor is null)
        {
            await ClearReminderCoreAsync(appeal, action, actorName, ct);
            return ToActionDto(action);
        }

        action.ReminderDuration = reminderDuration;
        action.ReminderColor = reminderColor;
        // بلا سقوط إلى `CreatedAt`: الأساس null يبقى سقوطًا ديناميكيًا موسومًا (§ الخطة).
        action.ReminderBaseDate ??= ActionDateParser.TryParse(action.ActionDate);

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "set_appeal_reminder",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"ضبط تذكير إجراء على الاستئناف (رقم {appeal.Id}) ({reminderDuration ?? "—"} — {reminderColor ?? "—"})", token);
        }, ct);

        return ToActionDto(action);
    }

    /// <summary>
    /// نقل تذكير إجراء استئناف إلى يوم استحقاق جديد من نافذة اليوم — مرآة
    /// `ExecutionActionService.RescheduleReminderAsync` للملفات: المدخل يوم الاستحقاق
    /// نفسه، وتُكتب `ReminderBaseDate` خلفيًا مع بقاء المدة — **تاريخ الإجراء لا يُكتب**.
    /// الغياب يُردّ `ArgumentException` (عرف مسار الاستئناف) لا `KeyNotFoundException`.
    /// </summary>
    public async Task<AppealActionDto> RescheduleAppealReminderAsync(
        int appealId,
        int actionId,
        string? newDueDate,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        EnsureAssignedFollower(appeal, userId);
        var action = appeal.Actions.FirstOrDefault(a => a.Id == actionId)
            ?? throw new ArgumentException("الإجراء غير موجود");

        if (string.IsNullOrWhiteSpace(newDueDate))
            throw new ArgumentException("اليوم الجديد مطلوب — استخدم مثال: 1/8/2026");

        var due = DocumentValidator.ParseDateTime(newDueDate.Trim(), "اليوم الجديد")
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
            await _audit.LogAsync(actorName, "reschedule_appeal_reminder",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"نقل تذكير إجراء على الاستئناف (رقم {appeal.Id}) إلى {due:yyyy-MM-dd} "
                + $"(تاريخ الأساس: {oldBaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—"} ← {action.ReminderBaseDate:yyyy-MM-dd}) — تاريخ الإجراء لم يتغير",
                token);
        }, ct);

        return ToActionDto(action);
    }

    public async Task<List<AppealReminderDto>> GetRemindersAsync(int userId, CancellationToken ct = default)
    {
        var rows = await ListReminderRowsAsync(userId, ct);
        return rows
            .Select(r =>
            {
                var (dueDate, suspect) = ActionReminderCalculator.TryComputeDueDate(
                    r.ReminderBaseDate, r.ActionDate, r.ReminderDuration, r.CreatedAt);
                return new AppealReminderDto(
                    r.ActionId,
                    r.AppealId,
                    r.DocumentId,
                    r.AppealTitle,
                    r.Text,
                    r.ActionDate,
                    r.ReminderDuration,
                    r.ReminderColor,
                    dueDate,
                    suspect,
                    r.DocumentLabel);
            })
            .OrderBy(r => r.DueDate)
            .ThenBy(r => r.AppealId)
            .ToList();
    }

    private async Task<List<(int ActionId, int AppealId, int DocumentId, string AppealTitle,
        string DocumentLabel, string Text, string? ActionDate, string? ReminderDuration,
        string? ReminderColor, DateTime? ReminderBaseDate, DateTime CreatedAt)>>
        ListReminderRowsAsync(int userId, CancellationToken ct)
    {
        var appeals = await _appeals.ListByAssigneeAsync(userId, null, asNoTracking: true, ct);
        var rows = new List<(int, int, int, string, string, string, string?, string?, string?, DateTime?, DateTime)>();
        foreach (var appeal in appeals)
        {
            var documentLabel = DocumentDisplayName.Build(appeal.Document);
            foreach (var action in appeal.Actions.Where(a =>
                         a.ReminderDuration != null || a.ReminderColor != null || a.ReminderBaseDate != null))
            {
                rows.Add((action.Id, appeal.Id, appeal.DocumentId,
                    AppealTitleOf(appeal), documentLabel, action.Text, action.ActionDate,
                    action.ReminderDuration, action.ReminderColor, action.ReminderBaseDate, action.CreatedAt));
            }
        }
        return rows;
    }

    /// <summary>
    /// تفريغ حالة التذكير كلها (المدة/اللون/الأساس) — نقطة واحدة للإلغاء المباشر
    /// وللـ`PUT` بالحقلين الفارغين، فلا ينحرف أحد المسارين عن الآخر.
    /// </summary>
    private static void ClearReminderFields(AppealAction action)
    {
        action.ReminderDuration = null;
        action.ReminderColor = null;
        action.ReminderBaseDate = null;
    }
}
