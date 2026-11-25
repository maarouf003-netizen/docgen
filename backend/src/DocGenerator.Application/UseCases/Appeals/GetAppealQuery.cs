using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Appeals;

/// <summary>تفاصيل استئناف بمعرفه.</summary>
public sealed record GetAppealQuery(int AppealId) : IQuery<AppealDto?>;

internal sealed class GetAppealQueryHandler
    : QueryHandlerBase<GetAppealQuery, AppealDto?>
{
    private readonly IAppealRepository _appeals;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public GetAppealQueryHandler(
        IAppealRepository appeals,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<GetAppealQuery, AppealDto?>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<AppealDto?> HandleCoreAsync(
        GetAppealQuery query, CancellationToken ct)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(query.AppealId, ct);
        return appeal is null ? null : DocumentAppealService.ToDto(appeal, ServerClock.CurrentYear(_clock, _timeZone));
    }
}
