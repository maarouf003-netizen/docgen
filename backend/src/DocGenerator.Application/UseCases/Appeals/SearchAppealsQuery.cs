using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Appeals;

/// <summary>بحث/قائمة الاستئنافات لنطاق رؤية محدد (صفحة «الاستئنافات» — §5).</summary>
public sealed record SearchAppealsQuery(
    string? Query, string? Status, int? VisibleBranchId, int? VisibleUserId,
    int? OwnerSectionId, int Page, int PerPage) : IQuery<PagedResult<AppealDto>>;

internal sealed class SearchAppealsQueryHandler
    : QueryHandlerBase<SearchAppealsQuery, PagedResult<AppealDto>>
{
    private readonly IAppealRepository _appeals;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public SearchAppealsQueryHandler(
        IAppealRepository appeals,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<SearchAppealsQuery, PagedResult<AppealDto>>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override Task<PagedResult<AppealDto>> HandleCoreAsync(
        SearchAppealsQuery query, CancellationToken ct)
    {
        var (page, perPage) = PageRequest.Normalize(query.Page, query.PerPage);
        return SearchCoreAsync(
            query.Query, query.Status, query.VisibleBranchId, query.VisibleUserId,
            query.OwnerSectionId, page, perPage, ct);
    }

    private async Task<PagedResult<AppealDto>> SearchCoreAsync(
        string? query, string? status, int? visibleBranchId, int? visibleUserId,
        int? ownerSectionId, int page, int perPage, CancellationToken ct)
    {
        var (total, items) = await _appeals.SearchAsync(
            query, status, visibleBranchId, visibleUserId, ownerSectionId, page, perPage, ct);

        return new PagedResult<AppealDto>
        {
            Items = items.Select(a => DocumentAppealService.ToDto(a, ServerClock.CurrentYear(_clock, _timeZone))).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }
}
