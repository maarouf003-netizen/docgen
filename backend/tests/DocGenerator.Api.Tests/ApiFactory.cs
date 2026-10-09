using System.Text;
using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Api.Tests.TestServices;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// WebApplicationFactory بقاعدة SQLite مؤقتة معزولة لكل مجموعة اختبارات،
/// مع إعدادات صريحة (Secret، RateLimiting، Swagger) — تطبيق حقيقي من Program.cs.
/// يُستبدل مشتّق كلمات المرور الثقيل (PBKDF2 200k) بنسخة سريعة مكافئة للسلوك
/// (<see cref="FastTestPasswordHasher"/>) لأن تكلفته تُدفع عند كل تسجيل دخول/إنشاء مستخدم.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"docgen_it_{Guid.NewGuid():N}.db");
    private readonly string _logDir = Path.Combine(
        Path.GetTempPath(), $"docgen_it_logs_{Guid.NewGuid():N}");

    /// <summary>
    /// يبني المضيف (الترحيلات + البذر) فورًا على خيط بلا <see cref="SynchronizationContext"/>،
    /// فلا تلتقط استمراريات <c>Program.cs</c> الداخلية سياق xUnit المتوازي (سبب قفل ميت
    /// موثّق عندما تعمل عدة مصانع مستقلة بالتوازي على عدّاء ضعيف الأنوية).
    /// </summary>
    public ApiFactory()
    {
        _ = Task.Run(() => Services).GetAwaiter().GetResult();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}");
        // عزل ملف سجل Serilog عن المستودع: كل مصنع يكتب في مجلد مؤقت خاص به.
        builder.UseSetting("Logging:File:Path", Path.Combine(_logDir, "logs-.txt"));
        builder.UseSetting("Database:UsePostgres", "false");
        // تجاوز كلمة بذر التطوير بقيمة اختبارية ثابتة (RF-006): القاعدة هنا ملف SQLite مؤقت
        // معزول يُحذَف في Dispose، فلا يصل هذا التجاوز لأي بيئة حقيقية.
        builder.UseSetting("Bootstrap:DevSeedPassword", "123456");
        builder.UseSetting("Swagger:Enabled", "false");
        builder.UseSetting("Jwt:Secret", "integration-test-secret-0123456789-0123456789-0123456789");
        builder.UseSetting("RateLimiting:MaxLoginAttempts", "5");
        builder.UseSetting("RateLimiting:WindowMinutes", "5");
        // سقوف المحدد العام مرفوعة في المصنع المشترك حتى لا يخنق سير الاختبارات المتسلسلة
        // (نفس عنوان IP للخادم الاختباري ومستخدمون مشتركون) — اختبارات S4 المخصصة تخفّضها
        // عبر WithWebHostBuilder على نسخ معزولة (انظر RateLimitingIntegrationTests).
        builder.UseSetting("RateLimiting:LoginIpPerMinute", "100000");
        builder.UseSetting("RateLimiting:GeneralAnonPerMinute", "100000");
        builder.UseSetting("RateLimiting:GeneralAuthPerMinute", "100000");
        builder.UseSetting("RateLimiting:ExpensivePerMinute", "100000");
        builder.UseSetting("RateLimiting:PasswordPerMinute", "100000");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPasswordHasher));
            if (descriptor is not null)
                services.Remove(descriptor);
            services.AddScoped<IPasswordHasher, FastTestPasswordHasher>();
        });
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

    /// <summary>
    /// يُسجّل الدخول على عميل جديد يحتفظ بـ Cookie المصادقة (Set-Cookie من الاستجابة) فيتولى
    /// ترويض الترويسات لاحقًا تلقائيًا — كما يفعل متصفح حقيقي مع HttpOnly + SameSite=Strict.
    /// يعيد Token للفحص/التأكيد في الاختبارات (قيمة الـ Cookie نفسها = JWT).
    /// </summary>
    public async Task<LoginResult?> LoginAsync(string username, string password, int? branchId = null)
    {
        var client = CreateClient();
        var body = JsonSerializer.Serialize(new { username, password, branchId });
        var response = await client.PostAsync("/api/auth/login",
            new StringContent(body, Encoding.UTF8, "application/json"));
        var content = await response.Content.ReadAsStringAsync();
        var token = ExtractCookieValue(response, "docgen_token");
        var csrf = ExtractCookieValue(response, "docgen_csrf");
        var result = new LoginResult(client, token, (int)response.StatusCode, content);
        if (csrf is not null)
            client.DefaultRequestHeaders.Add("X-CSRF-Token", csrf);
        return result;
    }

    /// <summary>يجلب قيمة Cookie من ترويسة Set-Cookie بالاسم المحدد (أول قيمة قبل الفواصل المنقوطة).</summary>
    public static string? ExtractCookieValue(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            return null;
        foreach (var value in setCookies)
        {
            var start = value.IndexOf(name + "=", StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                continue;
            var segment = value[(start + name.Length + 1)..];
            var end = segment.IndexOf(';');
            return end < 0 ? segment : segment[..end];
        }
        return null;
    }

    /// <summary>عميل مُصادَق عبر Cookie الدخول الفعلي (POST /api/auth/login).
    /// يُدار الدخول على خيط بلا <see cref="SynchronizationContext"/> حتى لا يلتقط
    /// <c>await</c> الداخلي سياق xUnit المتوازي فيحجب الخيط إلى الأبد (قفل ميت).</summary>
    public HttpClient AuthorizedClient(string username, string password = "123456")
    {
        var login = Task.Run(() => LoginAsync(username, password)).GetAwaiter().GetResult();
        if (login?.Token is null)
            throw new InvalidOperationException(
                $"Login failed for '{username}' (status {(login?.StatusCode ?? 0)}).");
        return login.Client;
    }

    /// <summary>عميل مُصادَق بحقن قيمة التوكن مباشرة في Cookie (مسار سريع لاختبارات الوثائق).</summary>
    public HttpClient ClientWithToken(string token)
    {
        var client = CreateClient();
        client.SetAuthCookie(token);
        return client;
    }

    /// <summary>
    /// سكّ توكن لجلسة مفروضة مباشرة دون المرور ببوابة الدخول — لاختبار حراسات
    /// النقاط الطرفية ضد جلسات شاذة (كرئيس بلا فرع) ترفضها البوابة أصلًا.
    /// الدفاع العمقي يُختبر هنا، والبوابة تُختبر في `AuthIntegrationTests`.
    /// تنبيه: التوكن يُسكّ من نسخة الكيان الممررة لحظيًا — أي تغيير لاحق
    /// (نقل فرع/دور) يُبطل هذا التوكن (`TokenVersion`) فلا تُمرّر كيانًا قديمًا
    /// ثم تتوقع نجاحًا.
    /// </summary>
    public HttpClient ClientForUser(User user)
    {
        using var scope = Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return ClientWithToken(tokens.CreateToken(user));
    }

    public async Task<User> CreateUserAsync(string username, UserRole role, int? branchId = null, string password = "123456", bool isActive = true)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = new User
        {
            Username = ArabicNameNormalizer.Normalize(username),
            FullName = username,
            Role = role,
            BranchId = branchId,
            IsActive = isActive,
            PasswordHash = hasher.Hash(password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<int> CreateDocumentAsync(string token, string borrowerName = "مقترض",
        string? applicant = "المدعي", string? court = "دمشق",
        string? borrowerFather = null, string? borrowerFamily = null,
        bool withEstate = false, bool registered = false, bool withSeizureDate = true)
    {
        var client = CreateClient();
        client.SetAuthCookie(token);
        var body = JsonSerializer.Serialize(new
        {
            documentType = "بيان دعوى",
            borrowerName,
            borrowerFather,
            borrowerFamily,
            applicant,
            court,
            contractType = "تعهد",
            amountNumeric = 500,
            branchName = "الفرع الرئيسي - دمشق",
            fileNumber = registered ? $"9{Guid.NewGuid():N}"[..7] : null,
            fileYear = registered ? "2026" : null,
            fileRegistrationDate = registered ? "1/8/2026" : null,
            assets = withEstate
                ? new[] { new { assetKind = "عقار", property = "بيت", propertyNumber = "12345", propertyDistrict = "المزة", landRegistry = "الصالحية", shareType = "تمام العقار", owners = new[] { "المدعى عليه" }, seizureDate = withSeizureDate ? "1/8/2026" : null } }
                : Array.Empty<object>(),
        });
        var response = await client.PostAsync("/api/documents",
            new StringContent(body, Encoding.UTF8, "application/json"));
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"create doc {(int)response.StatusCode}: {content}");
        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("id").GetInt32();
    }
}

/// <summary>حقن قيمة التوكن في Cookie المصادقة على عميل مع زوج Cookie/ترويسة CSRF متناسق
/// (بديل العميل الحقيقي للاختبارات السريعة).</summary>
public static class AuthCookieTestExtensions
{
    public static void SetAuthCookie(this HttpClient client, string token)
    {
        const string csrf = "test-csrf-token";
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", $"docgen_token={token}; docgen_csrf={csrf}");
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", csrf);
    }
}

public sealed record LoginResult(HttpClient Client, string? Token, int StatusCode, string Content);