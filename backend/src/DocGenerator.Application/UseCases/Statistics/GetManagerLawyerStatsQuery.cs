using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Statistics;

/// <summary>جدول محامي فرع محدد بتوزيعهم الشهري داخل نطاق الفترة.</summary>
public sealed record GetManagerLawyerStatsQuery(
    StatsPeriod Period,
    int BranchId,
    int? Year = null,
    int? Month = null,
    int? Quarter = null,
    int? OwnerSectionId = null,
    bool FullAccess = true) : IQuery<List<ManagerLawyerStatDto>>;

internal sealed class GetManagerLawyerStatsQueryHandler
    : QueryHandlerBase<GetManagerLawyerStatsQuery, List<ManagerLawyerStatDto>>
{
    private readonly IStatisticsRepository _stats;

    public GetManagerLawyerStatsQueryHandler(
        IStatisticsRepository stats,
        IEnumerable<IPipelineBehavior<GetManagerLawyerStatsQuery, List<ManagerLawyerStatDto>>> behaviors)
        : base(behaviors)
        => _stats = stats;

    protected override Task<List<ManagerLawyerStatDto>> HandleCoreAsync(
        GetManagerLawyerStatsQuery query, CancellationToken ct)
        => _stats.GetManagerLawyerStatsAsync(
            query.Period,
            query.BranchId,
            query.Year,
            query.Month,
            query.Quarter,
            ct,
            query.OwnerSectionId,
            query.FullAccess);
}
