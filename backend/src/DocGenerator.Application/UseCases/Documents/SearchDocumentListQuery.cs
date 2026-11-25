using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>
/// قائمة الملفات بإسقاط صف القائمة (P3-04b): نفس الفلاتر/الترتيب، لكن بلا
/// `WithStandardIncludes` — إسقاط خفيف للصفحة الحالية فقط.
/// </summary>
public sealed record SearchDocumentListQuery(
    string? Query, string? Status, string? Applicant, string? Court, string? Lawyer,
    string? Branch, string? AdministrativeBranch, string? ExecutedEntity, string? PublicEntityBranch,
    int Page, int PerPage, int? VisibleBranchId = null, int? VisibleUserId = null,
    int? OwnerSectionId = null, string? Cursor = null)
    : IQuery<PagedResult<DocumentListItemDto>>;

internal sealed class SearchDocumentListQueryHandler
    : QueryHandlerBase<SearchDocumentListQuery, PagedResult<DocumentListItemDto>>
{
    private readonly IDocumentRepository _documents;
    private readonly IAppealRepository _appeals;
    private readonly IKeysetCursorCodec _cursorCodec;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public SearchDocumentListQueryHandler(
        IDocumentRepository documents,
        IAppealRepository appeals,
        IKeysetCursorCodec cursorCodec,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<SearchDocumentListQuery, PagedResult<DocumentListItemDto>>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _appeals = appeals;
        _cursorCodec = cursorCodec;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<PagedResult<DocumentListItemDto>> HandleCoreAsync(
        SearchDocumentListQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var perPage = Math.Clamp(query.PerPage <= 0 ? 20 : query.PerPage, 1, 100);

        var keyset = _cursorCodec.DecodeOptional(query.Cursor, DocumentListCursorKinds.CreatedAt);
        var (total, rows, hasMore) = await _documents.SearchListAsync(
            query.Query, query.Status, query.Applicant, query.Court, query.Lawyer, query.Branch, query.AdministrativeBranch, query.ExecutedEntity, query.PublicEntityBranch, query.VisibleBranchId, query.VisibleUserId, page, perPage, ct, query.OwnerSectionId, keyset);

        var result = DocumentSearchHelpers.BuildListResult(rows, total, query.Cursor is null ? page : 1, perPage,
            DocumentSearchHelpers.NextCursor(DocumentListCursorKinds.CreatedAt, rows, hasMore, _cursorCodec),
            _clock, _timeZone);
        await DocumentSearchHelpers.EnrichAppealFlagsAsync(result.Items, _appeals, ct);
        return result;
    }
}
