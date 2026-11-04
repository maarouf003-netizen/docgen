using DocGenerator.Api.Security;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests;

/// <summary>
/// W0-CSP follow-up: ترويسة <c>Strict-Transport-Security</c> الفعلية التي يولّدها
/// <see cref="HstsMiddleware"/> على طلب <c>HTTPS</c>. تُستدعى طبقة الإطار الحقيقية على
/// <see cref="HttpContext"/> مخططه <c>https</c> لأن <c>TestServer</c> يثبّت المخطط على <c>http</c>
/// ولا يحاكي إنهاء <c>TLS</c> عند الوكيل — وهو النمط نفسه المعتمد في
/// <see cref="ForwardedHeadersMiddlewareTests"/>.
/// </summary>
public class HstsHeaderTests
{
    private static HstsOptions Configured(SecurityOptions security)
    {
        var hsts = new HstsOptions();
        HstsSetup.Configure(hsts, security);
        return hsts;
    }

    private static async Task<string?> HeaderForAsync(SecurityOptions security, string scheme)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Response.Body = new MemoryStream();

        var middleware = new HstsMiddleware(
            _ => Task.CompletedTask,
            Options.Create(Configured(security)));
        await middleware.Invoke(context);
        await context.Response.StartAsync();

        return context.Response.Headers["Strict-Transport-Security"];
    }

    [Fact]
    public async Task HttpsResponse_CarriesMaxAgeWithoutSubDomainsOrPreload()
    {
        var header = await HeaderForAsync(new SecurityOptions { HstsEnabled = true }, "https");

        Assert.NotNull(header);
        Assert.Contains("max-age=31536000", header, StringComparison.Ordinal);
        Assert.DoesNotContain("includeSubDomains", header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("preload", header, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HttpsResponse_CarriesSubDomainsWhenOptedIn()
    {
        var header = await HeaderForAsync(
            new SecurityOptions { HstsEnabled = true, HstsIncludeSubDomains = true },
            "https");

        Assert.NotNull(header);
        Assert.Contains("includeSubDomains", header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("preload", header, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HttpsResponse_CarriesPreloadAndForcedSubDomainsWhenEnabled()
    {
        var header = await HeaderForAsync(
            new SecurityOptions
            {
                HstsEnabled = true,
                HstsMaxAgeDays = 730,
                HstsPreload = true,
            },
            "https");

        Assert.NotNull(header);
        Assert.Contains("max-age=63072000", header, StringComparison.Ordinal);
        // preload يشترط includeSubDomains، فيُفرَض معه حتى لو لم تُضبط رايته.
        Assert.Contains("includeSubDomains", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("preload", header, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HttpResponse_DoesNotCarryHsts()
    {
        var header = await HeaderForAsync(new SecurityOptions { HstsEnabled = true }, "http");

        Assert.Null(header);
    }
}
