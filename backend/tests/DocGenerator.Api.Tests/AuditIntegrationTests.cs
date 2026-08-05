using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DocGenerator.Api.Tests;

public class AuditIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuditIntegrationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AuditLogs_ForLawyer_Forbidden_ForManager_Ok()
    {
        var lawyer = _factory.AuthorizedClient("lawyer1");
        var forbidden = await lawyer.GetAsync("/api/audit-logs");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var manager = _factory.AuthorizedClient("manager");
        var ok = await manager.GetAsync("/api/audit-logs?page=1&perPage=20");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task AuditLogs_ContainsLoginEvent_AfterLogin()
    {
        await _factory.LoginAsync("admin", "123456");

        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.GetAsync("/api/audit-logs?userName=admin&perPage=50");
        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var items = body!.RootElement.GetProperty("items");

        var hasLogin = items.EnumerateArray()
            .Any(e => e.GetProperty("actionType").GetString() == "login");
        Assert.True(hasLogin);

        body.Dispose();
    }

    [Fact]
    public async Task AuditLogs_ContainsCreateEvent_AfterCreatingDocument()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        await _factory.CreateDocumentAsync(token, "مقترض تدقيق");

        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.GetAsync("/api/audit-logs?userName=lawyer1&perPage=50");
        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var items = body!.RootElement.GetProperty("items");

        var hasCreate = items.EnumerateArray()
            .Any(e => e.GetProperty("actionType").GetString() == "create");
        Assert.True(hasCreate);

        body.Dispose();
    }
}

public class StatisticsIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public StatisticsIntegrationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Dashboard_ReturnsStats()
    {
        var client = _factory.AuthorizedClient("manager");
        var response = await client.GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.True(body!.RootElement.GetProperty("totalDocuments").GetInt32() >= 0);
        body.Dispose();
    }

    [Fact]
    public async Task BranchesSummary_ForLawyer_Forbidden()
    {
        var client = _factory.AuthorizedClient("lawyer1");
        var response = await client.GetAsync("/api/branches/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserActivity_ForManager_Ok()
    {
        var client = _factory.AuthorizedClient("manager");
        var response = await client.GetAsync("/api/users/activity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
