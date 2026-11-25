using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Statistics;

/// <summary>نشاط المستخدمين بأعداد ملفاتهم.</summary>
public sealed record GetUserActivityQuery : IQuery<List<UserActivityDto>>;

internal sealed class GetUserActivityQueryHandler
    : QueryHandlerBase<GetUserActivityQuery, List<UserActivityDto>>
{
    private readonly IStatisticsRepository _stats;

    public GetUserActivityQueryHandler(
        IStatisticsRepository stats,
        IEnumerable<IPipelineBehavior<GetUserActivityQuery, List<UserActivityDto>>> behaviors)
        : base(behaviors)
        => _stats = stats;

    protected override Task<List<UserActivityDto>> HandleCoreAsync(
        GetUserActivityQuery query, CancellationToken ct)
        => _stats.GetUserActivityAsync(ct);
}
