using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Statistics;

/// <summary>بطاقات إحصاءات الفترة لملفات مستخدم محدد.</summary>
public sealed record GetPersonalStatsQuery(
    StatsPeriod Period,
    int UserId,
    int? Year = null,
    int? Month = null,
    int? Quarter = null) : IQuery<ManagerStatsDto>;

internal sealed class GetPersonalStatsQueryHandler
    : QueryHandlerBase<GetPersonalStatsQuery, ManagerStatsDto>
{
    private readonly IStatisticsRepository _stats;

    public GetPersonalStatsQueryHandler(
        IStatisticsRepository stats,
        IEnumerable<IPipelineBehavior<GetPersonalStatsQuery, ManagerStatsDto>> behaviors)
        : base(behaviors)
        => _stats = stats;

    protected override Task<ManagerStatsDto> HandleCoreAsync(
        GetPersonalStatsQuery query, CancellationToken ct)
        => _stats.GetPersonalStatsAsync(
            query.Period, query.UserId, query.Year, query.Month, query.Quarter, ct);
}
