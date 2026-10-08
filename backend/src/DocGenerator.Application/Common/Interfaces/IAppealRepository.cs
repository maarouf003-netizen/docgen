using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// استعلامات الاستئنافات (DocumentAppeal) على مستوى قاعدة البيانات: جلب سجلٍ مع روابطه،
/// وقوائمه بحسب صاحب الرؤية (المحامي المسند إليه / فرع رئيس القسم)،
/// وبحث نصي في لقطات الأطراف وأرقام الأساس الاستئنافية.
/// </summary>
public interface IAppealRepository : IRepository<DocumentAppeal>
{
    /// <summary>استئناف بمعرفه مع كامل روابطه (الملف، المحامي المسند، المنشئ، الإجراءات، أرقام الأساس).</summary>
    Task<DocumentAppeal?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);

    /// <summary>استئنافات ملف معين (بطاقة «الاستئنافات» في وقوعات الملف)، الأحدث أولًا.</summary>
    Task<List<DocumentAppeal>> ListByDocumentAsync(int documentId, CancellationToken ct = default);

    /// <summary>
    /// كل الاستئنافات المرتبطة بمجموعة ملفات (تُستخدم لمزامنة لقطات الأطراف عند تغيير
    /// جهة عامة). استعلام خفيف (بلا روابط التحميل المسبق) ضمن التتبّع لتُحفظ تحديثاته.
    /// </summary>
    Task<List<DocumentAppeal>> ListByDocumentIdsAsync(
        IReadOnlyCollection<int> documentIds, CancellationToken ct = default);

    /// <summary>
    /// بحث/قائمة الاستئنافات لنطاق رؤية محدد: المحامي (استئنافاته المسندة إليه
    /// للمتابعة فقط)، رئيس القسم (دوائر القسم وبلا دائرة + المحال له)، رئيس
    /// الشعبة (دوائر شعبته)، الإدارة (الكل). البحث النصي يطابق أسماء
    /// المستأنف/المستأنف عليهم من اللقطات ورقم الأساس الاستئنافي والمحكمة.
    /// </summary>
    Task<(int Total, List<DocumentAppeal> Items)> SearchAsync(
        string? query,
        string? status,
        int? visibleBranchId,
        int? visibleUserId,
        int? ownerSectionId,
        int page,
        int perPage,
        CancellationToken ct = default);

    /// <summary>
    /// الاستثناء القرائي للإحالة (قرار §2.22): هل على الملف إحالة مفتوحة لرئيس
    /// القسم (`ForwardedToHead`) — تُرى حتى الحسم.
    /// </summary>
    Task<bool> HasForwardedAppealAsync(int documentId, CancellationToken ct = default);

    /// <summary>هل المستخدم هو المحامي المسند إليه متابعة استئناف على الملف المحدد؟</summary>
    Task<bool> IsAssignedFollowerAsync(int documentId, int userId, CancellationToken ct = default);

    /// <summary>خريطة معرفات الملفات التي لديها استئناف منظور واحد على الأقل ← معرف أول استئناف منظور لها (الأقدم). المحسوم والمشطوب لا يُظهران شارة القائمة.</summary>
    Task<Dictionary<int, int>> MapFirstAppealIdByDocumentIdsAsync(
        IReadOnlyCollection<int> documentIds, CancellationToken ct = default);

    /// <summary>
    /// كل استئنافات محامٍ المسندة إليه للمتابعة (واختياريًا ضمن فرع محدد)، بلا ترقيم —
    /// لنقل الاستئنافات جملةً وتذكيرات الإجراءات. التتبّع مطلوب عند القصد للتحديث
    /// (transfer-all) لتفادي تعارض تتبّع الكيان نفسه في السياق الواحد.
    /// </summary>
    Task<List<DocumentAppeal>> ListByAssigneeAsync(
        int assigneeId, int? branchId = null, bool asNoTracking = true, CancellationToken ct = default);

    /// <summary>عدد استئنافات محامٍ المسندة إليه (واختياريًا ضمن فرع محدد وحالة محددة) — لمعاينة النقل الجملة (المنظورة فقط).</summary>
    Task<int> CountByAssigneeAsync(int assigneeId, int? branchId = null, string? status = null, CancellationToken ct = default);

    /// <summary>
    /// الاستئنافات المنظورة المسندة لمحامٍ ضمن نطاق المنفِّذ (§5.5: تقاطع
    /// المصدر مع ملكية الدائرة) — فلترة قاعدية بلا تحميل التفاصيل، بتتبّع
    /// لأن القصد تحديثها (النقل الجملة). المحسوم/المشطوب وخارج النطاق مستبعدان هنا.
    /// </summary>
    Task<List<DocumentAppeal>> ListPendingByAssigneeInScopeAsync(
        int assigneeId, int branchId, int? ownerSectionId, CancellationToken ct = default);

    /// <summary>
    /// عدد الاستئنافات المنظورة المسندة لمحامٍ ضمن نطاق المنفِّذ (§5.5) —
    /// معاينة النقل الجملة، تطابق المنقول فعلًا. عدّ قاعدي بلا جلب.
    /// </summary>
    Task<int> CountPendingByAssigneeInScopeAsync(
        int assigneeId, int branchId, int? ownerSectionId, CancellationToken ct = default);
}
