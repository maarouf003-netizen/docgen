using System.Text;
using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Api.Tests.TestHelpers;
using DocGenerator.Api.Tests.TestServices;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Respawn.Graph;

namespace DocGenerator.Api.Tests;

/// <summary>
/// WebApplicationFactory الافتراضي بقاعدة <c>PostgreSQL</c> فريدة لكل مجموعة اختبارات
/// (هجرات EF الحقيقية + بذر التطوير + <see cref="ResetAsync"/> عبر <c>Respawn</c>)،
/// مع إعدادات صريحة (Secret، RateLimiting، Swagger) — تطبيق حقيقي من Program.cs.
/// يُستبدل مشتّق كلمات المرور الثقيل (PBKDF2-SHA256 بـ600k تكرار حاليًا) بنسخة سريعة مكافئة للسلوك
/// (<see cref="FastTestPasswordHasher"/>) لأن تكلفته تُدفع عند كل تسجيل دخول/إنشاء مستخدم.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// ثوابت سكّ التوكن الاختباري — مطابقة حرفيًا لإعدادات المصنع (`Jwt:Secret`
    /// أدناه) ولقيم التحقق الافتراضية (المُصدِر/الجمهور)؛ المرجع الوحيد لسكّ
    /// الرموز خارج بوابة الدخول (كـ`MintToken` في `MissingRoleClaimTests`).
    /// </summary>
    public const string TestJwtSecret = "integration-test-secret-0123456789-0123456789-0123456789";
    public const string TestJwtIssuer = "DocGenerator";
    public const string TestJwtAudience = "DocGeneratorClients";

    private readonly string _logDir = Path.Combine(
        Path.GetTempPath(), $"docgen_it_logs_{Guid.NewGuid():N}");

    private string? _connectionString;
    private PostgresTestDb? _database;

    /// <summary>
    /// يبني المضيف (الترحيلات + البذر) فورًا على خيط بلا <see cref="SynchronizationContext"/>،
    /// فلا تلتقط استمراريات <c>Program.cs</c> الداخلية سياق xUnit المتوازي (سبب قفل ميت
    /// موثّق عندما تعمل عدة مصانع مستقلة بالتوازي على عدّاء ضعيف الأنوية).
    /// </summary>
    public ApiFactory()
    {
        _ = Task.Run(() => Services).GetAwaiter().GetResult();
    }

    /// <summary>سلسلة اتصال القاعدة المعزولة لهذا المصنع (بعد بناء المضيف).</summary>
    public string ConnectionString =>
        _connectionString
        ?? throw new InvalidOperationException("لم تُهيَّأ قاعدة Postgres بعد.");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // الحارس H-02 (Program.cs) يشترط راية صريحة لبيئة Development. المصنع تطويري مقصود
        // (قاعدة اختبار معزولة + بذر اختباري)، فتُعلَن الراية هنا بدل تعطيل الحارس.
        builder.UseSetting("Security:AllowDevelopmentEnvironment", "true");
        ConfigureDatabaseSettings(builder);
        // عزل ملف سجل Serilog عن المستودع: كل مصنع يكتب في مجلد مؤقت خاص به.
        builder.UseSetting("Logging:File:Path", Path.Combine(_logDir, "logs-.txt"));
        // توليد المستندات معطَّل افتراضيًا في الإنتاج (`appsettings.json`)؛ اختبارات
        // التوليد تحتاج تفعيله صراحةً في المصنع الاختباري.
        builder.UseSetting("DocumentGeneration:Enabled", "true");
        // مشغّل صندوق الصادر معطَّل في اختبارات التكامل: يعمل على القاعدة نفسها
        // فيتنافس مع معاملات الاختبار على الصفوف، ومشغّله مغطّى باختبارات وحدة مستقلة.
        builder.UseSetting("HeadAlerts:Outbox:Enabled", "false");
        // احتفاظ المنتدى معطَّل: تمريرة الإقلاع الفورية كانت تكتب على القاعدة
        // بالتوازي مع تهيئة الاختبار، فتُنتج تذبذبًا في العدّادات (مراجعة المرحلة 6).
        builder.UseSetting("Forum:RetentionMonths", "0");
        // تجاوز كلمة بذر التطوير بقيمة اختبارية ثابتة (RF-006): القاعدة هنا معزولة
        // (قاعدة Postgres فريدة) وتُحذَف في Dispose، فلا يصل هذا التجاوز لأي بيئة حقيقية.
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
        builder.UseSetting("RateLimiting:CspReportPerMinute", "100000");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IPasswordHasher));
            if (descriptor is not null)
                services.Remove(descriptor);
            services.AddScoped<IPasswordHasher, FastTestPasswordHasher>();

            // اختبارات البناء القديم تنشئ ملفات بلا دائرة؛ المفتاح للاختبار فقط،
            // ولا يوجد أي مسار إعداد في الإنتاج يفعّله.
            var registryOptions = services.SingleOrDefault(d => d.ServiceType == typeof(NumberRegistryOptions));
            if (registryOptions is not null)
                services.Remove(registryOptions);
            services.AddSingleton(new NumberRegistryOptions { AllowCircuitlessLegacy = true });

            // P1-02b: ساعة اختبار ثابتة — لا تعتمد النتائج على تاريخ تشغيل الـCI.
            var clock = services.SingleOrDefault(d => d.ServiceType == typeof(TimeProvider));
            if (clock is not null)
                services.Remove(clock);
            services.AddSingleton<TimeProvider>(ApiTestClock.Provider);
        });

        // P1-02b: التحقق من عمر التوكن يستخدم الساعة الثابتة نفسها التي يسكّ بها
        // `TokenService` التوكن؛ وإلا صار التوكن منتهيًا فور إقلاع المصنع.
        builder.ConfigureTestServices(services =>
        {
            ConfigureDatabaseServices(services);

            // P1-02b: التحقق من عمر التوكن يستخدم الساعة الثابتة نفسها التي يسكّ بها
            // `TokenService` التوكن؛ وإلا صار التوكن منتهيًا فور إقلاع المصنع.
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.TimeProvider = ApiTestClock.Provider;
                    // مكتبة التحقق لا تقرأ TimeProvider في مسار العمر دائمًا؛ نثبّت
                    // الفحص على ساعة المصنع نفسها التي سكّت التوكن.
                    options.TokenValidationParameters.LifetimeValidator =
                        (notBefore, expires, _, _) =>
                        {
                            var now = ApiTestClock.UtcNow.UtcDateTime;
                            return (notBefore is null || notBefore <= now)
                                && (expires is null || now <= expires);
                        };
                });
        });
    }

    /// <summary>
    /// ينشئ قاعدة <c>PostgreSQL</c> فريدة لهذا المصنع ويشغّل هجرات EF الحقيقية ثم البذر
    /// (عبر <c>DatabaseInitializer</c> في Startup). إعادة البناء (‏`WithWebHostBuilder`)
    /// تُعيد استخدام القاعدة نفسها بدل إنشاء قاعدة جديدة في كل نداء.
    /// </summary>
    protected virtual void ConfigureDatabaseSettings(IWebHostBuilder builder)
    {
        if (_connectionString is not null)
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
            builder.UseSetting("Database:AutoMigrate", "true");
            return;
        }

        // قالب مهاجَر مرة واحدة لكل تشغيل ثم استنساخ (~1s) بدل تشغيل الهجرات (5–8s)
        // في كل مصنع. مسار الهجرات الكامل يبقى مغطّى في DatabaseInitializerTests
        // وPostgresBaselineTests، فلا يُستبدل التحقق.
        _database = PostgresTestDb.CreateMigrated();
        _connectionString = _database.ConnectionString;

        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        // الهجرات الحقيقية مقصودة: هذا المسار يختبر مخطط الإنتاج لا EnsureCreated.
        builder.UseSetting("Database:AutoMigrate", "true");
    }

    /// <summary>
    /// يعيد تسجيل السياق الحقيقي مع اعتراض <see cref="QueryCounter"/> (حارس N+1):
    /// يبقى مزوّد `PostgreSQL` والهجرات كما هي، ويُحقن العدّاد في خيارات السياق
    /// لقياس عدد الأوامر لكل طلب من الاختبار. Singleton لأنه يُقرأ بعد الطلب.
    /// </summary>
    protected virtual void ConfigureDatabaseServices(IServiceCollection services)
    {
        var queryCounter = new QueryCounter();
        services.AddSingleton(queryCounter);

        for (var i = services.Count - 1; i >= 0; i--)
        {
            var serviceType = services[i].ServiceType;
            if (serviceType == typeof(DocGeneratorDbContext)
                || serviceType == typeof(DocGeneratorPostgresDbContext)
                || serviceType == typeof(DbContextOptions)
                || serviceType == typeof(DbContextOptions<DocGeneratorDbContext>)
                || serviceType == typeof(DbContextOptions<DocGeneratorPostgresDbContext>))
            {
                services.RemoveAt(i);
            }
        }

        services.AddDbContext<DocGeneratorDbContext, DocGeneratorPostgresDbContext>((sp, options) =>
            options
                .UseNpgsql(ConnectionString, npgsql => npgsql.CommandTimeout(180))
                .AddInterceptors(sp.GetRequiredService<QueryCounter>()));
    }

    /// <summary>
    /// يفرّغ كل الجداول (عدا سجل الهجرات) عبر <c>Respawn</c> ثم يعيد بذر بيانات
    /// التطوير الثابتة، فيبدأ كل اختبار من حالة معروفة بلا الاعتماد على ترتيب التنفيذ.
    /// </summary>
    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        var respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            TablesToIgnore = [new Table("public", "__EFMigrationsHistory")],
            WithReseed = true,
        });
        await respawner.ResetAsync(connection);

        // `Respawn` يمسح القاعدة وحدها؛ ذاكرة التطبيق (`IMemoryCache`) تبقى حيّة بين
        // الاختبارات، فمُخنِّقات مبنية على هوية مستخدم تتسرب بين اختبارات متتالية بعد
        // إعادة البذر لأن معرفات المستخدمين تعود للبداية. تفريغها يجعل `ResetAsync` فعلًا
        // «حالة معروفة» لا «قاعدة معروفة فقط».
        if (Services.GetService<IMemoryCache>() is MemoryCache cache)
            cache.Clear();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await DbSeeder.SeedAsync(db, hasher, devPassword: "123456");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        // مجلد السجل يُنظَّف دائمًا (حتى لو لم تُنشأ قاعدة أو غابت سلسلة Postgres).
        if (disposing)
        {
            try { if (Directory.Exists(_logDir)) Directory.Delete(_logDir, recursive: true); } catch { /* ignore */ }
        }

        if (!disposing || _database is null)
            return;

        try
        {
            // متزامن عن قصد: هذا المسار داخل `Dispose` على خيط xUnit، و`await` هنا
            // يعود إلى سياق محجوب فيُجمِد الجولة (‏`docs/audit/80-…` §3).
            _database.Dispose();
        }
        catch
        {
            // لا تُفشل جولة الاختبار بسبب تنظيف قاعدة مؤقتة.
        }
    }

    private static int? BranchIdFromToken(string token)
    {
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
            return null;
        var claim = handler.ReadJwtToken(token).Claims
            .FirstOrDefault(c =>
                string.Equals(c.Type, "branch_id", StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Type, "branchId", StringComparison.OrdinalIgnoreCase));
        return int.TryParse(claim?.Value, out var branchId) ? branchId : null;
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
        // القيد CK_Documents_FileNumberRequiresCircuit: كل مستند مرقّم في اختبارات
        // Postgres يحتاج دائرة حقيقية، وإلا صار الطلب غير صالح (400) لا خطأ خادم.
        int? circuitId = null;
        if (registered)
        {
            var branchId = BranchIdFromToken(token)
                ?? throw new InvalidOperationException(
                    "CreateDocumentAsync(registered: true) يتطلب رمزًا يحمل branchId.");
            circuitId = await this.EnsureCircuitForBranchAsync(
                branchId, string.IsNullOrWhiteSpace(court) ? "دمشق" : court);
        }

        var body = JsonSerializer.Serialize(new
        {
            documentType = "بيان دعوى",
            borrowerName,
            borrowerFather,
            borrowerFamily,
            applicant,
            court,
            executionCircuitId = circuitId,
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
