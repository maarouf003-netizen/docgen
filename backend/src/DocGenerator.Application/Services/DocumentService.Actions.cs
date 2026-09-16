using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentService
{
    // ملاحظة إعادة هيكلة (المرحلة 3 — مؤجلة): عند أول تعديل يمس إجراءات التنفيذ
    // أو التذكيرات في هذا الملف، تُستخرج إلى خدمة مستقلة خلف واجهة (ExecutionActionService)
    // بدل إضافة المزيد هنا. المرجع: FIXES_LOG.md بند المعلقات #4.
    public async Task<List<ExecutionActionDto>> GetExecutionActionsAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return new List<ExecutionActionDto>();
        return doc.ExecutionActions
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new ExecutionActionDto(a.Id, a.Type, a.Text, a.ActionDate,
                a.ReminderDuration, a.ReminderColor, a.CreatedBy?.FullName, a.CreatedAt))
            .ToList();
    }

    public async Task<ExecutionActionDto> AddExecutionActionAsync(int documentId, AddExecutionActionRequest request, int userId, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            throw new KeyNotFoundException();

        var (type, text, actionDate) = NormalizeAction(request.Type, request.Text, request.ActionDate);
        var (reminderDuration, reminderColor) = NormalizeReminder(request.ReminderDuration, request.ReminderColor);

        var action = new ExecutionAction
        {
            DocumentId = documentId,
            Type = type,
            Text = text,
            ActionDate = actionDate,
            ReminderDuration = reminderDuration,
            ReminderColor = reminderColor,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
        };

        await _tx.RunAsync(async token =>
        {
            await _actions.AddAsync(action, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "action", doc.Id, doc.DocumentType,
                AuditWithActor($"أضاف {TypeLabel(type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), token);
        }, ct);
        return new ExecutionActionDto(action.Id, action.Type, action.Text, action.ActionDate,
            action.ReminderDuration, action.ReminderColor, actorName, action.CreatedAt);
    }

    public async Task<ExecutionActionDto?> UpdateExecutionActionAsync(int documentId, int actionId, UpdateExecutionActionRequest request, string? actorName, CancellationToken ct = default)
    {
        var (type, text, actionDate) = NormalizeAction(request.Type, request.Text, request.ActionDate);
        var (reminderDuration, reminderColor) = NormalizeReminder(request.ReminderDuration, request.ReminderColor);

        var action = await _actions.GetByIdAsync(actionId, ct);
        if (action is null || action.DocumentId != documentId)
            throw new KeyNotFoundException();

        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            throw new KeyNotFoundException();

        action.Type = type;
        action.Text = text;
        action.ActionDate = actionDate;
        action.ReminderDuration = reminderDuration;
        action.ReminderColor = reminderColor;

        await _tx.RunAsync(async token =>
        {
            _actions.Update(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "action", documentId, doc.DocumentType,
                AuditWithActor($"عدّل {TypeLabel(type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), token);
        }, ct);
        return new ExecutionActionDto(action.Id, action.Type, action.Text, action.ActionDate,
            action.ReminderDuration, action.ReminderColor, actorName, action.CreatedAt);
    }

    public async Task<bool> DeleteExecutionActionAsync(int documentId, int actionId, string? actorName, CancellationToken ct = default)
    {
        var action = await _actions.GetByIdAsync(actionId, ct);
        if (action is null || action.DocumentId != documentId)
            return false;

        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;

        return await _tx.RunAsync(async token =>
        {
            _actions.Remove(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "action", documentId, doc.DocumentType,
                AuditWithActor($"حذف {TypeLabel(action.Type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), token);
            return true;
        }, ct);
    }

    public async Task<bool> ClearReminderAsync(int documentId, int actionId, string? actorName, CancellationToken ct = default)
    {
        var action = await _actions.GetByIdAsync(actionId, ct);
        if (action is null || action.DocumentId != documentId)
            return false;

        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;

        action.ReminderDuration = null;
        action.ReminderColor = null;

        return await _tx.RunAsync(async token =>
        {
            _actions.Update(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "action", documentId, doc.DocumentType,
                AuditWithActor($"ألغى التذكير عن {TypeLabel(action.Type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), token);
            return true;
        }, ct);
    }

    /// <summary>
    /// يزرع إجراءات/ملاحظات «الإدخال المبدئي» (InitialActions) في جدول الإجراءات والملاحظات
    /// ذرّيًا ضمن معاملة حفظ المستند نفسها. كل عنصر يمر بـ NormalizeAction/NormalizeReminder
    /// فنُرفض الطلبات الخبيثة أو الناقصة ذرّيًا، وتُتجاهل الحقول الفارغة، ولا يُنشأ أي سجل
    /// مكرر (لا مقابل سجلات الملف القائمة ولا بين عناصر الطلب نفسه).
    /// </summary>
    private async Task SeedInitialActionsAsync(
        Document doc,
        List<AddExecutionActionRequest>? initialActions,
        int? userId,
        string? actorName,
        CancellationToken ct)
    {
        if (initialActions is null || initialActions.Count == 0 || userId is null)
            return;

        // منع التكرار عند التعديل: تُقارَن النصوص بعد التعقيم (نفس ما يُخزَّن) مع إجراءات الملف
        // القائمة، ومع بعضها داخل الطلب نفسه، بحيث لا يتضاعف السجل عند إعادة الحفظ.
        var existing = new HashSet<string>(doc.ExecutionActions
            .Where(a => a.Type is "action" or "note")
            .Select(a => $"{a.Type}|{a.Text}"));

        foreach (var request in initialActions)
        {
            // حقل لم يُعبأ أصلًا: يُتجاهل ولا يُفشل حفظ الملف.
            if (request is null || string.IsNullOrWhiteSpace(request.Text))
                continue;

            var (type, text, actionDate) = NormalizeAction(request.Type, request.Text, request.ActionDate);
            var (reminderDuration, reminderColor) = NormalizeReminder(request.ReminderDuration, request.ReminderColor);

            if (!existing.Add($"{type}|{text}"))
                continue;

            var action = new ExecutionAction
            {
                DocumentId = doc.Id,
                Type = type,
                Text = text,
                ActionDate = actionDate,
                ReminderDuration = reminderDuration,
                ReminderColor = reminderColor,
                CreatedById = userId.Value,
                CreatedAt = DateTime.UtcNow,
            };

            await _actions.AddAsync(action, ct);
            await _uow.SaveChangesAsync(ct);
            await _audit.LogAsync(actorName, "action", doc.Id, doc.DocumentType,
                AuditWithActor($"أضاف {TypeLabel(type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), ct);
        }
    }

    private (string Type, string Text, string? ActionDate) NormalizeAction(string type, string text, string? actionDate)
    {
        var sanitizedText = HtmlInputSanitizer.Sanitize(text);
        if (string.IsNullOrWhiteSpace(HtmlInputSanitizer.ToPlainText(sanitizedText)))
            throw new ArgumentException("نص الإجراء أو الملاحظة مطلوب");

        type = (type ?? "action").Trim();
        if (type is not ("action" or "note"))
            throw new ArgumentException("نوع غير صالح");

        var trimmedDate = actionDate?.Trim();

        if (type == "action")
        {
            if (string.IsNullOrWhiteSpace(trimmedDate))
                throw new ArgumentException("يجب إدخال تاريخ الإجراء");
        }
        else if (string.IsNullOrWhiteSpace(trimmedDate))
        {
            trimmedDate = ServerClock.TodayString(_clock, _timeZone, "yyyy-MM-dd");
        }

        return (type, sanitizedText, trimmedDate);
    }

    private static string TypeLabel(string type) => type == "note" ? "ملاحظة" : "إجراء";

    private static (string? Duration, string? Color) NormalizeReminder(string? duration, string? color)
    {
        var trimmedDuration = duration?.Trim();
        var trimmedColor = color?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedDuration) && string.IsNullOrWhiteSpace(trimmedColor))
            return (null, null);

        var validDurations = new[] { "3 أيام", "أسبوع", "أسبوعين", "شهر" };
        if (!string.IsNullOrWhiteSpace(trimmedDuration) && !validDurations.Contains(trimmedDuration))
            throw new ArgumentException("مدة تذكير غير صالحة");

        var validColors = new[] { "أحمر", "بنفسجي", "أصفر" };
        if (!string.IsNullOrWhiteSpace(trimmedColor) && !validColors.Contains(trimmedColor))
            throw new ArgumentException("لون تذكير غير صالح");

        return (trimmedDuration, trimmedColor);
    }

}
