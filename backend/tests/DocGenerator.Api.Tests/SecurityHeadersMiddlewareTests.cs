using DocGenerator.Api.Middleware;
using DocGenerator.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests;

/// <summary>
/// FR-010: ترويسة `CSP` مراقبة افتراضيًا، و`report-uri` يُلحَق فقط عند ضبطه نظيفًا
/// (قيمة ملوثة تُتجاهل حفاظًا على سلامة الترويسة من الحقن).
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

    [Fact]
    public async Task ReportUri_Appended_WhenClean()
    {
        var headers = await InvokeAsync(new SecurityOptions { CspReportUri = "/csp-report" });

        var csp = headers["Content-Security-Policy-Report-Only"].ToString();
        Assert.Contains("report-uri /csp-report", csp);
    }

    [Theory]
    [InlineData("/csp-report\r\nX-Injected: 1")]
    [InlineData("/csp report")]
    [InlineData("   ")]
    public async Task ReportUri_Ignored_WhenUnsafe(string uri)
    {
        var headers = await InvokeAsync(new SecurityOptions { CspReportUri = uri });

        var csp = headers["Content-Security-Policy-Report-Only"].ToString();
        Assert.DoesNotContain("report-uri", csp);
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
