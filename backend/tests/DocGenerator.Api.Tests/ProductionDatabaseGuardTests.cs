using System.Net;
using DocGenerator.Api.Tests.TestServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبارات حارس قاعدة الإنتاج (ADR-0001 — PostgreSQL فقط):
/// لا إقلاع بلا سلسلة Postgres صالحة، ولا وقوع صامت على SQLite، ونقاط البنية
/// (healthz/السنة/احتياطي SPA) تبقى مجهولة الهوية رغم التفويض الافتراضي (M-01).
/// </summary>
public sealed class ProductionDatabaseGuardTests : IDisposable
{
    private readonly List<WebApplicationFactory<Program>> _factories = new();
    private readonly List<string> _tempDbPaths = new();
    private readonly List<string> _tempLogDirs = new();

    public void Dispose()
    {
        foreach (var factory in _factories)
        {
            try { factory.Dispose(); } catch { /* best effort */ }
        }
        foreach (var dbPath in _tempDbPaths)
        {
            foreach (var suffix in new[] { "", "-shm", "-wal" })
            {
                try { File.Delete(dbPath + suffix); } catch { /* best effort */ }
            }
        }
        foreach (var dir in _tempLogDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void Production_WithoutPostgresConnection_RefusesToStart()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            // لا DATABASE_URL ولا سلسلة اتصال: يجب أن يرفض الإقلاع بدل SQLite افتراضي.
            builder.UseSetting("DATABASE_URL", "");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "");
            builder.UseSetting("Jwt:Secret", ApiFactory.TestJwtSecret);
        });
        _factories.Add(factory);

        var ex = Assert.ThrowsAny<Exception>(() => factory.Services);
        Assert.Contains("PostgreSQL connection string is required", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Production_OnSqliteConnectionString_RefusesToStart()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("DATABASE_URL", "");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Data Source=docgen.db");
            builder.UseSetting("Jwt:Secret", ApiFactory.TestJwtSecret);
        });
        _factories.Add(factory);

        var ex = Assert.ThrowsAny<Exception>(() => factory.Services);
        Assert.Contains("not a valid PostgreSQL connection string", ex.ToString(), StringComparison.Ordinal);
        Assert.Contains("SQLite is not supported", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Production_InfraEndpointsStayAnonymous()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"docgen_guard_{Guid.NewGuid():N}.db");
        _tempDbPaths.Add(dbPath);
        var logDir = Path.Combine(Path.GetTempPath(), $"docgen_guard_logs_{Guid.NewGuid():N}");
        _tempLogDirs.Add(logDir);

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            // ADR-0001: سلسلة Postgres شكلية للحارس، والسياق الفعلي SQLite اختباري فقط.
            builder.UseSetting("ConnectionStrings:DefaultConnection", TestDatabase.PostgresPlaceholder);
            // بيئة الإنتاج لا ترحّل تلقائيًا (Database:AutoMigrate=false افتراضيًا):
            // الاختبار يبني قاعدة مؤقتة جديدة، فيُفعّل الترحيل صراحةً لنطاق الاختبار.
            builder.UseSetting("Database:AutoMigrate", "true");
            builder.UseSetting("HeadAlerts:Outbox:Enabled", "false");
            builder.UseSetting("Bootstrap:AdminPassword", "GuardAdminPass123");
            builder.UseSetting("Jwt:Secret", ApiFactory.TestJwtSecret);
            builder.UseSetting("Logging:File:Path", Path.Combine(logDir, "logs-.txt"));
            builder.UseSetting("Swagger:Enabled", "false");
            builder.ConfigureTestServices(services =>
                TestDatabase.UseSqlite(services, $"Data Source={dbPath}"));
        });
        _factories.Add(factory);

        var client = factory.CreateClient();

        // فحص الصحة مجهول صراحةً ([AllowAnonymous]) — وإلا اعتبر المنسّق النشر ميتًا.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/healthz")).StatusCode);

        // سنة الخادم العامة مجهولة ([AllowAnonymous]) — لم يكسرها التفويض الافتراضي.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/meta/current-year")).StatusCode);

        // مسار SPA مجهول يصل لنقطة الاحتياطي (404 لغياب index.html في الاختبار،
        // لا 401) — فصفحة الدخول لا تُحجب.
        Assert.NotEqual(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/spa-probe-login-route")).StatusCode);

        // ومسار API محمي ما زال يطلب مصادقة (التفويض الافتراضي على المتحكمات فاعل).
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/documents?perPage=1")).StatusCode);
    }
}
