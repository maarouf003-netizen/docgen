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


    public async Task<DocumentOccurrenceDto> AddOccurrenceAsync(int documentId, UpsertOccurrenceRequest request, int userId, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            throw new KeyNotFoundException();

        var occurrence = CreateOccurrence(documentId, request, userId);
        await _tx.RunAsync(async token =>
        {
            await _occurrences.AddAsync(occurrence, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "occurrence", doc.Id, doc.DocumentType,
                AuditWithActor($"أضاف وقعة {ToOccurrenceLabel(occurrence)}: {ToOccurrenceSummary(occurrence)}", doc), token);
        }, ct);
        return ToDto(occurrence, actorName);
    }

    public async Task<DocumentOccurrenceDto?> UpdateOccurrenceAsync(int documentId, int occurrenceId, UpsertOccurrenceRequest request, string? actorName, CancellationToken ct = default)
    {
        var occurrence = await _occurrences.GetByIdAsync(occurrenceId, ct);
        if (occurrence is null || occurrence.DocumentId != documentId)
            return null;

        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return null;

        ApplyOccurrence(occurrence, request);
        occurrence.UpdatedAt = DateTime.UtcNow;

        await _tx.RunAsync(async token =>
        {
            _occurrences.Update(occurrence);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "occurrence", documentId, doc.DocumentType,
                AuditWithActor($"عدّل وقعة {ToOccurrenceLabel(occurrence)}: {ToOccurrenceSummary(occurrence)}", doc), token);
        }, ct);
        return ToDto(occurrence, actorName);
    }

    public async Task<bool> DeleteOccurrenceAsync(int documentId, int occurrenceId, string? actorName, CancellationToken ct = default)
    {
        var occurrence = await _occurrences.GetByIdAsync(occurrenceId, ct);
        if (occurrence is null || occurrence.DocumentId != documentId)
            return false;

        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;

        return await _tx.RunAsync(async token =>
        {
            _occurrences.Remove(occurrence);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "occurrence", documentId, doc.DocumentType,
                AuditWithActor($"حذف وقعة {ToOccurrenceLabel(occurrence)}: {ToOccurrenceSummary(occurrence)}", doc), token);
            return true;
        }, ct);
    }

    /// <summary>
    /// إنشاء كيان الوقعة مع التحقق الكامل من النوع والحقول: النوع يجب أن يكون ضمن
    /// OccurrenceTypeCatalog، والتواريخ نصوص حرة تُفسَّر زمنيًا، ورقم الملف الجديد إلزامي
    /// لوقعة التجديد، وجميع الحقول مقيدة بأطوالها القصوى.
    /// </summary>
    private static DocumentOccurrence CreateOccurrence(int documentId, UpsertOccurrenceRequest request, int userId)
    {
        var occurrence = new DocumentOccurrence
        {
            DocumentId = documentId,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        ApplyOccurrence(occurrence, request);
        return occurrence;
    }

    /// <summary>
    /// تطبيق حقول الطلب على كيان الوقعة مع التحقق (يُعارض ApplyOccurrence لكل من
    /// الإضافة والتعديل فيتوحد سلوك التحقق ويمنع التكرار).
    /// </summary>
    private static void ApplyOccurrence(DocumentOccurrence occurrence, UpsertOccurrenceRequest request)
    {
        var type = (request.OccurrenceType ?? string.Empty).Trim();
        if (!OccurrenceTypeCatalog.ValidTypes.Contains(type))
            throw new ArgumentException("نوع وقعة غير صالح");

        // وقوعات تغيير الحالة (نظام «طالبة تنفيذ»): تُحفظ حقولها التفصيلية كما وردت مع
        // التحقق من الحقول الإلزامية لكل نوع (تريث/منفذ بالتسوية/منفذ جبريا/تراجع).
        if (OccurrenceTypeCatalog.IsStatusChange(type))
        {
            var details = NormalizeDetails(request.Details);
            switch (type)
            {
                case OccurrenceTypeCatalog.Deferred:
                    RequireDetail(details, "tarithNumber", "رقم كتاب التريث");
                    RequireDetail(details, "tarithDate", "تاريخ كتاب التريث");
                    break;
                case OccurrenceTypeCatalog.Settled:
                    RequireDetail(details, "baraetNumber", "رقم كتاب براءة الذمة");
                    RequireDetail(details, "baraetDate", "تاريخ كتاب براءة الذمة");
                    break;
                case OccurrenceTypeCatalog.Forcible:
                    RequireDetail(details, "execSubStatus", "نوع التنفيذ الفرعي");
                    break;
                case OccurrenceTypeCatalog.Revert:
                    RequireDetail(details, "sayerNumber", "رقم كتاب الجهة العامة بالسير بالملف");
                    RequireDetail(details, "sayerDate", "تاريخ كتاب الجهة العامة بالسير بالملف");
                    RequireDetail(details, "sayerRegNumber", "رقم ورود كتاب بالسير بالملف");
                    RequireDetail(details, "sayerRegDate", "تاريخ ورود كتاب بالسير بالملف");
                    break;
            }
            occurrence.OccurrenceType = type;
            occurrence.EventDate = DocumentValidator.ParseDateTime(request.EventDate, "تاريخ الوقعة");
            occurrence.FileNumber = null;
            occurrence.FileType = null;
            occurrence.Year = null;
            occurrence.ReceiptNumber = null;
            occurrence.ReceiptDate = null;
            occurrence.Details = details.Count > 0 ? SerializeDetails(details) : null;
            return;
        }

        var number = (request.FileNumber ?? string.Empty).Trim();
        if (number.Length > 100)
            throw new ArgumentException("رقم الملف يتجاوز الطول المسموح");
        if (OccurrenceTypeCatalog.IsRenewal(type) && string.IsNullOrEmpty(number))
            throw new ArgumentException("رقم الملف الجديد مطلوب لوقعة التجديد");

        var fileType = (request.FileType ?? string.Empty).Trim();
        if (fileType.Length > 100)
            throw new ArgumentException("نوع الملف يتجاوز الطول المسموح");

        var receiptNumber = (request.ReceiptNumber ?? string.Empty).Trim();
        if (receiptNumber.Length > 200)
            throw new ArgumentException("رقم ورود اخطار التجديد يتجاوز الطول المسموح");

        if (request.Year is not null && (request.Year < 1900 || request.Year > 2100))
            throw new ArgumentException("سنة الوقعة غير صالحة");

        occurrence.OccurrenceType = type;
        occurrence.EventDate = DocumentValidator.ParseDateTime(request.EventDate,
            type == OccurrenceTypeCatalog.Renewal ? "تاريخ التجديد" : "تاريخ الشطب");
        occurrence.FileNumber = string.IsNullOrEmpty(number) ? null : number;
        occurrence.FileType = string.IsNullOrEmpty(fileType) ? null : fileType;
        occurrence.Year = request.Year;
        occurrence.ReceiptNumber = string.IsNullOrEmpty(receiptNumber) ? null : receiptNumber;
        occurrence.ReceiptDate = DocumentValidator.ParseDateTime(request.ReceiptDate, "تاريخ ورود اخطار التجديد");
        occurrence.Details = null;
    }

    /// <summary>تطبيع حقول الوقعة التفصيلية: تجاهل الفارغ وضبط القيم المخزنة.</summary>
    private static Dictionary<string, string> NormalizeDetails(Dictionary<string, string?>? raw)
    {
        var result = new Dictionary<string, string>();
        if (raw is null)
            return result;
        foreach (var (key, value) in raw)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;
            result[key] = value.Trim();
        }
        return result;
    }

    private static void RequireDetail(Dictionary<string, string> details, string key, string label)
    {
        if (!details.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"يجب إدخال {label} على الأقل");
    }

    private static string ToOccurrenceLabel(DocumentOccurrence occurrence) =>
        OccurrenceTypeCatalog.ToLabel(occurrence.OccurrenceType);

    /// <summary>
    /// ملخص مختصر للوقعة في سجل التدقيق: تاريخ الشطب/التجديد والرقم المعني بها.
    /// </summary>
    private static string ToOccurrenceSummary(DocumentOccurrence occurrence)
    {
        var date = occurrence.EventDate?.ToString("d/M/yyyy");
        return string.Concat(date, string.IsNullOrWhiteSpace(occurrence.FileNumber) ? string.Empty : $" — رقم: {occurrence.FileNumber}");
    }

    private static DocumentOccurrenceDto ToDto(DocumentOccurrence o, string? createdByName = null) =>
        new(o.Id, o.OccurrenceType, OccurrenceTypeCatalog.ToLabel(o.OccurrenceType), o.EventDate,
            o.FileNumber, o.FileType, o.Year, o.ReceiptNumber, o.ReceiptDate,
            ParseOccurrenceDetails(o.Details), createdByName);

    /// <summary>فكّ حقول الوقعة التفصيلية من JSON المخزن (أو null عند غيابها/عطبها).</summary>
    private static IReadOnlyDictionary<string, string>? ParseOccurrenceDetails(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
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

    private static (string Type, string Text, string? ActionDate) NormalizeAction(string type, string text, string? actionDate)
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
            trimmedDate = DateTime.Today.ToString("yyyy-MM-dd");
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
