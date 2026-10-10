using DocGenerator.Application.DTOs;
using DocGenerator.Application.Common.Interfaces;

namespace DocGenerator.Application.Services;

public interface IStatisticsService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(int? branchId, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true);
    Task<List<MonthlyStatDto>> GetMonthlyStatsAsync(int? branchId, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true);
    Task<List<BranchSummaryDto>> GetBranchesSummaryAsync(CancellationToken ct = default);
    Task<List<UserActivityDto>> GetUserActivityAsync(CancellationToken ct = default);
    Task<List<ReminderDto>> GetRemindersAsync(int userId, CancellationToken ct = default);
    Task<ManagerStatsDto> GetManagerStatsAsync(StatsPeriod period, int? branchId,
        int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true);
    Task<List<ManagerLawyerStatDto>> GetManagerLawyerStatsAsync(StatsPeriod period, int branchId,
        int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true);
    Task<ManagerStatsDto> GetPersonalStatsAsync(StatsPeriod period, int userId,
        int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default);
    Task<List<MonthlyStatDto>> GetAvailablePeriodsAsync(int? branchId, int? userId, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true);
}

public sealed class StatisticsService : IStatisticsService
{
    private readonly IStatisticsRepository _stats;

    public StatisticsService(IStatisticsRepository stats) => _stats = stats;

    public Task<DashboardStatsDto> GetDashboardStatsAsync(int? branchId, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true)
        => _stats.GetDashboardStatsAsync(branchId, ct, ownerSectionId, fullAccess);

    public Task<List<MonthlyStatDto>> GetMonthlyStatsAsync(int? branchId, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true)
        => _stats.GetMonthlyStatsAsync(branchId, ct, ownerSectionId, fullAccess);

    public Task<List<BranchSummaryDto>> GetBranchesSummaryAsync(CancellationToken ct = default)
        => _stats.GetBranchesSummaryAsync(ct);

    public Task<List<UserActivityDto>> GetUserActivityAsync(CancellationToken ct = default)
        => _stats.GetUserActivityAsync(ct);

    public Task<List<ReminderDto>> GetRemindersAsync(int userId, CancellationToken ct = default)
        => _stats.GetRemindersAsync(userId, ct);

    public Task<ManagerStatsDto> GetManagerStatsAsync(StatsPeriod period, int? branchId,
        int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true)
        => _stats.GetManagerStatsAsync(period, branchId, year, month, quarter, ct, ownerSectionId, fullAccess);

    public Task<List<ManagerLawyerStatDto>> GetManagerLawyerStatsAsync(StatsPeriod period, int branchId,
        int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true)
        => _stats.GetManagerLawyerStatsAsync(period, branchId, year, month, quarter, ct, ownerSectionId, fullAccess);

    public Task<ManagerStatsDto> GetPersonalStatsAsync(StatsPeriod period, int userId,
        int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default)
        => _stats.GetPersonalStatsAsync(period, userId, year, month, quarter, ct);

    public Task<List<MonthlyStatDto>> GetAvailablePeriodsAsync(int? branchId, int? userId, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true)
        => _stats.GetAvailablePeriodsAsync(branchId, userId, ct, ownerSectionId, fullAccess);
}
