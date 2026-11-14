using System.Net;
using DocGenerator.Api.Tests.TestServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace DocGenerator.Api.Tests;

/// <summary>
/// H-02: بيئة <c>Development</c> تُعطّل خمسة ضوابط أمنية دفعةً واحدة (تطهير رسائل الاستثناء،
/// الترحيل التلقائي، <c>Swagger</c>، <c>CORS</c> المحلي، و<c>Secure</c> على كوكي <c>CSRF</c>)،
/// فتشغيلها على خادم منشور يفتحها كلها بمتغيّر بيئة واحد. الحارس في <c>Program.cs</c> يرفض
/// الإقلاع بلا راية صريحة (<c>Security:AllowDevelopmentEnvironment</c>) — وهذا الاختبار يثبت
/// أن الرفض يقع فعلًا قبل بناء أي خدمة، وأن متغيّر البيئة وحده لم يبقَ كافيًا.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class DevelopmentEnvironmentGuardTests
{
    [Fact]
    public void DevelopmentWithoutExplicitFlag_RefusesToStart()
    {
        using var factory = new UnflaggedDevelopmentFactory();

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Refusing to start in Development", Flatten(error), StringComparison.Ordinal);
    }

    /// <summary>يجمع رسائل سلسلة الاستثناءات: المضيف قد يغلّف خطأ الإقلاع داخل استثناء خارجي.</summary>
    private static string Flatten(Exception error)
    {
        var messages = new List<string>();
        for (var current = error; current is not null; current = current.InnerException)
            messages.Add(current.Message);
        return string.Join(" | ", messages);
    }

    /// <summary>
    /// مضيف تطوير مقصود **بلا** راية <c>Security:AllowDevelopmentEnvironment</c> — الحالة التي
    /// يُنتجها خطأ تشغيل حقيقي على الخادم (ASPNETCORE_ENVIRONMENT=Development وحده).
    /// </summary>
    private sealed class UnflaggedDevelopmentFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(
            Path.GetTempPath(), $"docgen_devguard_{Guid.NewGuid():N}.db");
        private readonly string _logDir = Path.Combine(
            Path.GetTempPath(), $"docgen_devguard_logs_{Guid.NewGuid():N}");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            // ADR-0001: سلسلة Postgres شكلية للحارس + سياق SQLite اختباري.
            builder.UseSetting("ConnectionStrings:DefaultConnection", TestDatabase.PostgresPlaceholder);
            builder.UseSetting("Logging:File:Path", Path.Combine(_logDir, "logs-.txt"));
            builder.UseSetting("Database:AutoMigrate", "false");
            builder.UseSetting("HeadAlerts:Outbox:Enabled", "false");
            builder.UseSetting("Jwt:Secret", "devguard-test-secret-0123456789-0123456789-0123456789");
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
            try { if (Directory.Exists(_logDir)) Directory.Delete(_logDir, recursive: true); } catch { /* ignore */ }
        }
    }
}
