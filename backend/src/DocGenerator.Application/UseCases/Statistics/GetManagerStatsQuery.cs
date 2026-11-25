using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Statistics;

/// <summary>بطاقات إحصاءات المدير لفترة ونطاق مالك محددين.</summary>
public sealed record GetManagerStatsQuery(
    StatsPeriod Period,
    int? BranchId,
    int? Year = null,
    int? Month = null,
    int? Quarter = null,
    int? OwnerSectionId = null,
    bool FullAccess = true) : IQuery<ManagerStatsDto>;

internal sealed class GetManagerStatsQueryHandler
    : QueryHandlerBase<GetManagerStatsQuery, ManagerStatsDto>
{
    private readonly IStatisticsRepository _stats;

    public GetManagerStatsQueryHandler(
        IStatisticsRepository stats,
        IEnumerable<IPipelineBehavior<GetManagerStatsQuery, ManagerStatsDto>> behaviors)
        : base(behaviors)
        => _stats = stats;

    protected override Task<ManagerStatsDto> HandleCoreAsync(
        GetManagerStatsQuery query, CancellationToken ct)
        => _stats.GetManagerStatsAsync(
            query.Period,
            query.BranchId,
            query.Year,
            query.Month,
            query.Quarter,
            ct,
            query.OwnerSectionId,
            query.FullAccess);
}
