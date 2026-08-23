using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// استعلامات الاستئنافات (DocumentAppeal) على مستوى قاعدة البيانات: جلب سجلٍ مع روابطه،
/// وقوائمه بحسب صاحب الرؤية (المنشئ / المحامي المسند إليه / فرع رئيس القسم)،
/// وبحث نصي في لقطات الأطراف وأرقام الأساس الاستئنافية.
/// </summary>
public interface IAppealRepository : IRepository<DocumentAppeal>
{
    /// <summary>استئناف بمعرفه مع كامل روابطه (الملف، المحامي المسند، المنشئ، الإجراءات، أرقام الأساس).</summary>
    Task<DocumentAppeal?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);

    /// <summary>استئنافات ملف معين (بطاقة «الاستئنافات» في وقوعات الملف)، الأحدث أولًا.</summary>
    Task<List<DocumentAppeal>> ListByDocumentAsync(int documentId, CancellationToken ct = default);

    /// <summary>
    /// بحث/قائمة الاستئنافات لنطاق رؤية محدد: المحامي (استئنافاته المنشأة أو المسندة إليه)،
    /// رئيس القسم (فرعه)، الإدارة (الكل). البحث النصي يطابق أسماء المستأنف/المستأنف عليهم
    /// من اللقطات ورقم الأساس الاستئنافي والمحكمة.
    /// </summary>
    Task<(int Total, List<DocumentAppeal> Items)> SearchAsync(
        string? query,
        string? status,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
        CancellationToken ct = default);

    /// <summary>هل المستخدم هو المحامي المسند إليه متابعة استئناف على الملف المحدد؟</summary>
    Task<bool> IsAssignedFollowerAsync(int documentId, int userId, CancellationToken ct = default);

    /// <summary>خريطة معرفات الملفات التي لديها استئناف واحد على الأقل ← معرف أول استئناف لها.</summary>
    Task<Dictionary<int, int>> MapFirstAppealIdByDocumentIdsAsync(
        IReadOnlyCollection<int> documentIds, CancellationToken ct = default);

    /// <summary>
    /// كل استئنافات محامٍ المسندة إليه للمتابعة (واختياريًا ضمن فرع محدد)، بلا ترقيم —
    /// لنقل الاستئنافات جملةً وتذكيرات الإجراءات. التتبّع مطلوب عند القصد للتحديث
    /// (transfer-all) لتفادي تعارض تتبّع الكيان نفسه في السياق الواحد.
    /// </summary>
    Task<List<DocumentAppeal>> ListByAssigneeAsync(
        int assigneeId, int? branchId = null, bool asNoTracking = true, CancellationToken ct = default);

    /// <summary>عدد استئنافات محامٍ المسندة إليه (واختياريًا ضمن فرع محدد) — لمعاينة النقل الجملة.</summary>
    Task<int> CountByAssigneeAsync(int assigneeId, int? branchId = null, CancellationToken ct = default);
}
