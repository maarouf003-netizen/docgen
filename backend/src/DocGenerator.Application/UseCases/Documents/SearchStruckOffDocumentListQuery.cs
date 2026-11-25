using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>قائمة المشطوبة بإسقاط صف القائمة (P3-04c) — نفس الترقيم حرفيًا.</summary>
public sealed record SearchStruckOffDocumentListQuery(
    string? Query, int Page, int PerPage, int? VisibleBranchId = null,
    int? VisibleUserId = null, int? OwnerSectionId = null, string? Cursor = null)
    : IQuery<PagedResult<DocumentListItemDto>>;

internal sealed class SearchStruckOffDocumentListQueryHandler
    : QueryHandlerBase<SearchStruckOffDocumentListQuery, PagedResult<DocumentListItemDto>>
{
    private readonly IDocumentRepository _documents;
    private readonly IKeysetCursorCodec _cursorCodec;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public SearchStruckOffDocumentListQueryHandler(
        IDocumentRepository documents,
        IKeysetCursorCodec cursorCodec,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<SearchStruckOffDocumentListQuery, PagedResult<DocumentListItemDto>>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _cursorCodec = cursorCodec;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<PagedResult<DocumentListItemDto>> HandleCoreAsync(
        SearchStruckOffDocumentListQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var perPage = Math.Clamp(query.PerPage, 1, 100);

        var keyset = _cursorCodec.DecodeOptional(query.Cursor, DocumentListCursorKinds.StruckOffDate);
        var (total, rows, hasMore) = await _documents.SearchStruckOffListAsync(query.Query, null, null, null, null, null, query.VisibleBranchId, query.VisibleUserId, page, perPage, ct, query.OwnerSectionId, keyset);
        return DocumentSearchHelpers.BuildListResult(rows, total, query.Cursor is null ? page : 1, perPage,
            DocumentSearchHelpers.NextCursor(DocumentListCursorKinds.StruckOffDate, rows, hasMore, _cursorCodec),
            _clock, _timeZone);
    }
}
