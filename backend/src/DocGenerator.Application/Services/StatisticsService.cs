using DocGenerator.Application.DTOs;
using DocGenerator.Application.Common.Interfaces;

namespace DocGenerator.Application.Services;

public interface IStatisticsService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(int? branchId, CancellationToken ct = default);
    Task<List<MonthlyStatDto>> GetMonthlyStatsAsync(int? branchId, CancellationToken ct = default);
    Task<List<BranchSummaryDto>> GetBranchesSummaryAsync(CancellationToken ct = default);
    Task<List<UserActivityDto>> GetUserActivityAsync(CancellationToken ct = default);
    Task<List<ReminderDto>> GetRemindersAsync(int? branchId, int? userId, CancellationToken ct = default);
    Task<ManagerStatsDto> GetManagerStatsAsync(StatsPeriod period, int? branchId, CancellationToken ct = default);
    Task<List<ManagerLawyerStatDto>> GetManagerLawyerStatsAsync(StatsPeriod period, int branchId, CancellationToken ct = default);
}

public sealed class StatisticsService : IStatisticsService
{
    private readonly IStatisticsRepository _stats;

    public StatisticsService(IStatisticsRepository stats) => _stats = stats;

    public Task<DashboardStatsDto> GetDashboardStatsAsync(int? branchId, CancellationToken ct = default)
        => _stats.GetDashboardStatsAsync(branchId, ct);

    public Task<List<MonthlyStatDto>> GetMonthlyStatsAsync(int? branchId, CancellationToken ct = default)
        => _stats.GetMonthlyStatsAsync(branchId, ct);

    public Task<List<BranchSummaryDto>> GetBranchesSummaryAsync(CancellationToken ct = default)
        => _stats.GetBranchesSummaryAsync(ct);

    public Task<List<UserActivityDto>> GetUserActivityAsync(CancellationToken ct = default)
        => _stats.GetUserActivityAsync(ct);

    public Task<List<ReminderDto>> GetRemindersAsync(int? branchId, int? userId, CancellationToken ct = default)
        => _stats.GetRemindersAsync(branchId, userId, ct);

    public Task<ManagerStatsDto> GetManagerStatsAsync(StatsPeriod period, int? branchId, CancellationToken ct = default)
        => _stats.GetManagerStatsAsync(period, branchId, ct);

    public Task<List<ManagerLawyerStatDto>> GetManagerLawyerStatsAsync(StatsPeriod period, int branchId, CancellationToken ct = default)
        => _stats.GetManagerLawyerStatsAsync(period, branchId, ct);
}
