using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// البحث الترحّلي للمستندات على مستوى قاعدة البيانات (بدل تحميل الكل في الذاكرة).
/// </summary>
public interface IDocumentRepository : IRepository<Document>
{
    Task<(int TotalCount, List<Document> Items)> SearchAsync(
        string? query,
        string? status,
        string? applicant,
        string? court,
        string? lawyer,
        int? branchId,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
        CancellationToken ct = default);

    Task<(List<string> Applicants, List<string> Courts, List<string> Lawyers)> GetFilterOptionsAsync(
        int? visibleBranchId,
        int? visibleUserId,
        CancellationToken ct = default);

    /// <summary>
    /// يجلب مستنداً محذوفاً منطقياً متجاوزاً Query Filter، لخدمة الاستعادة.
    /// </summary>
    Task<Document?> GetDeletedByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// بحث ترحّلي عن المستندات المحذوفة منطقياً فقط (متجاوزاً Query Filter)،
    /// ليُعرض سجل المحذوفات قبل الاستعادة.
    /// </summary>
    Task<(int TotalCount, List<Document> Items)> SearchDeletedAsync(
        string? query,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
        CancellationToken ct = default);

    /// <summary>
    /// نقل ذرّي آمن للتفاؤلية: يحدّث المحامي المختص للملف بشرط أن يكون المحامي المختص
    /// الحالي ما زال هو المتوقع (WHERE على CreatedById)، فيرجع المستند المحدّث،
    /// أو null إن تغيّر المحامي أثناء العملية (تعارض متزامن).
    /// </summary>
    Task<Document?> TransferOwnerAsync(
        int id,
        int expectedCreatedById,
        int targetId,
        string targetFullName,
        CancellationToken ct = default);
}

/// <summary>
/// يحدّ عدد محاولات تسجيل الدخول الفاشلة لكل مفتاح (IP + اسم مستخدم) خلال نافذة زمنية.
/// تنفيذ يعتمد على قاعدة البيانات ليكون مشتركاً بين عقد النشر المتعددة —
/// يعادل LoginRateLimiter في نسخة Flask.
/// </summary>
public interface ILoginRateLimiter
{
    Task<bool> IsAllowedAsync(string key, CancellationToken ct = default);
    Task RecordFailureAsync(string key, CancellationToken ct = default);
    Task ResetAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// تسجيل أحداث التدقيق (دخول، إنشاء/تعديل/حذف، تغيير حالة).
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(string? userName, string actionType, int? documentId = null,
        string? documentType = null, string? details = null, CancellationToken ct = default);
}
