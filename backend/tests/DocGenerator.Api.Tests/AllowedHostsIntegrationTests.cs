using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DocGenerator.Api.Tests;

/// <summary>
/// H-04: ضبط <c>AllowedHosts</c> على النطاق النهائي هو الفرق بين «تشديد» و«انقطاع خدمة يوم
/// النقل». فحص الصحة يصل من الوكيل عادةً إلى <c>127.0.0.1:8080</c>، و<c>HostFilteringMiddleware</c>
/// يردّ <c>400</c> لأي <c>Host</c> غير مطابق — فيُعلن الموازن الخادم ميتًا. يحرس هذا الاختبار
/// العقد الثلاثي: مضيف مُدرج ⇒ 200، صيغة <c>IP:port</c> ⇒ 200، مضيف غريب ⇒ 400.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class AllowedHostsIntegrationTests : IClassFixture<AllowedHostsIntegrationTests.HostFilteringFactory>
{
    private readonly HostFilteringFactory _factory;

    public AllowedHostsIntegrationTests(HostFilteringFactory factory) => _factory = factory;

    /// <summary>القيمة المقترحة للخادم الحكومي: النطاق النهائي + عنوان الوكيل/الموازن.</summary>
    private const string AllowedList = "docgen.test;127.0.0.1";

    private async Task<HttpStatusCode> GetHealthAsync(string host)
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/healthz");
        request.Headers.Host = host;
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    [Fact]
    public async Task ListedHost_IsServed()
    {
        Assert.Equal(HttpStatusCode.OK, await GetHealthAsync("docgen.test"));
    }

    [Fact]
    public async Task HealthCheckHostWithPort_IsServed_WhenBareAddressIsListed()
    {
        // جوهر البند: بلا هذه المطابقة ينكسر فحص الصحة لحظة تحويل حركة الوكيل.
        Assert.Equal(HttpStatusCode.OK, await GetHealthAsync("127.0.0.1:8080"));
    }

    [Fact]
    public async Task UnknownHost_IsRejected()
    {
        Assert.Equal(HttpStatusCode.BadRequest, await GetHealthAsync("evil.example"));
    }

    /// <summary>
    /// مضيف إنتاج معزول يفعّل <c>AllowedHosts</c> على قائمة صريحة، مشترك بين اختبارات الفئة
    /// (بناء مضيف + ترحيل يُدفع مرّة واحدة بدل ثلاثة).
    /// </summary>
    public sealed class HostFilteringFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(
            Path.GetTempPath(), $"docgen_hostfilter_{Guid.NewGuid():N}.db");
        private readonly string _logDir = Path.Combine(
            Path.GetTempPath(), $"docgen_hostfilter_logs_{Guid.NewGuid():N}");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("AllowedHosts", AllowedList);
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}");
            builder.UseSetting("Logging:File:Path", Path.Combine(_logDir, "logs-.txt"));
            builder.UseSetting("Database:UsePostgres", "false");
            builder.UseSetting("Database:AutoMigrate", "true");
            builder.UseSetting("Database:AllowSqliteInProduction", "true");
            builder.UseSetting("Swagger:Enabled", "false");
            builder.UseSetting("Jwt:Secret", "hostfilter-test-secret-0123456789-0123456789-0123456789");
            builder.UseSetting("Bootstrap:AdminPassword", "HostFilter123456!");
            builder.UseSetting("Security:KnownProxies:0", "127.0.0.1");
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
