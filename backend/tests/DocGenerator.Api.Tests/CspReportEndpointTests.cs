using System.Net;
using System.Text;

namespace DocGenerator.Api.Tests;

/// <summary>
/// جامع تقارير <c>CSP</c> (W1): عام بلا مصادقة/CSRF (المتصفح يرسلها تلقائيًا)، يقبل
/// الصيغتين القديمة والحديثة، ويرفض الحمولة غير الصالحة/نوع المحتوى غير المدعوم.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class CspReportEndpointTests
{
    private readonly ApiFactory _factory;

    public CspReportEndpointTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task LegacyReport_AnonymousPostWithoutCsrf_Returns204()
    {
        var client = _factory.CreateClient();
        const string body = """
            {
              "csp-report": {
                "document-uri": "https://app.example/documents/12?token=secret",
                "blocked-uri": "https://evil.example/x.js?tracking=1",
                "effective-directive": "script-src",
                "disposition": "report"
              }
            }
            """;

        var response = await client.PostAsync(
            "/api/csp-report",
            new StringContent(body, Encoding.UTF8, "application/csp-report"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ReportingApiBatch_Returns204()
    {
        var client = _factory.CreateClient();
        const string body = """
            [
              {
                "type": "csp-violation",
                "url": "https://app.example/documents/12",
                "body": {
                  "effectiveDirective": "img-src",
                  "blockedURL": "https://cdn.example/a.png",
                  "documentURL": "https://app.example/documents/12",
                  "disposition": "enforce",
                  "statusCode": 200
                }
              },
              { "type": "deprecation", "body": { "id": "ignored" } }
            ]
            """;

        var response = await client.PostAsync(
            "/api/csp-report",
            new StringContent(body, Encoding.UTF8, "application/reports+json"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task MalformedJson_Returns400WithArabicMessage()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/csp-report",
            new StringContent("{ not-json", Encoding.UTF8, "application/csp-report"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "تقرير CSP غير صالح",
            json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task UnsupportedContentType_Returns415()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/api/csp-report",
            new StringContent("{}", Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }
}
