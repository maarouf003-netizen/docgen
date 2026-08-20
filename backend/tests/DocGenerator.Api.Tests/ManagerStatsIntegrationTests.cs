using System.Net;
using System.Net.Http.Json;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Api.Tests;

[Collection(ApiTestCollection.Name)]
public class ManagerStatsIntegrationTests
{
    private readonly ApiFactory _factory;

    public ManagerStatsIntegrationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ManagerStats_Endpoints_ForbiddenForLawyer()
    {
        var client = _factory.AuthorizedClient("lawyer1");

        var stats = await client.GetAsync("/api/stats/manager?period=monthly");
        Assert.Equal(HttpStatusCode.Forbidden, stats.StatusCode);

        var lawyers = await client.GetAsync("/api/stats/manager/lawyers?period=monthly&branchId=1");
        Assert.Equal(HttpStatusCode.Forbidden, lawyers.StatusCode);
    }

    [Fact]
    public async Task ManagerStats_Head_SeesOwnBranchStatsAndLawyers()
    {
        var head = _factory.AuthorizedClient("head1");

        var statsResponse = await head.GetAsync("/api/stats/manager?period=monthly");
        statsResponse.EnsureSuccessStatusCode();
        var stats = await statsResponse.Content.ReadFromJsonAsync<ManagerStatsDto>();
        Assert.NotNull(stats);

        // دون تمرير branchId: يُحتسب رئيس القسم على فرعه تلقائيًا.
        var lawyersResponse = await head.GetAsync("/api/stats/manager/lawyers?period=monthly");
        lawyersResponse.EnsureSuccessStatusCode();
        var lawyers = await lawyersResponse.Content.ReadFromJsonAsync<List<ManagerLawyerStatDto>>();
        Assert.NotNull(lawyers);
    }

    [Fact]
    public async Task ManagerStats_SpecificMonth_ReturnsSelectedPeriod()
    {
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.GetAsync("/api/stats/manager?period=monthly&year=2026&month=5");
        response.EnsureSuccessStatusCode();
        var stats = await response.Content.ReadFromJsonAsync<ManagerStatsDto>();

        Assert.NotNull(stats);
        Assert.Equal(2026, stats.PeriodYear);
        Assert.Equal(5, stats.PeriodMonth);
        Assert.Null(stats.PeriodQuarter);
    }

    [Fact]
    public async Task ManagerStats_SpecificQuarter_ReturnsSelectedQuarter()
    {
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.GetAsync("/api/stats/manager?period=quarterly&year=2026&quarter=2");
        response.EnsureSuccessStatusCode();
        var stats = await response.Content.ReadFromJsonAsync<ManagerStatsDto>();

        Assert.NotNull(stats);
        Assert.Equal(2026, stats.PeriodYear);
        Assert.Equal(2, stats.PeriodQuarter);
        Assert.Null(stats.PeriodMonth);
    }

    [Fact]
    public async Task ManagerStats_InvalidPeriodParams_ReturnBadRequest()
    {
        var manager = _factory.AuthorizedClient("manager");

        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync("/api/stats/manager?period=monthly&year=1800")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync("/api/stats/manager?period=monthly&month=13")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync("/api/stats/manager?period=quarterly&quarter=5")).StatusCode);
    }

    [Fact]
    public async Task PersonalStats_Lawyer_SeesOwnStatsForSelectedPeriod()
    {
        var lawyer = _factory.AuthorizedClient("lawyer1");
        var response = await lawyer.GetAsync("/api/stats/me?period=monthly&year=2026&month=5");
        response.EnsureSuccessStatusCode();
        var stats = await response.Content.ReadFromJsonAsync<ManagerStatsDto>();

        Assert.NotNull(stats);
        Assert.Equal(2026, stats.PeriodYear);
        Assert.Equal(5, stats.PeriodMonth);
    }

    [Fact]
    public async Task AvailablePeriods_ReturnsMonthsWithRegisteredFiles()
    {
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.GetAsync("/api/stats/periods");
        response.EnsureSuccessStatusCode();
        var periods = await response.Content.ReadFromJsonAsync<List<MonthlyStatDto>>();

        Assert.NotNull(periods);
        Assert.All(periods, p => Assert.True(p.Count >= 1));
        Assert.Equal(periods.OrderBy(p => p.Year).ThenBy(p => p.Month), periods);
    }

    [Fact]
    public async Task ManagerStats_CountsRegisteredDocumentsInCurrentMonth()
    {
        var today = DateTime.Today;
        var regDate = $"{today.Day}/{today.Month}/{today.Year}";

        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var lawyerClient = _factory.WithToken(token);
        var create = await lawyerClient.PostAsJsonAsync("/api/documents", new
        {
            documentType = "بيان دعوى",
            borrowerName = "مدير إحصاء",
            contractType = "تعهد",
            amountNumeric = 100,
            fileRegistrationDate = regDate,
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.GetAsync("/api/stats/manager?period=monthly");
        response.EnsureSuccessStatusCode();
        var stats = await response.Content.ReadFromJsonAsync<ManagerStatsDto>();

        Assert.NotNull(stats);
        Assert.True(stats.TotalFiles >= 1);
    }

    [Fact]
    public async Task ManagerStats_InvalidPeriod_ReturnsBadRequest()
    {
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.GetAsync("/api/stats/manager?period=weekly");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ManagerLawyerStats_RequiresBranchId()
    {
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.GetAsync("/api/stats/manager/lawyers?period=monthly");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ManagerLawyerStats_ReturnsBranchLawyers()
    {
        var manager = _factory.AuthorizedClient("manager");
        var summary = await manager.GetAsync("/api/branches/summary");
        summary.EnsureSuccessStatusCode();
        var branches = await summary.Content.ReadFromJsonAsync<List<BranchSummaryDto>>();
        Assert.NotNull(branches);
        var damascus = Assert.Single(branches.Where(b => b.BranchName.Contains("دمشق")));

        var response = await manager.GetAsync($"/api/stats/manager/lawyers?period=monthly&branchId={damascus.BranchId}");
        response.EnsureSuccessStatusCode();
        var lawyers = await response.Content.ReadFromJsonAsync<List<ManagerLawyerStatDto>>();

        Assert.NotNull(lawyers);
        Assert.Contains(lawyers, l => l.LawyerName == "محامي دمشق");
    }
}
