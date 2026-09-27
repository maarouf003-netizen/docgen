using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// ترويسات الأمان و`CORS` (S3): كل رد يحمل الترويسات، وسياسة `Vite` للتطوير فقط —
/// في الإنتاج لا يُعاد أي `Access-Control-Allow-Origin` لأصل `localhost`.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class SecurityHeadersIntegrationTests
{
    private readonly ApiFactory _factory;

    public SecurityHeadersIntegrationTests(ApiFactory factory) => _factory = factory;

    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        Assert.Equal("nosniff", Single(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Single(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Single(response, "Referrer-Policy"));
        Assert.Equal(
            "camera=(), microphone=(), geolocation=(), payment=(), usb=()",
            Single(response, "Permissions-Policy"));
        Assert.Equal("same-origin", Single(response, "Cross-Origin-Opener-Policy"));
        Assert.Equal("same-origin", Single(response, "Cross-Origin-Resource-Policy"));
        // الافتراضي وضع المراقبة (Security:CspReportOnly=true) — الفرض على المخدّم الخاص فقط.
        var csp = Single(response, "Content-Security-Policy-Report-Only");
        Assert.Contains("default-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }

    private static string Single(HttpResponseMessage response, string name)
    {
        Assert.True(response.Headers.TryGetValues(name, out var values), $"missing header: {name}");
        return Assert.Single(values);
    }

    [Fact]
    public async Task AnonymousEndpoint_CarriesSecurityHeaders()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/meta/current-year");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task AuthenticatedEndpoint_CarriesSecurityHeaders()
    {
        var client = _factory.AuthorizedClient("admin");
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task Development_AllowsViteOrigin()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/meta/current-year");
        request.Headers.Add("Origin", "http://localhost:5173");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("http://localhost:5173", Single(response, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Production_RejectsViteOrigin()
    {
        using var prod = new ProductionFactory();
        var client = prod.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/meta/current-year");
        request.Headers.Add("Origin", "http://localhost:5173");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Access-Control-Allow-Origin", out _));
        AssertSecurityHeaders(response);
    }

    /// <summary>
    /// مضيف إنتاج معزول (قاعدة SQLite مؤقتة + بذر المدير عبر `Bootstrap:AdminPassword`)
    /// للتحقق من سلوك الإنتاج (بلا `CORS`) دون مساس بمصنع التطوير المشترك.
    /// </summary>
    private sealed class ProductionFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(
            Path.GetTempPath(), $"docgen_prod_{Guid.NewGuid():N}.db");
        private readonly string _logDir = Path.Combine(
            Path.GetTempPath(), $"docgen_prod_logs_{Guid.NewGuid():N}");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}");
            builder.UseSetting("Logging:File:Path", Path.Combine(_logDir, "logs-.txt"));
            builder.UseSetting("Database:UsePostgres", "false");
            builder.UseSetting("Swagger:Enabled", "false");
            builder.UseSetting("Jwt:Secret", "production-test-secret-0123456789-0123456789-0123456789");
            builder.UseSetting("Bootstrap:AdminPassword", "ProdTest123456!");
            builder.UseSetting("RateLimiting:MaxLoginAttempts", "5");
            builder.UseSetting("RateLimiting:WindowMinutes", "5");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            foreach (var suffix in new[] { "", "-shm", "-wal" })
            {
                try { File.Delete(_dbPath + suffix); } catch { /* ignore */ }
            }
            try { if (Directory.Exists(_logDir)) Directory.Delete(_logDir, recursive: true); } catch { /* ignore */ }
        }
    }
}
