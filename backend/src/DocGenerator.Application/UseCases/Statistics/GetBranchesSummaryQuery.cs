using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Statistics;

/// <summary>ملخّص الإحصاءات لكل فرع.</summary>
public sealed record GetBranchesSummaryQuery : IQuery<List<BranchSummaryDto>>;

internal sealed class GetBranchesSummaryQueryHandler
    : QueryHandlerBase<GetBranchesSummaryQuery, List<BranchSummaryDto>>
{
    private readonly IStatisticsRepository _stats;

    public GetBranchesSummaryQueryHandler(
        IStatisticsRepository stats,
        IEnumerable<IPipelineBehavior<GetBranchesSummaryQuery, List<BranchSummaryDto>>> behaviors)
        : base(behaviors)
        => _stats = stats;

    protected override Task<List<BranchSummaryDto>> HandleCoreAsync(
        GetBranchesSummaryQuery query, CancellationToken ct)
        => _stats.GetBranchesSummaryAsync(ct);
}
