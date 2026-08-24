using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Services;

public interface IPortalService
{
    /// <summary>نطاق المندوب (هوية/قيد) — null إن لم يُربط بنطاق بعد.</summary>
    Task<PortalScopeDto?> GetMyScopeAsync(int userId, CancellationToken ct = default);

    Task<PagedResult<PortalFileListItemDto>> ListFilesAsync(
        int userId, string? query, string? status, int page, int perPage, CancellationToken ct = default);

    /// <summary>تفاصيل ملف قراءةً — null إذا خرج عن نطاق المندوب (يُترجم 404).</summary>
    Task<DocumentResponse?> GetFileAsync(int userId, int documentId, string? viewerName, CancellationToken ct = default);

    /// <summary>بطاقة الاستئنافات القرائية لملف داخل النطاق — null إذا خرج عن النطاق.</summary>
    Task<IReadOnlyList<PortalAppealDto>?> ListAppealsAsync(int userId, int documentId, CancellationToken ct = default);

    /// <summary>
    /// مصنّف Excel لملفات النطاق وفق فلاتر القائمة نفسها، مع سقف
    /// ExportOptions.MaxRows وتدقيق export_entity_portal_excel.
    /// </summary>
    Task<byte[]> ExportWorkbookAsync(int userId, string? query, string? status, string? viewerName, CancellationToken ct = default);
}

/// <summary>
/// خدمة بوابة مندوب الجهة العامة (المرحلة 3): رؤية قرائية بحسب الربط — هوية أم
/// تشمل كل قيودها النهائية، أو قيد بعينه (د1/د4)؛ قيود الانتظار لا تظهر إطلاقًا.
/// التصدير يمرّ بسقف الصفوف ويُدوَّن، وأعمدة المحامين الداخلية مخفية دائمًا عن البوابة.
/// </summary>
public sealed class PortalService : IPortalService
{
    private readonly IPortalRepository _portal;
    private readonly IRepository<Document> _documents;
    private readonly IAppealRepository _appeals;
    private readonly IExcelExportService _excel;
    private readonly IAuditLogger _audit;
    private readonly int _maxExportRows;

    public PortalService(
        IPortalRepository portal,
        IRepository<Document> documents,
        IAppealRepository appeals,
        IExcelExportService excel,
        IAuditLogger audit,
        IOptions<ExportOptions> exportOptions)
    {
        _portal = portal;
        _documents = documents;
        _appeals = appeals;
        _excel = excel;
        _audit = audit;
        _maxExportRows = Math.Max(1, exportOptions.Value.MaxRows);
    }

    public async Task<PortalScopeDto?> GetMyScopeAsync(int userId, CancellationToken ct = default)
    {
        var scope = await _portal.ResolveForUserAsync(userId, ct);
        if (scope is null || scope.GroupId == 0)
            return scope is null ? null : new PortalScopeDto("group", 0, string.Empty, "ministry",
                Array.Empty<PortalScopeEntryDto>());

        return new PortalScopeDto(scope.ScopeType, scope.GroupId, scope.CanonicalName, scope.EntityType,
            scope.Entries.Select(e => new PortalScopeEntryDto(e.Id, e.Governorate, e.BranchName, e.IsActive)).ToList());
    }

    public async Task<PagedResult<PortalFileListItemDto>> ListFilesAsync(
        int userId, string? query, string? status, int page, int perPage, CancellationToken ct = default)
    {
        var scope = await _portal.ResolveForUserAsync(userId, ct);
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage <= 0 ? 20 : perPage, 1, 100);

        var (total, items) = await _portal.SearchScopedAsync(scope?.EntryIds ?? new List<int>(), query, status, page, perPage, ct);
        var result = new PagedResult<PortalFileListItemDto> { Page = page, PerPage = perPage, TotalCount = total };
        result.Items = items.Select(ToListItem).ToList();
        return result;
    }

    public async Task<DocumentResponse?> GetFileAsync(int userId, int documentId, string? viewerName, CancellationToken ct = default)
    {
        if (!await IsInScopeAsync(userId, documentId, ct))
            return null;

        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return null;

        // تدقيق دخول جلسة العرض: مرة عند فتح الملف، لا مع كل صفحة/تفصيلة (§4).
        await _audit.LogAsync(viewerName, "view_entity_portal_files", documentId,
            details: "عرض ملف في بوابة الجهة العامة", ct: ct);

        return DocumentResponse.FromEntity(doc);
    }

    public async Task<IReadOnlyList<PortalAppealDto>?> ListAppealsAsync(int userId, int documentId, CancellationToken ct = default)
    {
        if (!await IsInScopeAsync(userId, documentId, ct))
            return null;

        var appeals = await _appeals.ListByDocumentAsync(documentId, ct);
        return appeals.Select(a => new PortalAppealDto(
            a.Id,
            a.Direction,
            a.Status,
            a.AppealTypeLabel,
            a.AppealBaseNumber,
            a.AppealYear,
            a.CreatedAt,
            a.DecisionDate,
            a.DecisionRuling)).ToList();
    }

    public async Task<byte[]> ExportWorkbookAsync(int userId, string? query, string? status, string? viewerName, CancellationToken ct = default)
    {
        var scope = await _portal.ResolveForUserAsync(userId, ct);
        var entryIds = scope?.EntryIds ?? new List<int>();

        var total = await _portal.CountScopedAsync(entryIds, query, status, ct);
        if (total > _maxExportRows)
            throw new ArgumentException($"عدد النتائج يتجاوز الحد الأقصى للتصدير ({_maxExportRows:N0}) — طبّق فلترًا أضيق");

        var docs = await _portal.ExportScopedAsync(entryIds, query, status, ct);
        var responses = docs.Select(DocumentResponse.FromEntity).ToList();

        await _audit.LogAsync(viewerName, "export_entity_portal_excel",
            details: $"صدّر {responses.Count} ملفًا من بوابة الجهة إلى Excel", ct: ct);

        // أعمدة المحامين الداخلية (فرع الإدارة/المحامي المختص/العدادات) مخفية دائمًا عن البوابة.
        return _excel.BuildDocumentsWorkbook(
            responses, includeAdministrativeBranch: false, includeAssignedLawyer: false, includeViewCount: false);
    }

    private async Task<bool> IsInScopeAsync(int userId, int documentId, CancellationToken ct)
    {
        var scope = await _portal.ResolveForUserAsync(userId, ct);
        return await _portal.IsDocumentInScopeAsync(documentId, scope?.EntryIds ?? new List<int>(), ct);
    }

    private static PortalFileListItemDto ToListItem(Document d) => new(
        d.Id,
        d.DocumentType ?? string.Empty,
        d.IsDraft,
        d.BorrowerName,
        d.Applicant,
        ExecutedSummary(d),
        d.AmountNumeric,
        d.Currency,
        d.ExecStatus,
        d.CreatedAt,
        d.UpdatedAt);

    private static string ExecutedSummary(Document d) =>
        string.Join("؛ ", d.ExecutedPublicEntities
            .Where(e => e.EntityNature == PartyNatureCatalog.PublicEntity)
            .Select(e => string.Join(' ', new[] { e.EntityName, e.EntityBranch }.Where(p => !string.IsNullOrWhiteSpace(p))))
            .Where(v => v.Length > 0));
}
