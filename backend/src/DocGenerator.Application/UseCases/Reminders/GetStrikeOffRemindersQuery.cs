using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Reminders;

/// <summary>
/// بطاقة «تذكيرات الشطب» بالكيان الكامل: ملفات «طالبة تنفيذ» النشطة التي مضى على
/// آخر إجراء فيها 5 أشهر — النطاق يمرره المتحكم من الرمز.
/// </summary>
public sealed record GetStrikeOffRemindersQuery(
    int Page,
    int PerPage,
    int? VisibleBranchId,
    int? VisibleUserId,
    int? OwnerSectionId) : IQuery<PagedResult<DocumentResponse>>;

internal sealed class GetStrikeOffRemindersQueryHandler
    : QueryHandlerBase<GetStrikeOffRemindersQuery, PagedResult<DocumentResponse>>
{
    private readonly IDocumentRepository _documents;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public GetStrikeOffRemindersQueryHandler(
        IDocumentRepository documents,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<GetStrikeOffRemindersQuery, PagedResult<DocumentResponse>>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<PagedResult<DocumentResponse>> HandleCoreAsync(
        GetStrikeOffRemindersQuery request, CancellationToken ct)
    {
        var page = request.Page;
        var perPage = request.PerPage;
        var visibleBranchId = request.VisibleBranchId;
        var visibleUserId = request.VisibleUserId;
        var ownerSectionId = request.OwnerSectionId;

        var (safePage, safePerPage) = StrikeOffReminderReadHelpers.NormalizePaging(page, perPage);
        var cutoff = StrikeOffReminderCalculator.CutoffUtc(ServerClock.Now(_clock, _timeZone), _timeZone);

        var (total, rows, _) = await _documents.SearchStrikeOffReminderIdsAsync(
            cutoff, visibleBranchId, visibleUserId, safePage, safePerPage, ct, ownerSectionId);

        var items = new List<DocumentResponse>(rows.Count);
        if (rows.Count > 0)
        {
            // الترتيب من SQL (الأقدم ركودًا) يُعاد صريحًا بعد التحميل — التحميل لا يضمن الترتيب.
            var ids = rows.Select(r => r.Id).ToList();
            var docs = (await _documents.GetByIdsWithStandardIncludesAsync(ids, ct))
                .ToDictionary(d => d.Id);
            var currentYear = ServerClock.CurrentYear(_clock, _timeZone);

            foreach (var row in rows)
            {
                if (docs.TryGetValue(row.Id, out var doc))
                    items.Add(DocumentResponse.FromEntity(doc, currentYear));
            }
        }

        return new PagedResult<DocumentResponse>
        {
            Items = items,
            Page = safePage,
            PerPage = safePerPage,
            TotalCount = total,
        };
    }
}
