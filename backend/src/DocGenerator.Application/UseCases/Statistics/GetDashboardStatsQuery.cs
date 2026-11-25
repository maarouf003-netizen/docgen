using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Statistics;

/// <summary>بطاقة اللوحة الإجمالية ضمن نطاق الفرع/المالك الممرَّر.</summary>
public sealed record GetDashboardStatsQuery(
    int? BranchId,
    int? OwnerSectionId = null,
    bool FullAccess = true) : IQuery<DashboardStatsDto>;

internal sealed class GetDashboardStatsQueryHandler
    : QueryHandlerBase<GetDashboardStatsQuery, DashboardStatsDto>
{
    private readonly IStatisticsRepository _stats;

    public GetDashboardStatsQueryHandler(
        IStatisticsRepository stats,
        IEnumerable<IPipelineBehavior<GetDashboardStatsQuery, DashboardStatsDto>> behaviors)
        : base(behaviors)
        => _stats = stats;

    protected override Task<DashboardStatsDto> HandleCoreAsync(
        GetDashboardStatsQuery query, CancellationToken ct)
        => _stats.GetDashboardStatsAsync(query.BranchId, ct, query.OwnerSectionId, query.FullAccess);
}
