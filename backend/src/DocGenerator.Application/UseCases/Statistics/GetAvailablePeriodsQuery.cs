using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Statistics;

/// <summary>الأشهر المتاحة التي قُيّدت فيها ملفات ضمن نطاق الفرع/المستخدم/المالك.</summary>
public sealed record GetAvailablePeriodsQuery(
    int? BranchId,
    int? UserId,
    int? OwnerSectionId = null,
    bool FullAccess = true) : IQuery<List<MonthlyStatDto>>;

internal sealed class GetAvailablePeriodsQueryHandler
    : QueryHandlerBase<GetAvailablePeriodsQuery, List<MonthlyStatDto>>
{
    private readonly IStatisticsRepository _stats;

    public GetAvailablePeriodsQueryHandler(
        IStatisticsRepository stats,
        IEnumerable<IPipelineBehavior<GetAvailablePeriodsQuery, List<MonthlyStatDto>>> behaviors)
        : base(behaviors)
        => _stats = stats;

    protected override Task<List<MonthlyStatDto>> HandleCoreAsync(
        GetAvailablePeriodsQuery query, CancellationToken ct)
        => _stats.GetAvailablePeriodsAsync(
            query.BranchId, query.UserId, ct, query.OwnerSectionId, query.FullAccess);
}
