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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}");
        builder.UseSetting("Database:UsePostgres", "false");
        builder.UseSetting("Swagger:Enabled", "false");
        builder.UseSetting("Jwt:Secret", "integration-test-secret-0123456789-0123456789-0123456789");
        builder.UseSetting("RateLimiting:MaxLoginAttempts", "5");
        builder.UseSetting("RateLimiting:WindowMinutes", "5");

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

    /// <summary>عميل مُصادَق عبر Cookie الدخول الفعلي (POST /api/auth/login).</summary>
    public HttpClient AuthorizedClient(string username, string password = "123456")
    {
        var login = LoginAsync(username, password).GetAwaiter().GetResult();
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
        bool withEstate = false)
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
            assets = withEstate
                ? new[] { new { assetKind = "عقار", property = "بيت", propertyNumber = "12345", propertyDistrict = "المزة", landRegistry = "الصالحية", shareType = "تمام العقار", owners = new[] { "المدعى عليه" } } }
                : Array.Empty<object>(),
        });
        var response = await client.PostAsync("/api/documents",
            new StringContent(body, Encoding.UTF8, "application/json"));
        var content = await response.Content.ReadAsStringAsync();
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