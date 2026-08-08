using DocGenerator.Application.DTOs;
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
        string? branch,
        string? administrativeBranch,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
        CancellationToken ct = default);

    /// <summary>
    /// كل المستندات المطابقة لفلاتر البحث (دون ترقيم) لتصديرها إلى ملف إكسل.
    /// </summary>
    Task<List<Document>> ExportAsync(
        string? query,
        string? status,
        string? applicant,
        string? court,
        string? lawyer,
        string? branch,
        string? administrativeBranch,
        int? visibleBranchId,
        int? visibleUserId,
        CancellationToken ct = default);

    /// <summary>
    /// خيارات فلترة «الملفات التنفيذية» بأسلوب إكسل. كل قائمة مُقيَّدة بباقي الفلاتر
    /// النشطة ما عدا فلتر الحقل نفسه، فيلتزم الاختيار اللاحق بنتائج الفلتر السابق تلقائيًا.
    /// </summary>
    Task<DocumentFilterOptions> GetFilterOptionsAsync(
        string? status,
        string? applicant,
        string? court,
        string? lawyer,
        string? branch,
        string? administrativeBranch,
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

    /// <summary>
    /// عدد ملفات المحامي غير المحذوفة (بجميع الحالات) — يُستخدم لمعاينة
    /// العدد قبل تنفيذ النقل الجماعي (لا يشمل المحذوف تلقائياً عبر Query Filter).
    /// </summary>
    Task<int> CountByOwnerAsync(int ownerId, CancellationToken ct = default);

    /// <summary>
    /// ملفات المحامي غير المحذوفة ببياناتها الأساسية (رقم/نوع/اسم المنفذ عليه)
    /// لكتابة سجل تدقيق لكل ملف بعد النقل الجماعي.
    /// </summary>
    Task<List<Document>> ListByOwnerAsync(int ownerId, CancellationToken ct = default);

    /// <summary>
    /// نقل جماعي ذرّي: يحدّث المحامي المختص لكل ملفات المصدر غير المحذوفة
    /// (WHERE على CreatedById) ويُرجع عدد الصفوف المتأثرة، فيمكن التحقق من
    /// تطابق العدد المتوقع مع ما نُقل فعلياً.
    /// </summary>
    Task<int> TransferAllOwnerAsync(
        int sourceOwnerId,
        int targetId,
        string targetFullName,
        CancellationToken ct = default);

    /// <summary>
    /// جلب عدة مستندات بمعرفاتها مع أرقام الأساس (Include BaseNumbers)،
    /// لخدمة حفظ التدوير ذرّيًا داخل المعاملة.
    /// </summary>
    Task<List<Document>> GetByIdsAsync(List<int> ids, CancellationToken ct = default);

    /// <summary>
    /// ملفات المحامي المؤهلة لتدوير أرقام الأساس (بحث ترحّلي): غير محذوفة (Query Filter)،
    /// مقيدة برقم ملف (ليست تحت رفع)، وغير منفَّذة — مع أرقام الأساس الخاصة بها.
    /// </summary>
    Task<(int TotalCount, List<Document> Items)> GetRotationCandidatesAsync(
        int userId, int page, int perPage, CancellationToken ct = default);

    /// <summary>
    /// بحث ترحّلي عن ملفات وضع «منفذ عليه» المشطوبة فقط (متجاوزاً Query Filter
    /// الخاص بالشطب): تُستثنى تلقائيًا من البحث العادي والتصدير، ويُعرض سجلها
    /// في صفحة «الملفات المشطوبة» قبل إعادة الشطب أو الاستعادة.
    /// </summary>
    Task<(int TotalCount, List<Document> Items)> SearchStruckOffAsync(
        string? query,
        string? applicant,
        string? court,
        string? lawyer,
        string? branch,
        string? administrativeBranch,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
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
/// إدخال سجل تدقيق يُجمَّع ليُحفظ دفعةً واحدة — لعمليات كبيرة العدد مثل تدوير الأرقام.
/// </summary>
public record AuditLogEntry(
    string? UserName,
    string ActionType,
    int? DocumentId = null,
    string? DocumentType = null,
    string? Details = null);

/// <summary>
/// تسجيل أحداث التدقيق (دخول، إنشاء/تعديل/حذف، تغيير حالة).
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(string? userName, string actionType, int? documentId = null,
        string? documentType = null, string? details = null, CancellationToken ct = default);

    /// <summary>
    /// يحفظ دفعة سجلات التدقيق بحفظ واحد في نهاية المعاملة — تفاديًا لاستدعاء
    /// حفظ منفصل لكل إدخال في العمليات الكبيرة (مثل تدوير آلاف الملفات).
    /// </summary>
    Task LogManyAsync(IReadOnlyList<AuditLogEntry> entries, CancellationToken ct = default);
}
