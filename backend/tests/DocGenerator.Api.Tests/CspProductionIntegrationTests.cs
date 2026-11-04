using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DocGenerator.Api.Tests;

/// <summary>
/// W0-CSP: بروفة وضع <c>Production</c> — الافتراض في التطوير أن <c>Vite</c> يخدم الواجهة،
/// فلا تصل ترويسة <c>Kestrel</c> إلى مستند <c>HTML</c> أصلًا. الاختبار يبني مضيفًا إنتاجيًا
/// حقيقيًا ويخدم <c>index.html</c> من جذر ويب مؤقت، ثم يقيس الترويسة **على مستند الصفحة**
/// كما يراها المتصفح: مفروضة (لا <c>Report-Only</c>)، وبلا <c>upgrade-insecure-requests</c>،
/// ومتسقة مع الأصول المحلية التي يبنيها <c>Vite</c>.
/// W1: يضيف التحقق من ربط جامع تقارير <c>CSP</c>: فشل إقلاع القيمة غير المطابقة، ووصول
/// <c>report-uri</c>/<c>report-to</c>/<c>Reporting-Endpoints</c> إلى المستند.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class CspProductionIntegrationTests : IClassFixture<ProductionSpaFactory>
{
    private readonly ProductionSpaFactory _factory;

    public CspProductionIntegrationTests(ProductionSpaFactory factory) => _factory = factory;

    [Fact]
    public async Task SpaDocument_CarriesEnforcedCspWithoutUpgradeDirective()
    {
        var client = _factory.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.DoesNotContain("upgrade-insecure-requests", csp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("script-src 'self'", csp);
        Assert.Contains("style-src 'self' 'unsafe-inline'", csp);
        Assert.False(response.Headers.Contains("Content-Security-Policy-Report-Only"));
    }

    [Fact]
    public async Task SpaDocument_StillCarriesBaselineSecurityHeaders()
    {
        var client = _factory.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        // صفحات المستند ليست ردود API: لا تُوسم بـ`no-store` (منع التخزين للـAPI وحده).
        Assert.False(response.Headers.Contains("Cache-Control"));
    }

    [Fact]
    public void InvalidCspReportUri_FailsHostStartup()
    {
        using var factory = WithSettings(
            ("Security:CspReportUri", "https://collector.example/csp"));

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("Security:CspReportUri", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void TypoInCspReportUri_FailsHostStartup()
    {
        using var factory = WithSettings(
            ("Security:CspReportUri", "/api/csp-reports"));

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("Security:CspReportUri", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfiguredCollector_AddsReportingHeadersToDocument()
    {
        using var factory = WithSettings(
            ("Security:CspReportUri", "/api/csp-report"));
        var client = factory.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("report-uri /api/csp-report", csp, StringComparison.Ordinal);
        Assert.Contains("report-to csp", csp, StringComparison.Ordinal);
        Assert.Equal(
            "csp=\"/api/csp-report\"",
            Assert.Single(response.Headers.GetValues("Reporting-Endpoints")));
        Assert.Equal(
            "{\"group\":\"csp\",\"max_age\":31536000,\"endpoints\":[{\"url\":\"/api/csp-report\"}]}",
            Assert.Single(response.Headers.GetValues("Report-To")));
    }

    private WebApplicationFactory<Program> WithSettings(params (string Key, string Value)[] settings)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);
        });
    }
}
