using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Appeals;

/// <summary>تاريخ أرقام الأساس الاستئنافية لكل السنوات.</summary>
public sealed record GetAppealBaseNumberHistoryQuery(int AppealId) : IQuery<List<AppealBaseNumberHistoryDto>>;

internal sealed class GetAppealBaseNumberHistoryQueryHandler
    : QueryHandlerBase<GetAppealBaseNumberHistoryQuery, List<AppealBaseNumberHistoryDto>>
{
    private readonly IAppealRepository _appeals;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public GetAppealBaseNumberHistoryQueryHandler(
        IAppealRepository appeals,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<GetAppealBaseNumberHistoryQuery, List<AppealBaseNumberHistoryDto>>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<List<AppealBaseNumberHistoryDto>> HandleCoreAsync(
        GetAppealBaseNumberHistoryQuery query, CancellationToken ct)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(query.AppealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        return AppealBaseNumberPolicy.BuildHistory(appeal, currentYear)
            .Select(h => new AppealBaseNumberHistoryDto(h.Year, h.Number, h.IsCurrent))
            .ToList();
    }
}
