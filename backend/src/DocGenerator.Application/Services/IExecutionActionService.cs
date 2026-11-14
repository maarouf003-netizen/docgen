using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// خدمة إجراءات التنفيذ والتذكيرات (مستخرجة من `DocumentService` — المرحلة 1):
/// دوال نقاط الإجراءات (قراءة/إضافة/تعديل/حذف) + مسارات التذكير الثلاثة المستقلة
/// (ضبط/إلغاء/نقل) + زرع «الإدخال المبدئي» داخل معاملة حفظ المستند.
/// الاتجاه الوحيد المسموح: النواة ← الإجراءات (النواة تستدعي الزرع عبر هذه الواجهة).
/// </summary>
public interface IExecutionActionService
{
    Task<List<ExecutionActionDto>> GetExecutionActionsAsync(int documentId, CancellationToken ct = default);

    Task<ExecutionActionDto> AddExecutionActionAsync(int documentId, AddExecutionActionRequest request, int userId, string? actorName, CancellationToken ct = default);

    Task<ExecutionActionDto?> UpdateExecutionActionAsync(int documentId, int actionId, UpdateExecutionActionRequest request, string? actorName, CancellationToken ct = default);

    Task<bool> DeleteExecutionActionAsync(int documentId, int actionId, string? actorName, CancellationToken ct = default);

    Task<bool> ClearReminderAsync(int documentId, int actionId, string? actorName, CancellationToken ct = default);

    /// <summary>
    /// إنشاء/تعديل تذكير إجراء قائم (مدة/لون) عبر مسار مستقل عن تعديل الإجراء:
    /// يهيّئ `ReminderBaseDate` من تاريخ الإجراء مرة واحدة فقط. الحقلان الفارغان = إلغاء.
    /// </summary>
    Task<ExecutionActionDto> SetReminderAsync(
        int documentId, int actionId, SetActionReminderRequest request, string? actorName, CancellationToken ct = default);

    /// <summary>
    /// نقل تذكير إجراء إلى يوم استحقاق جديد من نافذة اليوم: المدخل هو `dueDate`
    /// نفسه، وتُكتب `ReminderBaseDate = newDueDate − DurationDays` مع بقاء المدة
    /// وبقية الحقول دون مساس — تاريخ الإجراء لا يُكتب أبدًا. يرفض غياب التذكير
    /// والمدد الملتبسة برسائل ودية.
    /// </summary>
    Task<ExecutionActionDto> RescheduleReminderAsync(int documentId, int actionId, string? newDueDate, string? actorName, long? version = null, CancellationToken ct = default);

    /// <summary>
    /// زرع إجراءات/ملاحظات «الإدخال المبدئي» (`InitialActions`) ذرّيًا ضمن معاملة
    /// حفظ المستند نفسها — داخل معاملة المستدعي حصرًا (لا تفتح معاملة خاصة).
    /// </summary>
    Task SeedInitialActionsAsync(
        Document doc,
        List<AddExecutionActionRequest>? initialActions,
        int? userId,
        string? actorName,
        CancellationToken ct);
}
