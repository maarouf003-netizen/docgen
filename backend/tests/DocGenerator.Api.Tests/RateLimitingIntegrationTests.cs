using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DocGenerator.Api.Tests;

/// <summary>
/// حد المعدل العام (S4): كل سياسة تُختبر على مصنع معزول بحد منخفض (المصنع المشترك
/// سقوفه مرفوعة عمدًا حتى لا يخنق سير الاختبارات — انظر <c>ApiFactory</c>).
/// </summary>
[Collection(ApiTestCollection.Name)]
public class RateLimitingIntegrationTests
{
    private readonly ApiFactory _factory;

    public RateLimitingIntegrationTests(ApiFactory factory) => _factory = factory;

    private WebApplicationFactory<Program> IsolatedFactory(string key, string value)
        => _factory.WithWebHostBuilder(b => b.UseSetting(key, value));

    private static async Task<HttpClient> AuthedClientAsync(
        WebApplicationFactory<Program> factory, string username, string password)
    {
        var probe = factory.CreateClient();
        var login = await probe.PostAsJsonAsync("/api/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = ApiFactory.ExtractCookieValue(login, "docgen_token");
        var csrf = ApiFactory.ExtractCookieValue(login, "docgen_csrf");
        Assert.NotNull(token);
        Assert.NotNull(csrf);

        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Add("Cookie", $"docgen_token={token}; docgen_csrf={csrf}");
        authed.DefaultRequestHeaders.Add("X-CSRF-Token", csrf);
        return authed;
    }

    [Fact]
    public async Task LoginIpPolicy_BlocksFloodWith429()
    {
        using var factory = IsolatedFactory("RateLimiting:LoginIpPerMinute", "2");
        var client = factory.CreateClient();

        for (var i = 0; i < 2; i++)
        {
            // أسماء مميزة حتى لا يتدخل قفل الحساب (لكل حساب) في قياس حد الـ IP.
            var denied = await client.PostAsJsonAsync("/api/auth/login",
                new { username = $"rl_nonexistent_{Guid.NewGuid():N}", password = "wrong" });
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }

        var flooded = await client.PostAsJsonAsync("/api/auth/login",
            new { username = $"rl_nonexistent_{Guid.NewGuid():N}", password = "wrong" });
        Assert.Equal((HttpStatusCode)429, flooded.StatusCode);
        Assert.Contains("طلبات كثيرة", await flooded.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GlobalAnonLimiter_BlocksFloodWith429()
    {
        using var factory = IsolatedFactory("RateLimiting:GeneralAnonPerMinute", "2");
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/meta/current-year")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/meta/current-year")).StatusCode);

        var flooded = await client.GetAsync("/api/meta/current-year");
        Assert.Equal((HttpStatusCode)429, flooded.StatusCode);
    }

    [Fact]
    public async Task ExpensivePolicy_BlocksFloodWith429()
    {
        using var factory = IsolatedFactory("RateLimiting:ExpensivePerMinute", "2");
        var client = await AuthedClientAsync(factory, "admin", "123456");

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/dashboard")).StatusCode);

        var flooded = await client.GetAsync("/api/dashboard");
        Assert.Equal((HttpStatusCode)429, flooded.StatusCode);
    }

    [Fact]
    public async Task PasswordPolicy_BlocksFloodWith429()
    {
        var username = $"rl_pwd_{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(username, DocGenerator.Domain.Enums.UserRole.Lawyer, password: "123456");

        using var factory = IsolatedFactory("RateLimiting:PasswordPerMinute", "2");
        var client = await AuthedClientAsync(factory, username, "123456");

        for (var i = 0; i < 2; i++)
        {
            var denied = await client.PostAsJsonAsync("/api/auth/change-password",
                new { oldPassword = "wrong", newPassword = "654321" });
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        }

        var flooded = await client.PostAsJsonAsync("/api/auth/change-password",
            new { oldPassword = "wrong", newPassword = "654321" });
        Assert.Equal((HttpStatusCode)429, flooded.StatusCode);
    }

    /// <summary>
    /// التقسيم لكل مستخدم حقيقي (لا IP مشترك): استنفاد مستخدم لدلوه لا يمسّ دلو مستخدم آخر
    /// على نفس عنوان الخادم الاختباري — يفشل هذا الاختبار إن وُضع <c>UseRateLimiter</c> قبل المصادقة.
    /// </summary>
    [Fact]
    public async Task ExpensivePolicy_PartitionsPerUser()
    {
        using var factory = IsolatedFactory("RateLimiting:ExpensivePerMinute", "2");
        var admin = await AuthedClientAsync(factory, "admin", "123456");
        var manager = await AuthedClientAsync(factory, "manager", "123456");

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/dashboard")).StatusCode);
        Assert.Equal((HttpStatusCode)429, (await admin.GetAsync("/api/dashboard")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/dashboard")).StatusCode);
    }
}
