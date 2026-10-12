using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Services;

/// <summary>
/// خدمة البحث والتصدير (مستخرجة من `DocumentService` — المرحلة 2): قرائية غالبًا —
/// أدنى المراحل خطرًا. التدوير (`GetRotationListAsync`/`GetBaseNumberHistoryAsync`/
/// `SaveBaseNumbersAsync`) يبقى مع النواة (ترقيم/دوائر) لا مع البحث.
/// تقسيم الزوج موثّق: `GetDeletedAsync` (قراءة مفردة بالمعرف) بالنواة، و`SearchDeletedAsync`
/// (استعلام ترحّلي) هنا — التقسيم حسب شكل العملية لا الاسم.
/// </summary>
public interface IDocumentSearchService
{
    Task<PagedResult<DocumentResponse>> SearchDeletedAsync(string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, int? ownerSectionId = null);

    Task<PagedResult<DocumentResponse>> SearchAsync(string? query, string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, string? executedEntity, string? publicEntityBranch, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, int? ownerSectionId = null);

    /// <summary>
    /// بحث ترحّلي عن ملفات وضع «منفذ عليه» المشطوبة فقط — صفحة «الملفات المشطوبة».
    /// بنفس صلاحيات المحذوفات: محامٍ (ملفاته) / رئيس قسم (فرعه) / مشرف (الكل).
    /// </summary>
    Task<PagedResult<DocumentResponse>> SearchStruckOffAsync(string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, int? ownerSectionId = null);

    /// <summary>
    /// بحث ترحّلي عن الملفات المنفذة (منفذ عليها/عرض وايداع بحالة «منفذ»، وطالبة تنفيذ
    /// بالتسوية أو الجبري الكامل) — صفحة «الملفات المنفذة»، ظاهرة لجميع الأدوار.
    /// </summary>
    Task<PagedResult<DocumentResponse>> SearchExecutedAsync(string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, int? ownerSectionId = null);

    /// <summary>
    /// بحث ترحّلي عن ملفات «طالبة تنفيذ» بحالة «محال الى البداية» فقط (ومنها القادم من
    /// «منفذ جبريا» المحال بجزئيته) — صفحة «محال الى البداية»، ظاهرة لجميع الأدوار.
    /// </summary>
    Task<PagedResult<DocumentResponse>> SearchReferredToStartAsync(string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, int? ownerSectionId = null);

    /// <summary>
    /// تصدير المستندات بصفوف مسقطة (`DocumentExportRow`) بدل الكيانات الكاملة —
    /// تغيير عقد داخلي مبرر (منادٍ واحد) لخفض ذاكرة التصدير الكبير جذريًا.
    /// </summary>
    Task<List<DocumentExportRow>> ExportAsync(string? query, string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, string? executedEntity, string? publicEntityBranch, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, string? actorName = null, int? ownerSectionId = null);

    Task<DocumentFilterOptions> GetFilterOptionsAsync(string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, string? executedEntity, string? publicEntityBranch, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, int? ownerSectionId = null);
}
