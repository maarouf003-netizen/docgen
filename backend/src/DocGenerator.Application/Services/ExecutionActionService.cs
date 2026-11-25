using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// خدمة إجراءات التنفيذ (مستخرجة من خدمة المستندات القديمة — المرحلة 1، نقل ميكانيكي بلا سلوك):
/// بقيت فيها القراءة و`SeedInitialActionsAsync` التي تنضم لمعاملة حفظ المستند (داخل معاملة
/// المستدعي حصرًا) والمساعدات المشتركة مع معالجات `P4-01` (تطبيع/إسقاط/تفريغ تذكير).
/// كل نقاط الكتابة المستقلة مملوكة لأوامر: الإضافة/التعديل/الحذف/التذكيرات الثلاثة.
/// الاعتمادات: `_actions` و`_documents` و`_uow` و`_audit` والساعة/المنطقة فقط.
/// </summary>
public sealed class ExecutionActionService : IExecutionActionService
{
    private readonly IRepository<ExecutionAction> _actions;
    private readonly IDocumentRepository _documents;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public ExecutionActionService(
        IRepository<ExecutionAction> actions,
        IDocumentRepository documents,
        IUnitOfWork uow,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone)
    {
        _actions = actions;
        _documents = documents;
        _uow = uow;
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

    /// <summary>
    /// تفريغ حالة التذكير وتدوينه — التنفيذ الوحيد للإلغاء (يستدعيه مسار
    /// `PUT .../reminder` بالحقلين الفارغين ومعالج `ClearActionReminderCommand`)
    /// فلا تنحرف رسالة التدقيق بين المسارين. لا يفتح معاملة بنفسه: المستدعي يملكها
    /// (الخدمة عبر `_tx`، والمعالج عبر وسم `ITransactionalRequest`).
    /// </summary>
    internal static async Task ClearReminderWriteAsync(
        ExecutionAction action, Document doc, string? actorName,
        IRepository<ExecutionAction> actions, IUnitOfWork uow, IAuditLogger audit,
        CancellationToken ct)
    {
        ClearReminderFields(action);

        actions.Update(action);
        await uow.SaveChangesAsync(ct);
        await audit.LogAsync(actorName, "action", doc.Id, doc.DocumentType,
            DocumentText.AuditWithActor($"ألغى التذكير عن {DocumentText.TypeLabel(action.Type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), ct);
    }

    /// <summary>
    /// إنشاء/تعديل تذكير إجراء قائم (مدة/لون): تهيئة `ReminderBaseDate` من تاريخ الإجراء
    /// مرة واحدة فقط (`??=`) — بلا سقوط إلى `CreatedAt` كي لا يُجمَّد احتياط صامت؛ والصفوف
    /// القديمة غير القابلة للتحليل تبقى أساسها `null` فيُحسب الاستحقاق احتياطيًا **موسومًا**.
    /// إرسال الحقلين فارغين = إلغاء التذكير (نقطة قرار واحدة مع `ClearReminderAsync`).
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

            var (type, text, actionDate) = NormalizeAction(request.Type, request.Text, request.ActionDate, _clock, _timeZone);
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

    internal static (string Type, string Text, string? ActionDate) NormalizeAction(
        string type, string text, string? actionDate, TimeProvider clock, TimeZoneInfo timeZone)
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
            trimmedDate = ServerClock.TodayString(clock, timeZone, "yyyy-MM-dd");
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
    internal static ExecutionActionDto ToDto(ExecutionAction action, string? createdByName)
        => new(action.Id, action.Type, action.Text, action.ActionDate,
            action.ReminderDuration, action.ReminderColor, createdByName, action.CreatedAt, action.Version);

    internal static (string? Duration, string? Color) NormalizeReminder(string? duration, string? color)
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
