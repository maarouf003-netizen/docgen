using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Statistics;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حزمة معالجات قراءات الإحصاءات التسعة (`P4-01cp`) على مستودع واحد، بدل تكرار
/// ربط السلوكيات الفارغة في كل اختبار.
/// </summary>
public sealed record StatisticsReadHandlers(
    IQueryHandler<GetDashboardStatsQuery, DashboardStatsDto> Dashboard,
    IQueryHandler<GetMonthlyStatsQuery, List<MonthlyStatDto>> Monthly,
    IQueryHandler<GetBranchesSummaryQuery, List<BranchSummaryDto>> BranchesSummary,
    IQueryHandler<GetUserActivityQuery, List<UserActivityDto>> UserActivity,
    IQueryHandler<GetRemindersQuery, List<ReminderDto>> Reminders,
    IQueryHandler<GetManagerStatsQuery, ManagerStatsDto> ManagerStats,
    IQueryHandler<GetManagerLawyerStatsQuery, List<ManagerLawyerStatDto>> ManagerLawyerStats,
    IQueryHandler<GetPersonalStatsQuery, ManagerStatsDto> PersonalStats,
    IQueryHandler<GetAvailablePeriodsQuery, List<MonthlyStatDto>> AvailablePeriods);

public static class StatisticsReadHandlerFactory
{
    public static StatisticsReadHandlers Create(IStatisticsRepository stats)
        => new(
            new GetDashboardStatsQueryHandler(
                stats,
                Array.Empty<IPipelineBehavior<GetDashboardStatsQuery, DashboardStatsDto>>()),
            new GetMonthlyStatsQueryHandler(
                stats,
                Array.Empty<IPipelineBehavior<GetMonthlyStatsQuery, List<MonthlyStatDto>>>()),
            new GetBranchesSummaryQueryHandler(
                stats,
                Array.Empty<IPipelineBehavior<GetBranchesSummaryQuery, List<BranchSummaryDto>>>()),
            new GetUserActivityQueryHandler(
                stats,
                Array.Empty<IPipelineBehavior<GetUserActivityQuery, List<UserActivityDto>>>()),
            new GetRemindersQueryHandler(
                stats,
                Array.Empty<IPipelineBehavior<GetRemindersQuery, List<ReminderDto>>>()),
            new GetManagerStatsQueryHandler(
                stats,
                Array.Empty<IPipelineBehavior<GetManagerStatsQuery, ManagerStatsDto>>()),
            new GetManagerLawyerStatsQueryHandler(
                stats,
                Array.Empty<IPipelineBehavior<GetManagerLawyerStatsQuery, List<ManagerLawyerStatDto>>>()),
            new GetPersonalStatsQueryHandler(
                stats,
                Array.Empty<IPipelineBehavior<GetPersonalStatsQuery, ManagerStatsDto>>()),
            new GetAvailablePeriodsQueryHandler(
                stats,
                Array.Empty<IPipelineBehavior<GetAvailablePeriodsQuery, List<MonthlyStatDto>>>()));
}
