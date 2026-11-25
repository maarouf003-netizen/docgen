using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Statistics;

/// <summary>السلسلة الشهرية ضمن نطاق الفرع/المالك الممرَّر.</summary>
public sealed record GetMonthlyStatsQuery(
    int? BranchId,
    int? OwnerSectionId = null,
    bool FullAccess = true) : IQuery<List<MonthlyStatDto>>;

internal sealed class GetMonthlyStatsQueryHandler
    : QueryHandlerBase<GetMonthlyStatsQuery, List<MonthlyStatDto>>
{
    private readonly IStatisticsRepository _stats;

    public GetMonthlyStatsQueryHandler(
        IStatisticsRepository stats,
        IEnumerable<IPipelineBehavior<GetMonthlyStatsQuery, List<MonthlyStatDto>>> behaviors)
        : base(behaviors)
        => _stats = stats;

    protected override Task<List<MonthlyStatDto>> HandleCoreAsync(
        GetMonthlyStatsQuery query, CancellationToken ct)
        => _stats.GetMonthlyStatsAsync(query.BranchId, ct, query.OwnerSectionId, query.FullAccess);
}
