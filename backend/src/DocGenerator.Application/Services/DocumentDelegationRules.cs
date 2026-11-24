using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// قواعد الإنابة المشتركة بين الخدمة ومراحل `P4-01` — حرّاس الملكية/الحالة
/// المشتركة بين التعديل والحذف، فلا تنحرف رسالة أو ترتيب بين المسارين.
/// </summary>
internal static class DocumentDelegationRules
{
    /// <summary>
    /// فحص الملكية أولًا (403) قبل حالة الإنابة (400) — لا نكشف حالة إنابة ليست
    /// للمستخدم برسالة 400، ولا نخلط فشل الصلاحية بخطأ مدخلات (`P2-02e`).
    /// </summary>
    internal static void EnsurePendingByOwner(DocumentDelegation delegation, int userId)
    {
        if (delegation.SourceDocument.CreatedById != userId)
            throw new UnauthorizedAccessException("لا يمكنك تعديل أو حذف إنابة على ملف لا تملكه");
        if (delegation.Status != DelegationStatusCatalog.PendingHead)
            throw new ArgumentException("لا يمكن تعديل أو حذف إنابة لم تعد معلّقة");
    }
}
