using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// قواعد الاستئناف المشتركة بين الخدمة ومراحل `P4-01` — حرّاس الصلاحية/الحالة
/// المشتركة بين التعديل والحذف، فلا تنحرف رسالة أو ترتيب بين المسارين.
/// </summary>
internal static class DocumentAppealRules
{
    /// <summary>
    /// الصلاحية أولًا (403)، ثم حالة الاستئناف (400) — لا كشف لحالة استئناف الغير
    /// (`P2-02f`).
    /// </summary>
    internal static void EnsurePendingByCreator(DocumentAppeal appeal, int userId)
    {
        if (appeal.CreatedById != userId)
            throw new UnauthorizedAccessException("لا يمكنك تعديل أو حذف استئناف لم تسطّره");
        if (appeal.AssignedLawyerId is not null || appeal.Status != AppealStatusCatalog.Pending)
            throw new ArgumentException("لا يمكن تعديل أو حذف استئناف بعد إسناده");
    }
}
