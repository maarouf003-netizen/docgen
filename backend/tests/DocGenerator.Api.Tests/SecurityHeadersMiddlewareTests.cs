using DocGenerator.Api.Middleware;
using DocGenerator.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests;

/// <summary>
/// FR-010: ترويسة `CSP` مراقبة افتراضيًا، و`report-uri` يُلحَق فقط عند ضبطه نظيفًا
/// (قيمة ملوثة تُتجاهل حفاظًا على سلامة الترويسة من الحقن).
/// </summary>
public class SecurityHeadersMiddlewareTests
{
    private static async Task<IHeaderDictionary> InvokeAsync(SecurityOptions options)
    {
        var context = new DefaultHttpContext();
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
}
