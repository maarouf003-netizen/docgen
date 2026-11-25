using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Statistics;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسور اختبارية لمعالجات قراءات الإحصاءات (`P4-01cp`): تحافظ على توقيعات
/// `IStatisticsService` القديمة فوق معالجات الاستعلام — منطق الاختبارات كما هو.
/// </summary>
public static class StatisticsReadQueryExtensions
{
    public static Task<DashboardStatsDto> GetDashboardStatsAsync(
        this IQueryHandler<GetDashboardStatsQuery, DashboardStatsDto> handler,
        int? branchId,
        CancellationToken ct = default,
        int? ownerSectionId = null,
        bool fullAccess = true)
        => handler.HandleAsync(new GetDashboardStatsQuery(branchId, ownerSectionId, fullAccess), ct);

    public static Task<List<MonthlyStatDto>> GetMonthlyStatsAsync(
        this IQueryHandler<GetMonthlyStatsQuery, List<MonthlyStatDto>> handler,
        int? branchId,
        CancellationToken ct = default,
        int? ownerSectionId = null,
        bool fullAccess = true)
        => handler.HandleAsync(new GetMonthlyStatsQuery(branchId, ownerSectionId, fullAccess), ct);

    public static Task<List<BranchSummaryDto>> GetBranchesSummaryAsync(
        this IQueryHandler<GetBranchesSummaryQuery, List<BranchSummaryDto>> handler,
        CancellationToken ct = default)
        => handler.HandleAsync(new GetBranchesSummaryQuery(), ct);

    public static Task<List<UserActivityDto>> GetUserActivityAsync(
        this IQueryHandler<GetUserActivityQuery, List<UserActivityDto>> handler,
        CancellationToken ct = default)
        => handler.HandleAsync(new GetUserActivityQuery(), ct);

    public static Task<List<ReminderDto>> GetRemindersAsync(
        this IQueryHandler<GetRemindersQuery, List<ReminderDto>> handler,
        int userId,
        CancellationToken ct = default)
        => handler.HandleAsync(new GetRemindersQuery(userId), ct);

    public static Task<ManagerStatsDto> GetManagerStatsAsync(
        this IQueryHandler<GetManagerStatsQuery, ManagerStatsDto> handler,
        StatsPeriod period,
        int? branchId,
        int? year = null,
        int? month = null,
        int? quarter = null,
        CancellationToken ct = default,
        int? ownerSectionId = null,
        bool fullAccess = true)
        => handler.HandleAsync(
            new GetManagerStatsQuery(period, branchId, year, month, quarter, ownerSectionId, fullAccess), ct);

    public static Task<List<ManagerLawyerStatDto>> GetManagerLawyerStatsAsync(
        this IQueryHandler<GetManagerLawyerStatsQuery, List<ManagerLawyerStatDto>> handler,
        StatsPeriod period,
        int branchId,
        int? year = null,
        int? month = null,
        int? quarter = null,
        CancellationToken ct = default,
        int? ownerSectionId = null,
        bool fullAccess = true)
        => handler.HandleAsync(
            new GetManagerLawyerStatsQuery(period, branchId, year, month, quarter, ownerSectionId, fullAccess), ct);

    public static Task<ManagerStatsDto> GetPersonalStatsAsync(
        this IQueryHandler<GetPersonalStatsQuery, ManagerStatsDto> handler,
        StatsPeriod period,
        int userId,
        int? year = null,
        int? month = null,
        int? quarter = null,
        CancellationToken ct = default)
        => handler.HandleAsync(
            new GetPersonalStatsQuery(period, userId, year, month, quarter), ct);

    public static Task<List<MonthlyStatDto>> GetAvailablePeriodsAsync(
        this IQueryHandler<GetAvailablePeriodsQuery, List<MonthlyStatDto>> handler,
        int? branchId,
        int? userId,
        CancellationToken ct = default,
        int? ownerSectionId = null,
        bool fullAccess = true)
        => handler.HandleAsync(
            new GetAvailablePeriodsQuery(branchId, userId, ownerSectionId, fullAccess), ct);
}
