using DocGenerator.Api.Tests.TestServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace DocGenerator.Api.Tests;

/// <summary>
/// مضيف إنتاجي معزول يخدم <c>index.html</c> من جذر ويب مؤقت — بروفة ما يراه المتصفح فعلًا
/// عندما يخدم <c>Kestrel</c> مستند <c>SPA</c> المبني (لا <c>Vite</c>). مشترك بين بروفة
/// <c>CSP</c> و<c>HSTS</c>؛ كل اختبار يضيف راياته عبر <c>WithWebHostBuilder</c>.
/// قاعدة <c>PostgreSQL</c> معزولة مهاجَرة (‏`PostgresHostDatabase`؛ ADR-0001: لا مسار
/// إنتاجي آخر)، والحارس <c>H-02</c> لا يعنيه لأن البيئة إنتاج.
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

    private readonly PostgresHostDatabase _db = PostgresHostDatabase.Create();
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
        // وضع أمني إنتاجي مكتمل (P2-03): الحارس يرفض الإقلاع بدونه، والاختبارات تتحقق
        // من الرؤوس/الرفض فوق إعداد صالح لا فوق إعداد ناقص.
        builder.UseSecureProductionDefaults();
        builder.UseSetting("ConnectionStrings:DefaultConnection", _db.ConnectionString);
        builder.UseSetting("Logging:File:Path", Path.Combine(_logDir, "logs-.txt"));
        // القاعدة مهاجَرة مسبقًا؛ الإنتاج لا يرحّل تلقائيًا.
        builder.UseSetting("Database:AutoMigrate", "false");
        // لا مشغّل صندوق صادر في الاختبار.
        builder.UseSetting("HeadAlerts:Outbox:Enabled", "false");
        builder.UseSetting("Swagger:Enabled", "false");
        builder.UseSetting("Jwt:Secret", "csp-test-secret-0123456789-0123456789-0123456789");
        builder.UseSetting("Bootstrap:AdminPassword", "CspProbe123456!");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _db.Dispose();
        try { if (Directory.Exists(_logDir)) Directory.Delete(_logDir, recursive: true); } catch { /* ignore */ }
        try { if (Directory.Exists(_webRoot)) Directory.Delete(_webRoot, recursive: true); } catch { /* ignore */ }
    }
}
