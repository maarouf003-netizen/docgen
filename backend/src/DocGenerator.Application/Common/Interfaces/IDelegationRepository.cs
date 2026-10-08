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
    /// طلبات الإنابة المعلّقة (بانتظار الاعتماد) لنطاق رئيسٍ معيّن (§7.1 + §7.4):
    /// داخلية بدائرة لرئيس شعبته (أو قسمه إن بلا شعبة)، وداخلية بلا دائرة
    /// لرئيس قسم فرع المنيب، وخارجية لرئيس قسم الفرع المناب ما لم تُوجَّه لشعبة.
    /// `rejectedOnly` لفلتر «مرفوض بانتظار التصحيح» — لنافذة
    /// «طلبات الإنابة والاستئنافات والمطالعات».
    /// </summary>
    Task<List<DocumentDelegation>> ListPendingByBranchAsync(int branchId, int? ownerSectionId = null, bool rejectedOnly = false, CancellationToken ct = default);

    /// <summary>
    /// عدد طلبات الإنابة المعلّقة لنطاق الرئيس — نفس نطاق <see cref="ListPendingByBranchAsync"/>
    /// دون تحميل القوائم المرتبطة: لشارة الرئيس الخفيفة (استطلاع دوري).
    /// </summary>
    Task<int> CountPendingByBranchAsync(int branchId, int? ownerSectionId = null, bool rejectedOnly = false, CancellationToken ct = default);

    /// <summary>
    /// الإنابات غير المنفذة لملفٍ منيبٍ معيّن مع جميع مجموعات الملف المناب المحلية
    /// (الكفلاء/الورثة/الجهات طالبة التنفيذ/تاريخ القيد) — تُغذّي مزامنة «الملف المناب مرآةً
    /// للمنيب» دون N+1. الإنابات المنفذة سجل نهائي فلا تُمسّ نسخها.
    /// </summary>
    Task<List<DocumentDelegation>> ListPendingBySourceWithTargetsAsync(int sourceDocumentId, CancellationToken ct = default);

    /// <summary>إنابات تستهدف دائرة (DelegatedCircuitId) — لإعادة التسمية/التوجيه الجماعي.</summary>
    Task<List<DocumentDelegation>> ListByDelegatedCircuitAsync(int circuitId, CancellationToken ct = default);

    /// <summary>
    /// إنابات تستهدف دائرة متجاوزةً فلتر المصدر المحذوف (C1) — لمسار الحذف فقط:
    /// الفلتر العام يُخفي إنابات مصادرها محذوفة، لكن قيد FK ما زال يراها فينفجر
    /// الحذف 500 إن لم تُفك. التسمية تبقي الافتراضي (لا أثر مرئيًا هناك).
    /// </summary>
    Task<List<DocumentDelegation>> ListByDelegatedCircuitIncludingDeletedAsync(int circuitId, CancellationToken ct = default);

    /// <summary>عدد الإنابات الواردة المعلقة التي تستهدف الدائرة — حارس الحذف الموسع.</summary>
    Task<int> CountPendingIncomingByCircuitAsync(int circuitId, CancellationToken ct = default);

    /// <summary>
    /// عدد الإنابات المعلقة متجاوزًا فلتر المصدر المحذوف (C1) — لحارس الحذف: المعلقة
    /// تحظر الحذف أيًا كان حال مصدرها (صفها القاعدي ما زال يشير للدائرة).
    /// </summary>
    Task<int> CountPendingIncomingByCircuitIncludingDeletedAsync(int circuitId, CancellationToken ct = default);
}
