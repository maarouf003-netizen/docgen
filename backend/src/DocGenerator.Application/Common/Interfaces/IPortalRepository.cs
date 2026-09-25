using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>نطاق مندوب بعد حلّه من ربط حسابه: هوية أم أو قيد بعينه، ومعرّفات قيوده النهائية النشطة.</summary>
public sealed record PortalScopeResolution(
    string ScopeType,
    int GroupId,
    string CanonicalName,
    string EntityType,
    IReadOnlyList<(int Id, string Governorate, string BranchName, bool IsActive)> Entries)
{
    public IReadOnlyList<int> EntryIds => Entries.Select(e => e.Id).ToList();
}

/// <summary>
/// مفاتيح نطاق ملف لاستهداف مناديب الجهة: قيود الملف النهائية النشطة (RegistryIds)
/// مع معرّفات الهويات الأم (PublicEntityGroups) لتلك القيود.
/// </summary>
public sealed record DocumentScopeKeys(
    IReadOnlyList<int> EntryIds,
    IReadOnlyList<int> GroupIds)
{
    public static DocumentScopeKeys Empty { get; } = new(Array.Empty<int>(), Array.Empty<int>());
}

/// <summary>
/// صف اللقطة الإحصائية لملف واحد: حقول التصنيف (IsDraft، ExecStatus، ExecSubStatus —
/// الأخيرة لتمييز «منفذ جبريا + منفذ جزئيا» المتداول دائمًا بقرار المالك) ورابط الإنابة
/// (SourceDelegationId — نسخة الإنابة تُحتسب عددًا دون مبالغ) وأزواج مبالغ الدين الستة
/// كما سُجّلت في النموذج (المبلغ×3 + مبلغ الإدراج×3 بعملاتها).
/// </summary>
public sealed record PortalAmountRow(
    int DocumentId,
    bool IsDraft,
    string? ExecStatus,
    string? ExecSubStatus,
    int? SourceDelegationId,
    string? Currency,
    decimal AmountNumeric,
    string? Currency2,
    decimal Amount2Numeric,
    string? Currency3,
    decimal Amount3Numeric,
    string? InclusionCurrency,
    decimal InclusionAmountNumeric,
    string? InclusionCurrency2,
    decimal InclusionAmount2Numeric,
    string? InclusionCurrency3,
    decimal InclusionAmount3Numeric);

/// <summary>
/// مستودع بوابة مندوب الجهة: استعلامات الملفات المقيّدة بنطاق المندوب
/// (أي تطابق طرفي بقيد نهائي — د1/د4) والتصدير منها.
/// </summary>
public interface IPortalRepository
{
    /// <summary>يحلّ نطاق المندوب من ربط حسابه — null إن لم يُربط بنطاق.</summary>
    Task<PortalScopeResolution?> ResolveForUserAsync(int userId, CancellationToken ct = default);

    /// <summary>هل يقع الملف ضمن نطاق معرّفات القيود المعطاة (بقيد نهائي)؟</summary>
    Task<bool> IsDocumentInScopeAsync(int documentId, IReadOnlyCollection<int> entryIds, CancellationToken ct = default);

    /// <summary>
    /// قيود الملف النهائية النشطة وهوياتها الأم — لحصر مناديب الجهة المؤهلين
    /// لمراسلة مرتبطة بهذا الملف (عكس اشتقاق النطاق، بلا التحميل المسبق).
    /// </summary>
    Task<DocumentScopeKeys> GetDocumentScopeKeysAsync(int documentId, CancellationToken ct = default);

    /// <summary>قائمة ملفات النطاق مع عدّادها الكامل.</summary>
    Task<(int TotalCount, List<Document> Items)> SearchScopedAsync(
        IReadOnlyCollection<int> entryIds, string? query, string? status,
        int page, int perPage, CancellationToken ct = default);

    /// <summary>ملفات النطاق للتصدير بلا صفحات (السقف تتحقق منه الخدمة قبل الجلب).</summary>
    Task<List<Document>> ExportScopedAsync(
        IReadOnlyCollection<int> entryIds, string? query, string? status, CancellationToken ct = default);

    /// <summary>عدد ملفات النطاق المطابقة (للتحقق من سقف التصدير قبل الجلب).</summary>
    Task<int> CountScopedAsync(
        IReadOnlyCollection<int> entryIds, string? query, string? status, CancellationToken ct = default);

    // ── إحصاءات الجهة (المرحلة 4) — كلها فوق ScopePredicate الموحد وبلا صفحات ──

    /// <summary>تواريخ إنشاء ملفات النطاق (UTC) لبناء السلسلة الشهرية.</summary>
    Task<List<DateTime>> ListCreatedDatesAsync(IReadOnlyCollection<int> entryIds, CancellationToken ct = default);

    /// <summary>
    /// اللقطة الوحيدة التي تُشتق منها عدّادات الحالات والمجاميع معًا في الخدمة
    /// (فلا سباق بين استعلامين): كل ملف مع سلّة حالته (IsDraft، ExecStatus،
    /// ExecSubStatus) ورابط الإنابة (SourceDelegationId) وأزواج مبالغ الدين الستة
    /// (المبلغ×3 + مبلغ الإدراج×3 بعملاتها) — تُجمَّع حسب العملة بعد إسقاط الصفري.
    /// </summary>
    Task<List<PortalAmountRow>> ListAmountRowsAsync(IReadOnlyCollection<int> entryIds, CancellationToken ct = default);

    /// <summary>
    /// عدد الملفات المرتبطة بكل قيد من قيود النطاق. قد يُحتسب الملف الواحد تحت أكثر
    /// من قيد إذا ارتبط بأطراف متعددة ضمن النطاق نفسه (توزيع ارتباط لا تجزئة حصرية).
    /// </summary>
    Task<Dictionary<int, int>> CountDocsPerEntryAsync(IReadOnlyCollection<int> entryIds, CancellationToken ct = default);

    /// <summary>
    /// استئنافات ملفات النطاق: (معلّقة، مغلقة) — المغلق = محسوم/مشطوب حصرًا،
    /// وأي حالة مستقبلية غير معروفة تُحتسب معلّقة (ظاهرة لا مدفونة).
    /// </summary>
    Task<(int Pending, int Closed)> AppealsBreakdownAsync(IReadOnlyCollection<int> entryIds, CancellationToken ct = default);
}
