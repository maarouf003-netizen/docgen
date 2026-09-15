using System.Net;
using System.Text.Json;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Api.Tests;

/// <summary>
/// نقطة نهاية الساعة المركزية GET /api/meta/current-year: متاحة مجهولًا ولمندوب الجهة،
/// وتعيد سنة «قرار السنة» الحالية وفق منطقة النظام المقررة في الإعدادات (Asia/Damascus).
/// </summary>
public class MetaControllerTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public MetaControllerTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CurrentYear_AnonymousRequest_ReturnsOkWithYear()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/meta/current-year");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var currentYear = doc.RootElement.GetProperty("currentYear").GetInt32();
        Assert.InRange(currentYear, 2020, 2100);
    }

    [Fact]
    public async Task CurrentYear_MatchesServerClockInConfiguredZone()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/meta/current-year");

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var currentYear = doc.RootElement.GetProperty("currentYear").GetInt32();
        var expected = Application.Common.ServerClock.CurrentYear(
            TimeProvider.System, Application.Common.ServerClock.ResolveTimeZone("Asia/Damascus"));
        Assert.Equal(expected, currentYear);
    }

    [Fact]
    public async Task CurrentYear_EntityManagerUser_IsAllowedThroughPortalGuard()
    {
        var user = await _factory.CreateUserAsync("meta_entitymanager", UserRole.EntityManager);
        var login = await _factory.LoginAsync(user.Username, "123456");
        Assert.NotNull(login);
        Assert.True(login.StatusCode == 200, $"Login {login.StatusCode}: {login.Content}");
        using var client = login.Client;

        var response = await client.GetAsync("/api/meta/current-year");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}