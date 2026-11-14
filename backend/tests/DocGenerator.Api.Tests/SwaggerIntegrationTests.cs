using System.Net;
using DocGenerator.Api.Tests.TestServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace DocGenerator.Api.Tests;

public class SwaggerIntegrationTests
{
    [Fact]
    public async Task Swagger_DisabledOutsideDevelopment_ReturnsNotFound()
    {
        using var factory = new SwaggerDisabledFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

/// <summary>
/// Factory تعمل ببيئة الإنتاج (Swagger مغلق) لتأكيد التقييد الصارم.
/// </summary>
public sealed class SwaggerDisabledFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"docgen_swag_{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        // ADR-0001: لا مسار SQLite إنتاجي — سلسلة Postgres شكلية للحارس، والسياق اختباري.
        builder.UseSetting("ConnectionStrings:DefaultConnection", TestDatabase.PostgresPlaceholder);
    // بيئة الإنتاج لا ترحّل تلقائيًا (Database:AutoMigrate=false افتراضيًا):
    // الاختبار يبني قاعدة مؤقتة جديدة، فيُفعّل الترحيل صراحةً لنطاق الاختبار.
    builder.UseSetting("Database:AutoMigrate", "false");
        builder.UseSetting("HeadAlerts:Outbox:Enabled", "false");
        builder.UseSetting("Swagger:Enabled", "false");
        builder.UseSetting("Jwt:Secret", "integration-test-secret-0123456789-0123456789-0123456789");
        builder.UseSetting("Bootstrap:AdminPassword", "test-bootstrap-password");
        builder.ConfigureTestServices(services =>
            TestDatabase.UseSqlite(services, $"Data Source={_dbPath}"));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            try { File.Delete(_dbPath + suffix); } catch { /* ignore */ }
        }
    }
}
