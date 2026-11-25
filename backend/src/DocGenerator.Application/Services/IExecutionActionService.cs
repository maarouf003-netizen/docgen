using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// زرع إجراءات/ملاحظات «الإدخال المبدئي» داخل معاملة حفظ المستند، والمساعدات
/// المشتركة مع معالجات `P4-01` (تطبيع/إسقاط/تفريغ تذكير). الكتابة المستقلة عبر
/// الأوامر، والقراءة عبر `GetExecutionActionsQuery` (`P4-01ct`).
/// الاتجاه الوحيد المسموح: النواة ← الإجراءات (النواة تستدعي الزرع عبر هذه الواجهة).
/// </summary>
public interface IExecutionActionService
{
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
