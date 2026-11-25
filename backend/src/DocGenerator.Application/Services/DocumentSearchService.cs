using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// خدمة البحث والتصدير (مستخرجة من خدمة المستندات القديمة — المرحلة 2، نقل ميكانيكي بلا سلوك).
/// الاعتمادات الدقيقة: `_documents` و`_appeals` و`_audit` (التصدير يسجّل تدقيقًا)
/// و`ExportOptions` (`_maxExportRows`) والساعة/المنطقة — وبلا `_tx`/`_uow`/`_users`.
/// قرائية غالبًا (الكتابة الوحيدة: سجل تدقيق التصدير بحفظة واحدة بلا معاملة).
/// </summary>
public sealed class DocumentSearchService : IDocumentSearchService
{
    private readonly IDocumentRepository _documents;
    private readonly IAppealRepository _appeals;
    private readonly IAuditLogger _audit;
    private readonly int _maxExportRows;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IKeysetCursorCodec _cursorCodec;

    public DocumentSearchService(
        IDocumentRepository documents,
        IAppealRepository appeals,
        IAuditLogger audit,
        Microsoft.Extensions.Options.IOptions<Common.ExportOptions> exportOptions,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IKeysetCursorCodec cursorCodec)
    {
        _documents = documents;
        _appeals = appeals;
        _audit = audit;
        _maxExportRows = Math.Max(1, exportOptions.Value.MaxRows);
        _clock = clock;
        _timeZone = timeZone;
        _cursorCodec = cursorCodec;
    }

    public async Task<PagedResult<DocumentResponse>> SearchDeletedAsync(
        string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, int? ownerSectionId = null)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, items) = await _documents.SearchDeletedAsync(query, visibleBranchId, visibleUserId, page, perPage, ct, ownerSectionId);

        return new PagedResult<DocumentResponse>
        {
            Items = items.Select(d => DocumentResponse.FromEntity(d, ServerClock.CurrentYear(_clock, _timeZone))).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    /// <summary>قائمة المحذوفات بإسقاط صف القائمة (P3-04c) — نفس الترقيم حرفيًا.</summary>
    public async Task<PagedResult<DocumentListItemDto>> SearchDeletedListAsync(
        string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, int? ownerSectionId = null, string? cursor = null)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var keyset = _cursorCodec.DecodeOptional(cursor, DocumentListCursorKinds.DeletedAt);
        var (total, rows, hasMore) = await _documents.SearchDeletedListAsync(query, visibleBranchId, visibleUserId, page, perPage, ct, ownerSectionId, keyset);
        return BuildListResult(rows, total, cursor is null ? page : 1, perPage,
            NextCursor(DocumentListCursorKinds.DeletedAt, rows, hasMore));
    }

    public async Task<PagedResult<DocumentResponse>> SearchStruckOffAsync(
        string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, int? ownerSectionId = null)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, items) = await _documents.SearchStruckOffAsync(query, null, null, null, null, null, visibleBranchId, visibleUserId, page, perPage, ct, ownerSectionId);

        return new PagedResult<DocumentResponse>
        {
            Items = items.Select(d => DocumentResponse.FromEntity(d, ServerClock.CurrentYear(_clock, _timeZone))).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    /// <summary>قائمة المشطوبة بإسقاط صف القائمة (P3-04c) — نفس الترقيم حرفيًا.</summary>
    public async Task<PagedResult<DocumentListItemDto>> SearchStruckOffListAsync(
        string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, int? ownerSectionId = null, string? cursor = null)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var keyset = _cursorCodec.DecodeOptional(cursor, DocumentListCursorKinds.StruckOffDate);
        var (total, rows, hasMore) = await _documents.SearchStruckOffListAsync(query, null, null, null, null, null, visibleBranchId, visibleUserId, page, perPage, ct, ownerSectionId, keyset);
        return BuildListResult(rows, total, cursor is null ? page : 1, perPage,
            NextCursor(DocumentListCursorKinds.StruckOffDate, rows, hasMore));
    }

    public async Task<PagedResult<DocumentResponse>> SearchExecutedAsync(
        string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, int? ownerSectionId = null)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, items) = await _documents.SearchExecutedAsync(query, visibleBranchId, visibleUserId, page, perPage, ct, ownerSectionId);

        return new PagedResult<DocumentResponse>
        {
            Items = items.Select(d => DocumentResponse.FromEntity(d, ServerClock.CurrentYear(_clock, _timeZone))).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    /// <summary>قائمة المنفذة بإسقاط صف القائمة (P3-04c) — نفس الترقيم حرفيًا.</summary>
    public async Task<PagedResult<DocumentListItemDto>> SearchExecutedListAsync(
        string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, int? ownerSectionId = null, string? cursor = null)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var keyset = _cursorCodec.DecodeOptional(cursor, DocumentListCursorKinds.UpdatedAt);
        var (total, rows, hasMore) = await _documents.SearchExecutedListAsync(query, visibleBranchId, visibleUserId, page, perPage, ct, ownerSectionId, keyset);
        return BuildListResult(rows, total, cursor is null ? page : 1, perPage,
            NextCursor(DocumentListCursorKinds.UpdatedAt, rows, hasMore));
    }

    public async Task<PagedResult<DocumentResponse>> SearchReferredToStartAsync(
        string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, int? ownerSectionId = null)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, items) = await _documents.SearchReferredToStartAsync(query, visibleBranchId, visibleUserId, page, perPage, ct, ownerSectionId);

        return new PagedResult<DocumentResponse>
        {
            Items = items.Select(d => DocumentResponse.FromEntity(d, ServerClock.CurrentYear(_clock, _timeZone))).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    /// <summary>قائمة «محال الى البداية» بإسقاط صف القائمة (P3-04c) — نفس الترقيم حرفيًا.</summary>
    public async Task<PagedResult<DocumentListItemDto>> SearchReferredToStartListAsync(
        string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, int? ownerSectionId = null, string? cursor = null)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var keyset = _cursorCodec.DecodeOptional(cursor, DocumentListCursorKinds.StartReferralDate);
        var (total, rows, hasMore) = await _documents.SearchReferredToStartListAsync(query, visibleBranchId, visibleUserId, page, perPage, ct, ownerSectionId, keyset);
        return BuildListResult(rows, total, cursor is null ? page : 1, perPage,
            NextCursor(DocumentListCursorKinds.StartReferralDate, rows, hasMore));
    }

    public async Task<PagedResult<DocumentResponse>> SearchAsync(
        string? query, string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, string? executedEntity, string? publicEntityBranch, int page, int perPage,
        int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, int? ownerSectionId = null)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, items) = await _documents.SearchAsync(
            query, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch, visibleBranchId, visibleUserId, page, perPage, ct, ownerSectionId);

        var result = new PagedResult<DocumentResponse>
        {
            Items = items.Select(d => DocumentResponse.FromEntity(d, ServerClock.CurrentYear(_clock, _timeZone))).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
        await EnrichAppealFlagsAsync(result.Items, ct);
        return result;
    }

    /// <summary>
    /// قائمة الملفات بإسقاط صف القائمة (P3-04b): نفس الفلاتر/الترتيب، لكن بلا
    /// `WithStandardIncludes`. يُعاد استخدام `DocumentResponse.FromEntity` على كيان
    /// مُجمَّع في الذاكرة، فتبقى الحقول المشتقة (الحالة/الرقم الفعّال/التدوير/آخر نشاط)
    /// مطابقة لقائمة التفاصيل حرفيًا.
    /// </summary>
    public async Task<PagedResult<DocumentListItemDto>> SearchListAsync(
        string? query, string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, string? executedEntity, string? publicEntityBranch, int page, int perPage,
        int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, int? ownerSectionId = null, string? cursor = null)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage <= 0 ? 20 : perPage, 1, 100);

        var keyset = _cursorCodec.DecodeOptional(cursor, DocumentListCursorKinds.CreatedAt);
        var (total, rows, hasMore) = await _documents.SearchListAsync(
            query, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch, visibleBranchId, visibleUserId, page, perPage, ct, ownerSectionId, keyset);

        var result = BuildListResult(rows, total, cursor is null ? page : 1, perPage,
            NextCursor(DocumentListCursorKinds.CreatedAt, rows, hasMore));
        await EnrichAppealFlagsAsync(result.Items, ct);
        return result;
    }

    /// <summary>
    /// بناء صفحة عناصر القائمة من صفوف مُسقَطة — مصدر واحد لكل قوائم الملفات
    /// (P3-04b والقوائم الخمس في P3-04c) فلا يتباعد شكل استجابة قائمة عن أخرى.
    /// </summary>
    private PagedResult<DocumentListItemDto> BuildListResult(
        List<DocumentListRow> rows, int total, int page, int perPage, string? nextCursor = null)
    {
        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        return new PagedResult<DocumentListItemDto>
        {
            Items = rows.Select(row => DocumentListItemDto.FromRow(row, currentYear)).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
            NextCursor = nextCursor,
        };
    }

    /// <summary>
    /// يبني مؤشر الصفحة التالية من آخر صف معروض وقيمة مفتاح ترتيب القائمة — بلا
    /// قراءة إضافية؛ غياب صف تالٍ (`hasMore = false`) يعني آخر صفحة.
    /// </summary>
    private string? NextCursor(string kind, List<DocumentListRow> rows, bool hasMore)
    {
        if (!hasMore || rows.Count == 0)
            return null;

        var last = rows[^1];
        DateTime? sortValue = kind switch
        {
            DocumentListCursorKinds.CreatedAt => last.CreatedAt,
            DocumentListCursorKinds.DeletedAt => last.DeletedAt,
            DocumentListCursorKinds.StruckOffDate => last.StruckOffDate,
            DocumentListCursorKinds.UpdatedAt => last.UpdatedAt,
            DocumentListCursorKinds.StartReferralDate => last.StartReferralDate,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "نوع مؤشر غير معروف"),
        };
        return _cursorCodec.Encode(kind, sortValue, last.Id);
    }

    private async Task EnrichAppealFlagsAsync(List<DocumentListItemDto> items, CancellationToken ct)
    {
        if (items.Count == 0)
            return;
        var ids = items.Select(i => i.Id).ToList();
        var map = await _appeals.MapFirstAppealIdByDocumentIdsAsync(ids, ct);
        if (map.Count == 0)
            return;
        foreach (var item in items)
        {
            if (map.TryGetValue(item.Id, out var appealId))
            {
                item.HasAppeals = true;
                item.MatchedAppealId = appealId;
            }
        }
    }

    /// <summary>
    /// إثراء نتائج القائمة بعلم وجود استئناف منظور على الملف ومعرف أول استئناف منظور —
    /// لشارة «استئناف» بجانب النتائج والانتقال إلى تفاصيل الاستئناف. المحسوم والمشطوب
    /// لا يُظهران الشارة (تختفي بعد حسم/شطب آخر منظور). استعلام دفعي واحد لمعرفات الصفحة الحالية فقط.
    /// </summary>
    private async Task EnrichAppealFlagsAsync(List<DocumentResponse> items, CancellationToken ct)
    {
        if (items.Count == 0)
            return;
        var ids = items.Select(i => i.Id).ToList();
        var map = await _appeals.MapFirstAppealIdByDocumentIdsAsync(ids, ct);
        if (map.Count == 0)
            return;
        foreach (var item in items)
        {
            if (map.TryGetValue(item.Id, out var appealId))
            {
                item.HasAppeals = true;
                item.MatchedAppealId = appealId;
            }
        }
    }

    public async Task<DocumentFilterOptions> GetFilterOptionsAsync(
        string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, string? executedEntity, string? publicEntityBranch,
        int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, int? ownerSectionId = null)
        => await _documents.GetFilterOptionsAsync(status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch, visibleBranchId, visibleUserId, ct, ownerSectionId);

    public async Task<List<DocumentExportRow>> ExportAsync(
        string? query, string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, string? executedEntity, string? publicEntityBranch,
        int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default, string? actorName = null, int? ownerSectionId = null)
    {
        // سقف التصدير: يُعدَّل عدد النتائج المطابقة أولًا قبل جلب أي صف إلى الذاكرة،
        // فيُرفض التصدير الواسع برسالة واضحة بدل ذروة ذاكرة غير محصورة على الخادم.
        var total = await _documents.CountExportAsync(
            query, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch, visibleBranchId, visibleUserId, ct, ownerSectionId);
        if (total > _maxExportRows)
            throw new ArgumentException($"عدد النتائج يتجاوز الحد الأقصى للتصدير ({_maxExportRows:N0}) — طبّق فلترًا أضيق");

        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        var rows = await _documents.ExportRowsAsync(
            query, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch, visibleBranchId, visibleUserId, ct, ownerSectionId);
        // الرقم الفعّال (LatestFrom) على البذور المسقطة — نفس منطق `FromEntity` حرفيًا،
        // في الذاكرة (رخيص: بضعة أرقام أساس لكل صف).
        foreach (var row in rows)
        {
            var latest = EffectiveFileIdentity.LatestFrom(row.BaseNumbers, currentYear, row.ExecutionCircuitId);
            row.DisplayFileNumber = latest?.BaseNumber ?? row.FileNumber;
            row.DisplayFileYear = latest?.Year.ToString(CultureInfo.InvariantCulture) ?? row.FileYear;
        }
        // RF-017 (SEC-002): التصدير قراءة بلا كتابة عمل — تدقيقه حفظة واحدة بلا معاملة
        // (بالفلاتر والعدد الفعلي؛ الفلاتر الفارغة تُحذَف من النص).
        var filters = string.Join("،", new[] { query, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch }
            .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
        await _audit.LogAsync(actorName, "export_documents",
            details: $"صدّر {rows.Count} صفًا" + (filters.Length == 0 ? " (بلا فلاتر)" : $" بفلاتر: {filters}"), ct: ct);
        return rows;
    }
}
