using System.Net;
using System.Text;
using System.Text.Json;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Api.Tests;

/// <summary>
/// نقطة إبلاغ أخطاء الواجهة (<c>POST /api/client-errors</c>): موثقة حصرًا، مخنوقة لكل مستخدم،
/// بسقوف طول، ورد <c>202</c> بلا صدى. ملاحظة طبقية: <c>CsrfMiddleware</c> يسبق المصادقة فيرفض
/// أي طلب بلا زوج CSRF صالح بـ <c>403</c> — لذا يرفق اختبار المجهول زوجًا صالحًا ليصل إلى
/// <c>[Authorize]</c> ويُقاس <c>401</c> الحقيقي. الخنق لكل مستخدم لذا كل اختبار ينشئ مستخدمًا
/// فريدًا (المصنع والذاكرة مشتركان داخل المجموعة).
/// </summary>
[Collection(ApiTestCollection.Name)]
public class ClientErrorsIntegrationTests
{
    private readonly ApiFactory _factory;

    public ClientErrorsIntegrationTests(ApiFactory factory) => _factory = factory;

    private static StringContent JsonBody(object payload)
        => new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static HttpClient AnonymousClientWithCsrfPair(ApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "docgen_csrf=test-csrf-pair");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", "test-csrf-pair");
        return client;
    }

    private async Task<HttpClient> FreshUserClientAsync(UserRole role = UserRole.Lawyer)
    {
        var username = $"clienterr_{Guid.NewGuid():N}";
        await _factory.CreateUserAsync(username, role);
        var login = await _factory.LoginAsync(username, "123456");
        Assert.NotNull(login?.Token);
        return login!.Client;
    }

    [Fact]
    public async Task Anonymous_WithValidCsrfPair_Returns401()
    {
        var client = AnonymousClientWithCsrfPair(_factory);

        var response = await client.PostAsync("/api/client-errors",
            JsonBody(new { message = "boom", component = "probe", url = "/x" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_ValidReport_Returns202WithoutEcho()
    {
        var client = await FreshUserClientAsync();

        var response = await client.PostAsync("/api/client-errors",
            JsonBody(new { message = "عطل عرض في صفحة", component = "DocumentsList", url = "/documents" }));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.DoesNotContain("عطل عرض", body);
    }

    [Fact]
    public async Task MultilineMessageWithStack_Returns202WithoutEcho()
    {
        var client = await FreshUserClientAsync();

        var response = await client.PostAsync("/api/client-errors",
            JsonBody(new
            {
                message = "سطر أول\nسطر ثانٍ\r\nسطر ثالث",
                stack = "Error: boom\n    at render (/app/x.js:1:2)",
                component = "Probe",
                url = "/docs?token=secret#frag",
            }));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.DoesNotContain("سطر أول", body);
        Assert.DoesNotContain("secret", body);
    }

    [Fact]
    public async Task OversizedMessage_Returns400()
    {
        var client = await FreshUserClientAsync();

        var response = await client.PostAsync("/api/client-errors",
            JsonBody(new { message = new string('x', 3000) }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FloodBeyondPerMinuteLimit_Returns429()
    {
        var client = await FreshUserClientAsync();
        HttpResponseMessage? last = null;

        for (var i = 0; i < 31; i++)
        {
            last?.Dispose();
            last = await client.PostAsync("/api/client-errors",
                JsonBody(new { message = $"فيض {i}" }));
        }

        Assert.NotNull(last);
        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
        last.Dispose();
    }

    [Fact]
    public async Task EntityManager_CanReport_Returns202()
    {
        var client = await FreshUserClientAsync(UserRole.EntityManager);

        var response = await client.PostAsync("/api/client-errors",
            JsonBody(new { message = "عطل بوابة", url = "/portal/files" }));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
