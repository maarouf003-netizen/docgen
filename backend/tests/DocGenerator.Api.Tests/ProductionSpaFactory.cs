using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DocGenerator.Api.Tests;

/// <summary>
/// مضيف إنتاجي معزول يخدم <c>index.html</c> من جذر ويب مؤقت — بروفة ما يراه المتصفح فعلًا
/// عندما يخدم <c>Kestrel</c> مستند <c>SPA</c> المبني (لا <c>Vite</c>). مشترك بين بروفة
/// <c>CSP</c> و<c>HSTS</c>؛ كل اختبار يضيف راياته عبر <c>WithWebHostBuilder</c>.
/// قاعدة <c>SQLite</c> مؤقتة، والحارس <c>H-02</c> لا يعنيه لأن البيئة إنتاج.
/// </summary>
public class ProductionSpaFactory : WebApplicationFactory<Program>
{
    private const string IndexHtml = """
        <!doctype html>
        <html lang="ar" dir="rtl">
          <head>
            <meta charset="UTF-8" />
            <link rel="icon" type="image/svg+xml" href="/favicon.svg" />
            <title>بروفة أمنية</title>
            <script type="module" crossorigin src="/assets/index-test.js"></script>
            <link rel="stylesheet" crossorigin href="/assets/index-test.css" />
          </head>
          <body><div id="root"></div></body>
        </html>
        """;

    /// <summary>محتوى ملفات الأصول الوهمية: شكل مخرجات <c>Vite</c> بلا سكربتات مضمّنة.</summary>
    private static readonly (string RelativePath, string Content)[] SyntheticAssets =
    [
        ("assets/index-test.js", "export {};\n"),
        ("assets/index-test.css", ":root { color-scheme: light; }\n"),
        ("favicon.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1 1\"></svg>\n"),
    ];

    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"docgen_prod_{Guid.NewGuid():N}.db");
    private readonly string _logDir = Path.Combine(
        Path.GetTempPath(), $"docgen_prod_logs_{Guid.NewGuid():N}");
    private readonly string _webRoot = Path.Combine(
        Path.GetTempPath(), $"docgen_prod_web_{Guid.NewGuid():N}");

    public ProductionSpaFactory()
    {
        Directory.CreateDirectory(_webRoot);
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), IndexHtml);
        foreach (var (relativePath, content) in SyntheticAssets)
        {
            var fullPath = Path.Combine(_webRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }
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
