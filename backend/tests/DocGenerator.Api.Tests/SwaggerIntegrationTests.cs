using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

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
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}");
        builder.UseSetting("Database:UsePostgres", "false");
        builder.UseSetting("Swagger:Enabled", "false");
        builder.UseSetting("Jwt:Secret", "integration-test-secret-0123456789-0123456789-0123456789");
        builder.UseSetting("Bootstrap:AdminPassword", "test-bootstrap-password");
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
