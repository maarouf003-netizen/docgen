using System.Net;
using System.Net.Http.Json;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Api.Tests;

public class ManagerStatsIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ManagerStatsIntegrationTests(ApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("head1")]
    [InlineData("lawyer1")]
    public async Task ManagerStats_Endpoints_ForbiddenForNonManagerRoles(string username)
    {
        var client = _factory.AuthorizedClient(username);

        var stats = await client.GetAsync("/api/stats/manager?period=monthly");
        Assert.Equal(HttpStatusCode.Forbidden, stats.StatusCode);

        var lawyers = await client.GetAsync("/api/stats/manager/lawyers?period=monthly&branchId=1");
        Assert.Equal(HttpStatusCode.Forbidden, lawyers.StatusCode);
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
