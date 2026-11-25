using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Tests.TestHelpers;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests;

/// <summary>
/// `P4-01cp`: إثبات أن كل معالج إحصاءات يمرّر وسائطه إلى مستودع الإحصاءات حرفيًا
/// ويعيد نتيجته كما هي — بلا لفّ أو تحويل أو قراءة إضافية.
/// </summary>
public sealed class StatisticsHandlerParityTests
{
    [Fact]
    public async Task Dashboard_ForwardsAllArgumentsVerbatim()
    {
        var repo = new RecordingStatisticsRepository();
        using var cts = new CancellationTokenSource();

        var result = await StatisticsReadHandlerFactory.Create(repo).Dashboard
            .GetDashboardStatsAsync(7, cts.Token, 3, false);

        var (method, args) = Assert.Single(repo.Calls);
        Assert.Equal("GetDashboardStatsAsync", method);
        Assert.Equal(new object?[] { 7, cts.Token, 3, false }, args);
        Assert.Null(result);
    }

    [Fact]
    public async Task Monthly_ForwardsAllArgumentsVerbatim()
    {
        var repo = new RecordingStatisticsRepository();
        using var cts = new CancellationTokenSource();

        var result = await StatisticsReadHandlerFactory.Create(repo).Monthly
            .GetMonthlyStatsAsync(4, cts.Token, 2, false);

        var (method, args) = Assert.Single(repo.Calls);
        Assert.Equal("GetMonthlyStatsAsync", method);
        Assert.Equal(new object?[] { 4, cts.Token, 2, false }, args);
        Assert.Null(result);
    }

    [Fact]
    public async Task BranchesSummary_ForwardsToken()
    {
        var repo = new RecordingStatisticsRepository();
        using var cts = new CancellationTokenSource();

        var result = await StatisticsReadHandlerFactory.Create(repo).BranchesSummary
            .GetBranchesSummaryAsync(cts.Token);

        var (method, args) = Assert.Single(repo.Calls);
        Assert.Equal("GetBranchesSummaryAsync", method);
        Assert.Equal(new object?[] { cts.Token }, args);
        Assert.Null(result);
    }

    [Fact]
    public async Task UserActivity_ForwardsToken()
    {
        var repo = new RecordingStatisticsRepository();
        using var cts = new CancellationTokenSource();

        var result = await StatisticsReadHandlerFactory.Create(repo).UserActivity
            .GetUserActivityAsync(cts.Token);

        var (method, args) = Assert.Single(repo.Calls);
        Assert.Equal("GetUserActivityAsync", method);
        Assert.Equal(new object?[] { cts.Token }, args);
        Assert.Null(result);
    }

    [Fact]
    public async Task Reminders_ForwardsUserAndToken()
    {
        var repo = new RecordingStatisticsRepository();
        using var cts = new CancellationTokenSource();

        var result = await StatisticsReadHandlerFactory.Create(repo).Reminders
            .GetRemindersAsync(11, cts.Token);

        var (method, args) = Assert.Single(repo.Calls);
        Assert.Equal("GetRemindersAsync", method);
        Assert.Equal(new object?[] { 11, cts.Token }, args);
        Assert.Null(result);
    }

    [Fact]
    public async Task ManagerStats_ForwardsPeriodScopeAndToken()
    {
        var repo = new RecordingStatisticsRepository();
        using var cts = new CancellationTokenSource();

        var result = await StatisticsReadHandlerFactory.Create(repo).ManagerStats
            .GetManagerStatsAsync(StatsPeriod.Monthly, 5, 2026, 8, null, cts.Token, 3, false);

        var (method, args) = Assert.Single(repo.Calls);
        Assert.Equal("GetManagerStatsAsync", method);
        Assert.Equal(
            new object?[] { StatsPeriod.Monthly, 5, 2026, 8, null, cts.Token, 3, false }, args);
        Assert.Null(result);
    }

    [Fact]
    public async Task ManagerLawyerStats_ForwardsPeriodScopeAndToken()
    {
        var repo = new RecordingStatisticsRepository();
        using var cts = new CancellationTokenSource();

        var result = await StatisticsReadHandlerFactory.Create(repo).ManagerLawyerStats
            .GetManagerLawyerStatsAsync(StatsPeriod.Quarterly, 5, 2026, null, 2, cts.Token, 3, false);

        var (method, args) = Assert.Single(repo.Calls);
        Assert.Equal("GetManagerLawyerStatsAsync", method);
        Assert.Equal(
            new object?[] { StatsPeriod.Quarterly, 5, 2026, null, 2, cts.Token, 3, false }, args);
        Assert.Null(result);
    }

    [Fact]
    public async Task PersonalStats_ForwardsPeriodUserAndToken()
    {
        var repo = new RecordingStatisticsRepository();
        using var cts = new CancellationTokenSource();

        var result = await StatisticsReadHandlerFactory.Create(repo).PersonalStats
            .GetPersonalStatsAsync(StatsPeriod.Yearly, 9, 2026, null, null, cts.Token);

        var (method, args) = Assert.Single(repo.Calls);
        Assert.Equal("GetPersonalStatsAsync", method);
        Assert.Equal(new object?[] { StatsPeriod.Yearly, 9, 2026, null, null, cts.Token }, args);
        Assert.Null(result);
    }

    [Fact]
    public async Task AvailablePeriods_ForwardsScopeAndToken()
    {
        var repo = new RecordingStatisticsRepository();
        using var cts = new CancellationTokenSource();

        var result = await StatisticsReadHandlerFactory.Create(repo).AvailablePeriods
            .GetAvailablePeriodsAsync(5, 9, cts.Token, 3, false);

        var (method, args) = Assert.Single(repo.Calls);
        Assert.Equal("GetAvailablePeriodsAsync", method);
        Assert.Equal(new object?[] { 5, 9, cts.Token, 3, false }, args);
        Assert.Null(result);
    }

    private sealed class RecordingStatisticsRepository : IStatisticsRepository
    {
        public List<(string Method, object?[] Args)> Calls { get; } = [];

        public Task<DashboardStatsDto> GetDashboardStatsAsync(int? branchId, CancellationToken ct = default,
            int? ownerSectionId = null, bool fullAccess = true)
        {
            Calls.Add((nameof(GetDashboardStatsAsync), [branchId, ct, ownerSectionId, fullAccess]));
            return Task.FromResult<DashboardStatsDto>(null!);
        }

        public Task<List<MonthlyStatDto>> GetMonthlyStatsAsync(int? branchId, CancellationToken ct = default,
            int? ownerSectionId = null, bool fullAccess = true)
        {
            Calls.Add((nameof(GetMonthlyStatsAsync), [branchId, ct, ownerSectionId, fullAccess]));
            return Task.FromResult<List<MonthlyStatDto>>(null!);
        }

        public Task<List<BranchSummaryDto>> GetBranchesSummaryAsync(CancellationToken ct = default)
        {
            Calls.Add((nameof(GetBranchesSummaryAsync), [ct]));
            return Task.FromResult<List<BranchSummaryDto>>(null!);
        }

        public Task<List<UserActivityDto>> GetUserActivityAsync(CancellationToken ct = default)
        {
            Calls.Add((nameof(GetUserActivityAsync), [ct]));
            return Task.FromResult<List<UserActivityDto>>(null!);
        }

        public Task<List<ReminderDto>> GetRemindersAsync(int userId, CancellationToken ct = default)
        {
            Calls.Add((nameof(GetRemindersAsync), [userId, ct]));
            return Task.FromResult<List<ReminderDto>>(null!);
        }

        public Task<ManagerStatsDto> GetManagerStatsAsync(StatsPeriod period, int? branchId,
            int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default,
            int? ownerSectionId = null, bool fullAccess = true)
        {
            Calls.Add((nameof(GetManagerStatsAsync),
                [period, branchId, year, month, quarter, ct, ownerSectionId, fullAccess]));
            return Task.FromResult<ManagerStatsDto>(null!);
        }

        public Task<List<ManagerLawyerStatDto>> GetManagerLawyerStatsAsync(StatsPeriod period, int branchId,
            int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default,
            int? ownerSectionId = null, bool fullAccess = true)
        {
            Calls.Add((nameof(GetManagerLawyerStatsAsync),
                [period, branchId, year, month, quarter, ct, ownerSectionId, fullAccess]));
            return Task.FromResult<List<ManagerLawyerStatDto>>(null!);
        }

        public Task<ManagerStatsDto> GetPersonalStatsAsync(StatsPeriod period, int userId,
            int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default)
        {
            Calls.Add((nameof(GetPersonalStatsAsync), [period, userId, year, month, quarter, ct]));
            return Task.FromResult<ManagerStatsDto>(null!);
        }

        public Task<List<MonthlyStatDto>> GetAvailablePeriodsAsync(int? branchId, int? userId,
            CancellationToken ct = default, int? ownerSectionId = null, bool fullAccess = true)
        {
            Calls.Add((nameof(GetAvailablePeriodsAsync),
                [branchId, userId, ct, ownerSectionId, fullAccess]));
            return Task.FromResult<List<MonthlyStatDto>>(null!);
        }
    }
}
