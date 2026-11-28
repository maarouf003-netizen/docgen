using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>قائمة «محال الى البداية» بإسقاط صف القائمة (P3-04c) — نفس الترقيم حرفيًا.</summary>
public sealed record SearchReferredToStartDocumentListQuery(
    string? Query, int Page, int PerPage, int? VisibleBranchId = null,
    int? VisibleUserId = null, int? OwnerSectionId = null, string? Cursor = null)
    : IQuery<PagedResult<DocumentListItemDto>>;

internal sealed class SearchReferredToStartDocumentListQueryHandler
    : QueryHandlerBase<SearchReferredToStartDocumentListQuery, PagedResult<DocumentListItemDto>>
{
    private readonly IDocumentRepository _documents;
    private readonly IKeysetCursorCodec _cursorCodec;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public SearchReferredToStartDocumentListQueryHandler(
        IDocumentRepository documents,
        IKeysetCursorCodec cursorCodec,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<SearchReferredToStartDocumentListQuery, PagedResult<DocumentListItemDto>>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _cursorCodec = cursorCodec;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<PagedResult<DocumentListItemDto>> HandleCoreAsync(
        SearchReferredToStartDocumentListQuery query, CancellationToken ct)
    {
        var (page, perPage) = PageRequest.Normalize(query.Page, query.PerPage);

        var keyset = _cursorCodec.DecodeOptional(query.Cursor, DocumentListCursorKinds.StartReferralDate);
        var (total, rows, hasMore) = await _documents.SearchReferredToStartListAsync(query.Query, query.VisibleBranchId, query.VisibleUserId, page, perPage, ct, query.OwnerSectionId, keyset);
        return DocumentSearchHelpers.BuildListResult(rows, total, query.Cursor is null ? page : 1, perPage,
            DocumentSearchHelpers.NextCursor(DocumentListCursorKinds.StartReferralDate, rows, hasMore, _cursorCodec),
            _clock, _timeZone);
    }
}
