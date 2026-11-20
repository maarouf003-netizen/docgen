using System.Net;
using DocGenerator.Api.Tests.TestServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace DocGenerator.Api.Tests;

public class SwaggerIntegrationTests
{
    [PostgresFact]
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
    private readonly PostgresHostDatabase _db = PostgresHostDatabase.Create();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        // قاعدة Postgres معزولة مهاجَرة مسبقًا: لا مسار SQLite ولا EnsureCreated.
        builder.UseSecureProductionDefaults();
        builder.UseSetting("ConnectionStrings:DefaultConnection", _db.ConnectionString);
        builder.UseSetting("Database:AutoMigrate", "false");
        builder.UseSetting("HeadAlerts:Outbox:Enabled", "false");
        builder.UseSetting("Swagger:Enabled", "false");
        builder.UseSetting("Jwt:Secret", "integration-test-secret-0123456789-0123456789-0123456789");
        builder.UseSetting("Bootstrap:AdminPassword", "test-bootstrap-password");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _db.Dispose();
    }
}
