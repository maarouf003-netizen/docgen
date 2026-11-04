using DocGenerator.Api.Middleware;
using DocGenerator.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests;

/// <summary>
/// FR-010: ترويسة `CSP` مراقبة افتراضيًا، و`report-uri`/`report-to` يُلحقان فقط عند ضبط
/// المسار المدمج `/api/csp-report` حرفيًا؛ أي قيمة أخرى تُتجاهل في الوسيط (وتمنع الإقلاع
/// عبر `CspSetup.Validate` في المضيف الحقيقي).
/// H-03: ردود `/api` تحمل `Cache-Control: no-store` حتى لا تُخزَّن بيانات ما بعد المصادقة،
/// وما عداها (الصحة/الأصول الثابتة) لا يحمله.
/// W0-CSP: السياسة لا تحمل `upgrade-insecure-requests` (تُبيّض التطبيق على خادم `HTTP`)،
/// وتُثبَّت هنا مباشرةً حتى لا تعود عبر نسخ/لصق مستقبلي.
/// </summary>
public class SecurityHeadersMiddlewareTests
{
    private static async Task<IHeaderDictionary> InvokeAsync(SecurityOptions options, string path = "/")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var middleware = new SecurityHeadersMiddleware(
            _ => Task.CompletedTask,
            Options.Create(options));
        await middleware.InvokeAsync(context);
        return context.Response.Headers;
    }

    [Fact]
    public async Task ReportOnly_ByDefault()
    {
        var headers = await InvokeAsync(new SecurityOptions());

        Assert.True(headers.ContainsKey("Content-Security-Policy-Report-Only"));
        Assert.False(headers.ContainsKey("Content-Security-Policy"));
    }

    [Fact]
    public async Task Enforce_WhenReportOnlyDisabled()
    {
        var headers = await InvokeAsync(new SecurityOptions { CspReportOnly = false });

        Assert.True(headers.ContainsKey("Content-Security-Policy"));
        Assert.False(headers.ContainsKey("Content-Security-Policy-Report-Only"));
    }

    [Fact]
    public async Task Csp_DoesNotUpgradeInsecureRequests()
    {
        var headers = await InvokeAsync(new SecurityOptions());

        var csp = headers["Content-Security-Policy-Report-Only"].ToString();
        Assert.DoesNotContain("upgrade-insecure-requests", csp, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Csp_CoreDirectives_ArePresent()
    {
        var headers = await InvokeAsync(new SecurityOptions());

        var csp = headers["Content-Security-Policy-Report-Only"].ToString();
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("script-src 'self'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.Contains("base-uri 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
    }

    [Theory]
    [InlineData("/csp-report\r\nX-Injected: 1")]
    [InlineData("/api/csp report")]
    [InlineData("   ")]
    [InlineData("https://evil.example/csp-report")]
    [InlineData("http://evil.example/csp-report")]
    [InlineData("//evil.example/csp-report")]
    [InlineData("csp-report")]
    [InlineData("/csp-report")]
    [InlineData("/api/csp-report/")]
    [InlineData("/api/csp-reports")]
    [InlineData("/api/csp-report?source=csp")]
    [InlineData("\\\\evil.example\\csp-report")]
    [InlineData("/api/csp-report\"")]
    [InlineData("/api/csp-report,other")]
    [InlineData("/api/csp-report;other")]
    [InlineData("/تقرير")]
    public async Task ReportUri_Ignored_WhenUnsafeOrNotBuiltIn(string uri)
    {
        var headers = await InvokeAsync(new SecurityOptions { CspReportUri = uri });

        var csp = headers["Content-Security-Policy-Report-Only"].ToString();
        Assert.DoesNotContain("report-uri", csp);
        Assert.DoesNotContain("report-to", csp);
        Assert.False(headers.ContainsKey("Reporting-Endpoints"));
    }

    [Fact]
    public async Task ReportUri_Appended_ForBuiltInCollector()
    {
        var headers = await InvokeAsync(
            new SecurityOptions { CspReportUri = CspReportUriPolicy.RequiredPath });

        var csp = headers["Content-Security-Policy-Report-Only"].ToString();
        Assert.Contains($"report-uri {CspReportUriPolicy.RequiredPath}", csp);
        Assert.Contains("report-to csp", csp);
        Assert.Equal(
            $"csp=\"{CspReportUriPolicy.RequiredPath}\"",
            headers["Reporting-Endpoints"].ToString());
    }

    [Fact]
    public async Task ReportingEndpoints_Absent_WhenReportUriUnset()
    {
        var headers = await InvokeAsync(new SecurityOptions());

        Assert.False(headers.ContainsKey("Reporting-Endpoints"));
        Assert.DoesNotContain("report-to", headers["Content-Security-Policy-Report-Only"].ToString());
    }

    [Theory]
    [InlineData("/api/documents")]
    [InlineData("/API/auth/login")]
    public async Task ApiPath_CarriesNoStore(string path)
    {
        var headers = await InvokeAsync(new SecurityOptions(), path);

        Assert.Equal("no-store", headers["Cache-Control"].ToString());
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/apixyz")]
    [InlineData("/")]
    public async Task NonApiPath_DoesNotCarryNoStore(string path)
    {
        var headers = await InvokeAsync(new SecurityOptions(), path);

        Assert.False(headers.ContainsKey("Cache-Control"));
    }
}
