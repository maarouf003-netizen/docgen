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
/// </summary>
[Collection(ApiTestCollection.Name)]
public class CspProductionIntegrationTests : IClassFixture<CspProductionIntegrationTests.ProductionSpaFactory>
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

    /// <summary>
    /// مضيف إنتاجي معزول بجذر ويب مؤقت يحتوي <c>index.html</c> بلا سكربتات مضمّنة —
    /// نفس شكل مخرجات <c>Vite</c>. قاعدة SQLite مؤقتة، والحارس <c>H-02</c> لا يعنيه
    /// لأن البيئة إنتاج.
    /// </summary>
    public sealed class ProductionSpaFactory : WebApplicationFactory<Program>
    {
        private const string IndexHtml = """
            <!doctype html>
            <html lang="ar" dir="rtl">
              <head>
                <meta charset="UTF-8" />
                <link rel="icon" type="image/svg+xml" href="/favicon.svg" />
                <title>بروفة CSP</title>
                <script type="module" crossorigin src="/assets/index-test.js"></script>
                <link rel="stylesheet" crossorigin href="/assets/index-test.css" />
              </head>
              <body><div id="root"></div></body>
            </html>
            """;

        private readonly string _dbPath = Path.Combine(
            Path.GetTempPath(), $"docgen_csp_{Guid.NewGuid():N}.db");
        private readonly string _logDir = Path.Combine(
            Path.GetTempPath(), $"docgen_csp_logs_{Guid.NewGuid():N}");
        private readonly string _webRoot = Path.Combine(
            Path.GetTempPath(), $"docgen_csp_web_{Guid.NewGuid():N}");

        public ProductionSpaFactory()
        {
            Directory.CreateDirectory(_webRoot);
            File.WriteAllText(Path.Combine(_webRoot, "index.html"), IndexHtml);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseWebRoot(_webRoot);
            builder.UseSetting("Security:CspReportOnly", "false");
            builder.UseSetting("Security:KnownProxies:0", "127.0.0.1");
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}");
            builder.UseSetting("Logging:File:Path", Path.Combine(_logDir, "logs-.txt"));
            builder.UseSetting("Database:UsePostgres", "false");
            builder.UseSetting("Database:AutoMigrate", "true");
            builder.UseSetting("Database:AllowSqliteInProduction", "true");
            builder.UseSetting("Swagger:Enabled", "false");
            builder.UseSetting("Jwt:Secret", "csp-test-secret-0123456789-0123456789-0123456789");
            builder.UseSetting("Bootstrap:AdminPassword", "CspProbe123456!");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            foreach (var suffix in new[] { "", "-shm", "-wal" })
            {
                try { File.Delete(_dbPath + suffix); } catch { /* ignore */ }
            }
            try { if (Directory.Exists(_logDir)) Directory.Delete(_logDir, recursive: true); } catch { /* ignore */ }
            try { if (Directory.Exists(_webRoot)) Directory.Delete(_webRoot, recursive: true); } catch { /* ignore */ }
        }
    }
}
