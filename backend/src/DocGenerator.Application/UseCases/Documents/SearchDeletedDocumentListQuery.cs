using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>قائمة المحذوفات بإسقاط صف القائمة (P3-04c) — نفس الترقيم حرفيًا.</summary>
public sealed record SearchDeletedDocumentListQuery(
    string? Query, int Page, int PerPage, int? VisibleBranchId = null,
    int? VisibleUserId = null, int? OwnerSectionId = null, string? Cursor = null)
    : IQuery<PagedResult<DocumentListItemDto>>;

internal sealed class SearchDeletedDocumentListQueryHandler
    : QueryHandlerBase<SearchDeletedDocumentListQuery, PagedResult<DocumentListItemDto>>
{
    private readonly IDocumentRepository _documents;
    private readonly IKeysetCursorCodec _cursorCodec;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public SearchDeletedDocumentListQueryHandler(
        IDocumentRepository documents,
        IKeysetCursorCodec cursorCodec,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<SearchDeletedDocumentListQuery, PagedResult<DocumentListItemDto>>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _cursorCodec = cursorCodec;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<PagedResult<DocumentListItemDto>> HandleCoreAsync(
        SearchDeletedDocumentListQuery query, CancellationToken ct)
    {
        var (page, perPage) = PageRequest.Normalize(query.Page, query.PerPage);

        var keyset = _cursorCodec.DecodeOptional(query.Cursor, DocumentListCursorKinds.DeletedAt);
        var (total, rows, hasMore) = await _documents.SearchDeletedListAsync(query.Query, query.VisibleBranchId, query.VisibleUserId, page, perPage, ct, query.OwnerSectionId, keyset);
        return DocumentSearchHelpers.BuildListResult(rows, total, query.Cursor is null ? page : 1, perPage,
            DocumentSearchHelpers.NextCursor(DocumentListCursorKinds.DeletedAt, rows, hasMore, _cursorCodec),
            _clock, _timeZone);
    }
}
