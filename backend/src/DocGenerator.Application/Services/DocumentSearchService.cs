using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Services;

/// <summary>
/// خدمة البحث والتصدير (مستخرجة من `DocumentService` — المرحلة 2، نقل ميكانيكي بلا سلوك).
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

    public DocumentSearchService(
        IDocumentRepository documents,
        IAppealRepository appeals,
        IAuditLogger audit,
        Microsoft.Extensions.Options.IOptions<Common.ExportOptions> exportOptions,
        TimeProvider clock,
        TimeZoneInfo timeZone)
    {
        _documents = documents;
        _appeals = appeals;
        _audit = audit;
        _maxExportRows = Math.Max(1, exportOptions.Value.MaxRows);
        _clock = clock;
        _timeZone = timeZone;
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
            var latest = EffectiveFileIdentity.LatestFrom(row.BaseNumbers, currentYear);
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
