using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// استعلامات الإنابات (DocumentDelegation): جلب سجلٍ مع كامل روابطه (المصدر/المناب/الأصول/الفرع/
/// المحامي)، وقوائم الإنابات بحسب صاحب الرؤية (المنيب / الملف المناب / رئيس القسم).
/// </summary>
public interface IDelegationRepository : IRepository<DocumentDelegation>
{
    /// <summary>إنابة بمعرفها مع كامل روابطها (المصدر، المناب، الأصول، الفرع الخارجي، المحامي، المنشئ).</summary>
    Task<DocumentDelegation?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);

    /// <summary>إنابات ملف معين (المنيب: المصدر؛ أو المناب: جُلبت من الملف المناب نفسه).</summary>
    Task<List<DocumentDelegation>> ListBySourceAsync(int sourceDocumentId, CancellationToken ct = default);

    /// <summary>
    /// إنابات الملف المناب (TargetDocument) لمحتوى «معلومات الملف المنيب»: تُرجع الإنابة
    /// التي نشأ عنها الملف، أو null إن لم يكن ملفًا منابًا.
    /// </summary>
    Task<DocumentDelegation?> FindByTargetAsync(int targetDocumentId, CancellationToken ct = default);

    /// <summary>
    /// طلبات الإنابة المعلّقة (بانتظار رئيس القسم) لفرعٍ معيّن: إنابات ملفات ذلك الفرع،
    /// مع بيانات المصدر والمنشئ — لنافذة «طلبات الإنابة والاستئنافات والمطالعات».
    /// </summary>
    Task<List<DocumentDelegation>> ListPendingByBranchAsync(int branchId, CancellationToken ct = default);
}
