namespace DocGenerator.Application.Services;

/// <summary>
/// حرّاس الوصول القرائي لخدمة الاستئنافات: هل المستخدم هو المحامي المسند إليه
/// متابعة استئناف على الملف؟ وهل للملف إحالة مفتوحة لرئيس القسم؟ تُستدعى من
/// مسارات قراءة أخرى داخل معاملاتها (صلاحيات المتحكم/المراسلات/الإحالة)، فتبقى
/// على الخدمة؛ أما أسطح القراءة/القوائم/العدّ/التاريخ فملكيتها لمعالجات الاستعلام
/// تحت <c>UseCases/Appeals</c>.
/// </summary>
public interface IDocumentAppealService
{
    /// <summary>كيان الاستئناف بروابطه (للتحقق من صلاحية العرض في المتحكم).</summary>
    Task<DocGenerator.Domain.Entities.DocumentAppeal?> GetEntityAsync(int appealId, CancellationToken ct = default);

    /// <summary>هل المستخدم هو المحامي المسند إليه متابعة استئناف على الملف؟ (وصول قرائي للملف الأساس).</summary>
    Task<bool> IsAssignedFollowerAsync(int documentId, int userId, CancellationToken ct = default);

    /// <summary>الاستثناء القرائي للإحالة (قرار §2.22): إحالة مفتوحة لرئيس القسم على الملف.</summary>
    Task<bool> HasForwardedAppealAsync(int documentId, CancellationToken ct = default);
}
