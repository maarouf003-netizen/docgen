using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// خدمة إجراءات التنفيذ والتذكيرات (مستخرجة من `DocumentService` — المرحلة 1، نقل ميكانيكي
/// بلا سلوك): كل نقطة دخول عامة تفتح معاملتها الخاصة عبر `_tx.RunAsync` — باستثناء
/// `SeedInitialActionsAsync` التي تنضم لمعاملة حفظ المستند (داخل معاملة المستدعي حصرًا).
/// الاعتمادات: `_actions` و`_documents` و`_uow` و`_tx` و`_audit` والساعة/المنطقة فقط.
/// </summary>
public sealed class ExecutionActionService : IExecutionActionService
{
    private readonly IRepository<ExecutionAction> _actions;
    private readonly IDocumentRepository _documents;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public ExecutionActionService(
        IRepository<ExecutionAction> actions,
        IDocumentRepository documents,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone)
    {
        _actions = actions;
        _documents = documents;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
    }

    public async Task<List<ExecutionActionDto>> GetExecutionActionsAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return new List<ExecutionActionDto>();
        return doc.ExecutionActions
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => ToDto(a, a.CreatedBy?.FullName))
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
            // تاريخ الإجراء محلولًا للاستعلامات النطاقية (تذكيرات الشطب) — يُكتب هنا فقط.
            ActionDateParsed = ActionDateParser.TryParse(actionDate),
            ReminderDuration = reminderDuration,
            ReminderColor = reminderColor,
            // تاريخ أساس التذكير يُثبَّت مرة واحدة عند الإنشاء من تاريخ الإجراء المحلول —
            // ولا يُشتق لاحقًا من نص الإجراء ولا يُكتب فيه أي مسار تذكير.
            ReminderBaseDate = ActionReminderCalculator.BaseDateForNewReminder(reminderDuration, reminderColor, actionDate),
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
        };

        await _tx.RunAsync(async token =>
        {
            await _actions.AddAsync(action, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "action", doc.Id, doc.DocumentType,
                DocumentText.AuditWithActor($"أضاف {DocumentText.TypeLabel(type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), token);
        }, ct);
        return ToDto(action, actorName);
    }

    public async Task<ExecutionActionDto?> UpdateExecutionActionAsync(int documentId, int actionId, UpdateExecutionActionRequest request, string? actorName, CancellationToken ct = default)
    {
        // تعديل الإجراء (نوع/نص/تاريخ) لا يكتب أي حقل تذكير: حالة التذكير تُدار من
        // `SetReminderAsync` و«نقل التذكير» حصرًا، فلا يتغيّر `ReminderBaseDate` هنا أبدًا.
        var (type, text, actionDate) = NormalizeAction(request.Type, request.Text, request.ActionDate);

        var action = await _actions.GetByIdAsync(actionId, ct);
        if (action is null || action.DocumentId != documentId)
            throw new KeyNotFoundException();

        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            throw new KeyNotFoundException();

        action.Type = type;
        action.Text = text;
        action.ActionDate = actionDate;
        // التاريخ المحلول يتبع نص الإجراء المُدخل (لا يمسّه أي مسار تذكير).
        action.ActionDateParsed = ActionDateParser.TryParse(actionDate);

        await _tx.RunAsync(async token =>
        {
            _actions.Update(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "action", documentId, doc.DocumentType,
                DocumentText.AuditWithActor($"عدّل {DocumentText.TypeLabel(type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), token);
        }, ct);
        return ToDto(action, actorName);
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
                DocumentText.AuditWithActor($"حذف {DocumentText.TypeLabel(action.Type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), token);
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

        await ClearReminderCoreAsync(action, doc, actorName, ct);
        return true;
    }

    /// <summary>
    /// تفريغ حالة التذكير وتدوينه — التنفيذ الوحيد للإلغاء (يستدعيه `ClearReminderAsync`،
    /// ومسار `PUT .../reminder` بالحقلين الفارغين) فلا تنحرف رسالة التدقيق بين المسارين.
    /// يستقبل الكيان المحمَّل والمستند تفاديًا لإعادة القراءة.
    /// </summary>
    private async Task ClearReminderCoreAsync(
        ExecutionAction action, Document doc, string? actorName, CancellationToken ct)
    {
        ClearReminderFields(action);

        await _tx.RunAsync(async token =>
        {
            _actions.Update(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "action", doc.Id, doc.DocumentType,
                DocumentText.AuditWithActor($"ألغى التذكير عن {DocumentText.TypeLabel(action.Type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), token);
        }, ct);
    }

    /// <summary>
    /// إنشاء/تعديل تذكير إجراء قائم (مدة/لون): تهيئة `ReminderBaseDate` من تاريخ الإجراء
    /// مرة واحدة فقط (`??=`) — بلا سقوط إلى `CreatedAt` كي لا يُجمَّد احتياط صامت؛ والصفوف
    /// القديمة غير القابلة للتحليل تبقى أساسها `null` فيُحسب الاستحقاق احتياطيًا **موسومًا**.
    /// إرسال الحقلين فارغين = إلغاء التذكير (نقطة قرار واحدة مع `ClearReminderAsync`).
    /// </summary>
    public async Task<ExecutionActionDto> SetReminderAsync(
        int documentId, int actionId, SetActionReminderRequest request, string? actorName, CancellationToken ct = default)
    {
        var action = await _actions.GetByIdAsync(actionId, ct);
        if (action is null || action.DocumentId != documentId)
            throw new KeyNotFoundException();

        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            throw new KeyNotFoundException();

        var (reminderDuration, reminderColor) = NormalizeReminder(request.ReminderDuration, request.ReminderColor);

        if (reminderDuration is null && reminderColor is null)
        {
            await ClearReminderCoreAsync(action, doc, actorName, ct);
            return ToDto(action, actorName);
        }

        action.ReminderDuration = reminderDuration;
        action.ReminderColor = reminderColor;
        action.ReminderBaseDate ??= ActionDateParser.TryParse(action.ActionDate);

        await _tx.RunAsync(async token =>
        {
            _actions.Update(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "action", documentId, doc.DocumentType,
                DocumentText.AuditWithActor(
                    $"ضبط تذكير {DocumentText.TypeLabel(action.Type)}: {HtmlInputSanitizer.ToPlainText(action.Text)} ({reminderDuration ?? "—"} — {reminderColor ?? "—"})",
                    doc), token);
        }, ct);

        return ToDto(action, actorName);
    }

    /// <summary>
    /// نقل تذكير إجراء إلى يوم استحقاق جديد من نافذة اليوم: المدخل (`newDueDate`) هو يوم
    /// الاستحقاق نفسه لا تاريخ الإجراء؛ وتُكتب `ReminderBaseDate = newDueDate − DurationDays`
    /// مع بقاء المدة وبقية الحقول دون مساس. **تاريخ الإجراء لا يُكتب ولا يُعاد تنسيقه** —
    /// واقعة قانونية (I1) — وكل حسابات التذكير على `DateTime` لا على نص (I7).
    /// </summary>
    public async Task<ExecutionActionDto> RescheduleReminderAsync(int documentId, int actionId, string? newDueDate, string? actorName, CancellationToken ct = default)
    {
        var action = await _actions.GetByIdAsync(actionId, ct);
        if (action is null || action.DocumentId != documentId)
            throw new KeyNotFoundException();

        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            throw new KeyNotFoundException();

        if (string.IsNullOrWhiteSpace(newDueDate))
            throw new ArgumentException("اليوم الجديد مطلوب — استخدم مثال: 1/8/2026");

        var due = DocumentValidator.ParseDateTime(newDueDate.Trim(), "اليوم الجديد")
            ?? throw new ArgumentException("اليوم الجديد مطلوب — استخدم مثال: 1/8/2026");

        if (string.IsNullOrWhiteSpace(action.ReminderDuration)
            && string.IsNullOrWhiteSpace(action.ReminderColor)
            && action.ReminderBaseDate is null)
            throw new ArgumentException("لا يوجد تذكير لإعادة جدولته");

        // حارس المدة الملتبسة (بيانات قديمة): مدة غير فارغة خارج المدد الأربع كانت
        // تُحسب صفرًا بصمت فيُسكت وسم `dueDateSuspect` — هنا تُرفض برسالة توجه
        // لنافذة الإجراءات بدل إصلاح صامت.
        var trimmedDuration = action.ReminderDuration?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedDuration) && !ActionReminderCalculator.ValidDurations.Contains(trimmedDuration))
            throw new ArgumentException("مدة التذكير غير معروفة — عدّل الإجراء من نافذة الإجراءات");

        // الإسناد إلى تاريخ أساس التذكير وحده (`DateTime` خالص): تاريخ الإجراء واقعة
        // قانونية لا تُعاد كتابتها، ولا يُعاد تنسيق نصها — I1/I7.
        var oldBaseDate = action.ReminderBaseDate;
        action.ReminderBaseDate = due.AddDays(-ActionReminderCalculator.DurationDays(trimmedDuration));

        await _tx.RunAsync(async token =>
        {
            _actions.Update(action);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "action", documentId, doc.DocumentType,
                DocumentText.AuditWithActor(
                    $"نقل التذكير إلى {due:yyyy-MM-dd} (تاريخ الأساس: {oldBaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—"} ← {action.ReminderBaseDate:yyyy-MM-dd}) — تاريخ الإجراء لم يتغير",
                    doc), token);
        }, ct);
        return ToDto(action, actorName);
    }

    /// <summary>
    /// يزرع إجراءات/ملاحظات «الإدخال المبدئي» (InitialActions) في جدول الإجراءات والملاحظات
    /// ذرّيًا ضمن معاملة حفظ المستند نفسها — داخل معاملة المستدعي حصرًا (لا تفتح معاملة
    /// خاصة). كل عنصر يمر بـ NormalizeAction/NormalizeReminder فنُرفض الطلبات الخبيثة
    /// أو الناقصة ذرّيًا، وتُتجاهل الحقول الفارغة، ولا يُنشأ أي سجل مكرر (لا مقابل
    /// سجلات الملف القائمة ولا بين عناصر الطلب نفسه).
    /// </summary>
    public async Task SeedInitialActionsAsync(
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
                ActionDateParsed = ActionDateParser.TryParse(actionDate),
                ReminderDuration = reminderDuration,
                ReminderColor = reminderColor,
                ReminderBaseDate = ActionReminderCalculator.BaseDateForNewReminder(reminderDuration, reminderColor, actionDate),
                CreatedById = userId.Value,
                CreatedAt = DateTime.UtcNow,
            };

            await _actions.AddAsync(action, ct);
            await _uow.SaveChangesAsync(ct);
            await _audit.LogAsync(actorName, "action", doc.Id, doc.DocumentType,
                DocumentText.AuditWithActor($"أضاف {DocumentText.TypeLabel(type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), ct);
        }
    }

    private (string Type, string Text, string? ActionDate) NormalizeAction(string type, string text, string? actionDate)
    {
        var sanitizedText = HtmlInputSanitizer.Sanitize(text);
        if (string.IsNullOrWhiteSpace(HtmlInputSanitizer.ToPlainText(sanitizedText)))
            throw new ArgumentException("نص الإجراء أو الملاحظة مطلوب");

        type = (type ?? "action").Trim();
        ActionReminderCalculator.ValidateActionType(type, "نوع");

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

        // رفض الكتابة الميتة: تاريخ غير فارغ لا يُحلَّل كان يُخزَّن نصًا ثم يسقط بصمت إلى
        // تاريخ الإنشاء في حساب التذكير — الآن يُرفض برسالة الحقل (الفارغ/الافتراضي مباح).
        ActionReminderCalculator.ValidateActionDate(trimmedDate, "تاريخ الإجراء");

        return (type, sanitizedText, trimmedDate);
    }

    /// <summary>
    /// تفريغ حالة التذكير كلها (المدة/اللون/الأساس) — نقطة واحدة يستخدمها الإلغاء المباشر
    /// والـ`PUT` بالحقلين الفارغين، فلا ينحرف أحد المسارين عن الآخر.
    /// </summary>
    private static void ClearReminderFields(ExecutionAction action)
    {
        action.ReminderDuration = null;
        action.ReminderColor = null;
        action.ReminderBaseDate = null;
    }

    /// <summary>
    /// تحويل إجراء إلى DTO موحّد (اسم العرض: منشئ السجل للقراءة، والفاعل للعمليات الجارية) —
    /// نقطة واحدة فلا تنحرف الحقول بين المسارات الستة.
    /// </summary>
    private static ExecutionActionDto ToDto(ExecutionAction action, string? createdByName)
        => new(action.Id, action.Type, action.Text, action.ActionDate,
            action.ReminderDuration, action.ReminderColor, createdByName, action.CreatedAt);

    private static (string? Duration, string? Color) NormalizeReminder(string? duration, string? color)
    {
        var trimmedDuration = duration?.Trim();
        var trimmedColor = color?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedDuration) && string.IsNullOrWhiteSpace(trimmedColor))
            return (null, null);

        // المصدر الوحيد للحقيقة (ActionReminderCalculator) — لا مجموعات حرفيات مكررة.
        ActionReminderCalculator.ValidateReminder(trimmedDuration, trimmedColor);

        return (trimmedDuration, trimmedColor);
    }
}
