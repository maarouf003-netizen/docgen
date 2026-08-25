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
/// مستودع بوابة مندوب الجهة: استعلامات الملفات المقيّدة بنطاق المندوب
/// (أي تطابق طرفي بقيد نهائي — د1/د4) والتصدير منها.
/// </summary>
public interface IPortalRepository
{
    /// <summary>يحلّ نطاق المندوب من ربط حسابه — null إن لم يُربط بنطاق.</summary>
    Task<PortalScopeResolution?> ResolveForUserAsync(int userId, CancellationToken ct = default);

    /// <summary>هل يقع الملف ضمن نطاق معرّفات القيود المعطاة (بقيد نهائي)؟</summary>
    Task<bool> IsDocumentInScopeAsync(int documentId, IReadOnlyCollection<int> entryIds, CancellationToken ct = default);

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

    /// <summary>ثنائيات (IsDraft، ExecStatus) لملفات النطاق — يُصنّفها المستدعي وفق كتالوج الحالات.</summary>
    Task<List<(bool IsDraft, string? ExecStatus)>> ListStatusPairsAsync(IReadOnlyCollection<int> entryIds, CancellationToken ct = default);

    /// <summary>تواريخ إنشاء ملفات النطاق (UTC) لبناء السلسلة الشهرية.</summary>
    Task<List<DateTime>> ListCreatedDatesAsync(IReadOnlyCollection<int> entryIds, CancellationToken ct = default);

    /// <summary>ثنائيات (العملة، المبلغ) لملفات النطاق لتجميع العملات الأعلى.</summary>
    Task<List<(string? Currency, decimal Amount)>> ListCurrencyAmountsAsync(IReadOnlyCollection<int> entryIds, CancellationToken ct = default);

    /// <summary>
    /// عدد الملفات المرتبطة بكل قيد من قيود النطاق. قد يُحتسب الملف الواحد تحت أكثر
    /// من قيد إذا ارتبط بأطراف متعددة ضمن النطاق نفسه (توزيع ارتباط لا تجزئة حصرية).
    /// </summary>
    Task<Dictionary<int, int>> CountDocsPerEntryAsync(IReadOnlyCollection<int> entryIds, CancellationToken ct = default);

    /// <summary>استئنافات ملفات النطاق: (معلّقة، مغلقة) — المغلق يشمل المحسوم والمشطوب.</summary>
    Task<(int Pending, int Closed)> AppealsBreakdownAsync(IReadOnlyCollection<int> entryIds, CancellationToken ct = default);
}
