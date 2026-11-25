using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Reminders;

/// <summary>
/// قائمة «تذكيرات الشطب» بإسقاط صف القائمة (P3-04c): نفس تصفية/ترقيم المعرّفات في SQL،
/// ثم تحميل بطاقاتها بإسقاط خفيف — بلا <c>WithStandardIncludes</c> وبلا كيان كامل.
/// </summary>
public sealed record GetStrikeOffReminderListQuery(
    int Page,
    int PerPage,
    int? VisibleBranchId,
    int? VisibleUserId,
    int? OwnerSectionId,
    string? Cursor) : IQuery<PagedResult<DocumentListItemDto>>;

internal sealed class GetStrikeOffReminderListQueryHandler
    : QueryHandlerBase<GetStrikeOffReminderListQuery, PagedResult<DocumentListItemDto>>
{
    private readonly IDocumentRepository _documents;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IKeysetCursorCodec _cursorCodec;

    public GetStrikeOffReminderListQueryHandler(
        IDocumentRepository documents,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IKeysetCursorCodec cursorCodec,
        IEnumerable<IPipelineBehavior<GetStrikeOffReminderListQuery, PagedResult<DocumentListItemDto>>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _clock = clock;
        _timeZone = timeZone;
        _cursorCodec = cursorCodec;
    }

    protected override async Task<PagedResult<DocumentListItemDto>> HandleCoreAsync(
        GetStrikeOffReminderListQuery request, CancellationToken ct)
    {
        var page = request.Page;
        var perPage = request.PerPage;
        var visibleBranchId = request.VisibleBranchId;
        var visibleUserId = request.VisibleUserId;
        var ownerSectionId = request.OwnerSectionId;
        var cursor = request.Cursor;

        var (safePage, safePerPage) = StrikeOffReminderReadHelpers.NormalizePaging(page, perPage);
        var cutoff = StrikeOffReminderCalculator.CutoffUtc(ServerClock.Now(_clock, _timeZone), _timeZone);

        var keyset = _cursorCodec.DecodeOptional(cursor, DocumentListCursorKinds.ReminderLastActivity);
        var (total, rows, hasMore) = await _documents.SearchStrikeOffReminderIdsAsync(
            cutoff, visibleBranchId, visibleUserId, safePage, safePerPage, ct, ownerSectionId, keyset);

        var items = new List<DocumentListItemDto>(rows.Count);
        if (rows.Count > 0)
        {
            // الترتيب من SQL (الأقدم ركودًا) يُعاد صريحًا بعد التحميل — التحميل لا يضمن الترتيب.
            var ids = rows.Select(r => r.Id).ToList();
            var byId = (await _documents.GetListRowsByIdsAsync(ids, ct))
                .ToDictionary(d => d.Id);
            var currentYear = ServerClock.CurrentYear(_clock, _timeZone);

            foreach (var row in rows)
            {
                if (byId.TryGetValue(row.Id, out var listRow))
                    items.Add(DocumentListItemDto.FromRow(listRow, currentYear));
            }
        }

        string? nextCursor = null;
        if (hasMore && rows.Count > 0)
        {
            var last = rows[^1];
            nextCursor = _cursorCodec.Encode(
                DocumentListCursorKinds.ReminderLastActivity, last.LastActivityUtc, last.Id);
        }

        return new PagedResult<DocumentListItemDto>
        {
            Items = items,
            Page = cursor is null ? safePage : 1,
            PerPage = safePerPage,
            TotalCount = total,
            NextCursor = nextCursor,
        };
    }
}
