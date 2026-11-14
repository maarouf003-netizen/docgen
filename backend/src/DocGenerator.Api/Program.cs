using System.Security.Claims;
using System.Text;
using DocGenerator.Api.Auth;
using DocGenerator.Api.Http;
using DocGenerator.Api.Middleware;
using DocGenerator.Api.Security;
using DocGenerator.Application;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Options;
using DocGenerator.Infrastructure;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{

var builder = WebApplication.CreateBuilder(args);

// H-02 (تشديد الإنتاج): بيئة Development تُسقط خمسة ضوابط دفعةً واحدة — تطهير رسائل
// الاستثناء (فيعود النص الخام إلى المستخدم)، والترحيل التلقائي عند الإقلاع، وSwagger،
// وCORS لأصول localhost مع AllowCredentials، وSecure على كوكي CSRF. متغيّر بيئة واحد
// على خادم منشور يفتحها كلها، فتُشترط راية صريحة؛ ويُستثنى زمن تصميم EF (dotnet ef)
// لأن المضيف يُبنى هناك بلا خدمة تُواجه المستخدمين.
if (builder.Environment.IsDevelopment()
    && !builder.Configuration.GetValue<bool>("Security:AllowDevelopmentEnvironment")
    && !EF.IsDesignTime)
{
    throw new InvalidOperationException(
        "Refusing to start in Development: it disables response sanitization and enables auto-migrate, "
        + "Swagger and localhost CORS. For an intentional local run set "
        + "Security__AllowDevelopmentEnvironment=true (launchSettings/user-secrets/environment variable). "
        + "Never enable it on a deployed host.");
}

// لا بصمة خادم في الترويسات (S3): إخفاء `Server: Kestrel` من كل رد.
// سقف حجم الطلب 10MB (S4): يمنع الحمولات الضخمة قبل وصولها لخدمات `PBKDF2/Excel/Word`.
builder.WebHost.UseKestrel(o =>
{
    o.AddServerHeader = false;
    o.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
    o.MultipartBodyLengthLimit = 10 * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o =>
    o.SerializerOptions.MaxDepth = 32);

// ADR-0001: PostgreSQL هو مزوّد قاعدة البيانات الوحيد في التشغيل — لا مسار SQLite إنتاجي.
// يدعم المضيفون السحابيون ربط قاعدة Postgres فيحقنون DATABASE_URL تلقائيًا بصيغة postgres://؛
// وهو مصدر موثوق يغني عن اللصق اليدوي لسلسلة الاتصال.
// قيمة فارغة/بيضاء تُعامَل كغائبة — وإلا فازت "" على سلسلة الاتصال عبر ??.
var databaseUrl = builder.Configuration["DATABASE_URL"];
if (string.IsNullOrWhiteSpace(databaseUrl))
    databaseUrl = null;

var rawConn = databaseUrl
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(rawConn))
{
    throw new InvalidOperationException(
        "PostgreSQL connection string is required: set DATABASE_URL to a postgres:// URL "
        + "(as injected by hosting platforms) or ConnectionStrings__DefaultConnection to a keyword-style "
        + "Npgsql connection string. SQLite is not supported at runtime (ADR-0001).");
}

var conn = PostgresConnectionString.Normalize(rawConn);

// بعد التحويل يجب أن تكون القيمة بصيغة كلامية يقبلها Npgsql دائمًا؛ هذا الحارس يفضح أي تشوّه
// (ومنها سلسلة SQLite قديمة بقيت في إعداد منشور).
var parseable = false;
try
{
    _ = new NpgsqlConnectionStringBuilder(conn);
    parseable = true;
}
catch (ArgumentException)
{
}
if (!parseable)
{
    throw new InvalidOperationException(
        "The configured connection string is not a valid PostgreSQL connection string "
        + $"(e.g. Host=...;Port=...;Database=... or a postgres:// URL). "
        + $"Raw value (masked): {DescribeRawValue(rawConn)}. "
        + "Provide ConnectionStrings__DefaultConnection, or set the DATABASE_URL environment "
        + "variable to a postgres:// URL (as injected by most hosting platforms), then redeploy. "
        + "SQLite is not supported at runtime (ADR-0001).");
}

static string DescribeRawValue(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw))
        return "empty";
    var uriLike = raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        || raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);
    var scheme = raw.Contains("://", StringComparison.Ordinal)
        ? raw[..raw.IndexOf("://", StringComparison.Ordinal)]
        : "keyword-style";
    return $"length={raw.Length}, scheme='{scheme}', uri-like={uriLike}, "
        + $"has-spaces={raw.Any(char.IsWhiteSpace)}, has-control={raw.Any(char.IsControl)}";
}

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
// مفتاح HMAC-SHA256 بحد أدنى 256 بت (S5): مفتاح قصير يقبل توقيعًا ضعيفًا قابلًا للتخمين.
if (string.IsNullOrWhiteSpace(jwt.Secret) || Encoding.UTF8.GetByteCount(jwt.Secret) < 32)
    throw new InvalidOperationException(
        "Jwt:Secret is required with at least 32 bytes (256-bit) of entropy. Configure it via appsettings.Development.json, `dotnet user-secrets`, or the Jwt__Secret environment variable.");

var swaggerEnabled = builder.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("Swagger:Enabled");

var wordTemplates = builder.Configuration.GetSection("WordTemplates").Get<WordTemplatesOptions>()
    ?? new WordTemplatesOptions();
if (!Path.IsPathRooted(wordTemplates.Path))
    wordTemplates.Path = Path.Combine(builder.Environment.ContentRootPath, wordTemplates.Path);

// Serilog: المدد كلها من الإعدادات لا مثبتة بالكود (Logging:LogLevel للمستويات + Logging:File للملف الدوار).
var fileLogging = builder.Configuration.GetSection("Logging:File").Get<LoggingOptions>() ?? new LoggingOptions();
if (!Enum.TryParse<LogEventLevel>(builder.Configuration["Logging:LogLevel:Default"], out var serilogMinimum))
    serilogMinimum = LogEventLevel.Information;
var isDevelopmentHost = builder.Environment.IsDevelopment();
// ملاحظة معمارية: Services.AddSerilog (لا Host.UseSerilog) عمدًا — الأخير يجمّد مسجلًا
// ثابتًا مشتركًا (ReloadableLogger.Freeze) فينفجر إقلاع المضيف الثاني في نفس العملية
// (مصانع WebApplicationFactory المتوازية في الاختبارات). هنا كل مضيف يملك مسجلّه الخاص.
var serilogConfig = new LoggerConfiguration()
    .MinimumLevel.Is(serilogMinimum)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName();
if (isDevelopmentHost)
    serilogConfig.WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
else
    serilogConfig.WriteTo.Console(new CompactJsonFormatter());
serilogConfig.WriteTo.File(
    fileLogging.Path,
    rollingInterval: RollingInterval.Day,
    retainedFileCountLimit: fileLogging.RetainedDays,
    fileSizeLimitBytes: fileLogging.FileSizeLimitBytes,
    rollOnFileSizeLimit: true,
    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
builder.Services.AddSerilog(serilogConfig.CreateLogger(), dispose: true);

// P0-03a: إعدادات إعادة المحاولة تُقرأ مرة واحدة وتُمرَّر لمزوّد EF. الافتراضي 0
// (معطّل)؛ التفعيل يصبح آمنًا مع الإستراتيجية الواعية بالمعاملة والنطاق لكل محاولة.
var databaseOptions = builder.Configuration
    .GetSection(DatabaseOptions.SectionName)
    .Get<DatabaseOptions>() ?? new DatabaseOptions();
databaseOptions.Validate();

builder.Services
    .AddSingleton(jwt)
    .Configure<DocGenerator.Application.Common.ExportOptions>(builder.Configuration.GetSection("Export"))
    .Configure<DocGenerator.Application.Common.ForumOptions>(builder.Configuration.GetSection("Forum"))
    .Configure<DocGenerator.Application.Common.DatabaseOptions>(builder.Configuration.GetSection("Database"))
    .Configure<DocGenerator.Application.Common.HeadAlertOutboxOptions>(builder.Configuration.GetSection(DocGenerator.Application.Common.HeadAlertOutboxOptions.SectionName))
    .Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimiting"))
    .Configure<SecurityOptions>(builder.Configuration.GetSection("Security"))
    .Configure<LockoutOptions>(builder.Configuration.GetSection("Lockout"))
    .Configure<LoggingOptions>(builder.Configuration.GetSection("Logging:File"))
    .Configure<DocumentGenerationOptions>(builder.Configuration.GetSection(DocumentGenerationOptions.SectionName))
    .Configure<WordTemplatesOptions>(o =>
    {
        o.Path = wordTemplates.Path;
        o.Templates = new Dictionary<string, string>(wordTemplates.Templates);
    })
    .AddApplication(builder.Configuration)
    .AddInfrastructure(conn, databaseOptions.RetryMaxCount, databaseOptions.RetryMaxDelaySeconds)
    .AddCors(o => o.AddPolicy("Vite", p => p
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader().AllowAnyMethod().AllowCredentials()))
    .AddControllers();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<DocGenerator.Api.Security.ExportConcurrencyGuard>();
builder.Services.AddRateLimiter(RateLimitingSetup.Configure);
// احتفاظ المنتدى: قيمة سالبة تُفشل الإقلاع مبكرًا (كحارس مفتاح JWT أعلاه) —
// `0` معطّل مشروع، والموجب أشهر الحذف الصلب التلقائي عدا المثبّتة.
var forumRetentionMonths = builder.Configuration.GetValue<int?>("Forum:RetentionMonths") ?? 6;
if (forumRetentionMonths < 0)
    throw new InvalidOperationException(
        "Forum:RetentionMonths must be 0 (disabled) or a positive number of months. "
        + "Negative retention is rejected at startup to prevent deleting the whole forum history.");
builder.Services.AddHostedService<DocGenerator.Api.Background.ForumRetentionHostedService>();
// مشغّل صندوق الصادر: يسلّم التنبيهات النظامية المرصودة داخل معاملة العمل.
// خدمة واحدة نشطة (قفل «نسخة واحدة»)، وصف واحد لكل دورة.
builder.Services.AddHostedService<DocGenerator.Infrastructure.Outbox.HeadAlertOutboxDispatcher>();
// RF-016: فحص صحة القاعدة — خارج /api فلا مصادقة ولا حارس بوابة؛ بلا حزم جديدة.
builder.Services.AddHealthChecks()
    .AddCheck<DocGenerator.Api.Health.DatabaseHealthCheck>("database");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            // تثبيت الخوارزمية (S5): المفتاح متماثل واحد فلا تُقبل أي خوارزمية أخرى.
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
        };
        o.Events = new JwtBearerEvents
        {
            // جلاسة المصادقة Cookie HttpOnly؛ يقرأها المُصدِّق كبديل لترويسة Authorization
            // (تُحترم الترويسة إن وُجدت، لمن يريد الاتصال البرمجي بالخادم).
            OnMessageReceived = context =>
            {
                var token = context.Request.Cookies[AuthCookie.Name];
                if (string.IsNullOrEmpty(context.Token) && !string.IsNullOrEmpty(token))
                    context.Token = token;
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var sub = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.Principal?.FindFirstValue("sub");
                if (!int.TryParse(sub, out var userId))
                {
                    context.Fail("invalid subject");
                    return;
                }

                var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                var user = await users.GetByIdAsync(userId, context.HttpContext.RequestAborted);
                var claimVersion = int.TryParse(
                    context.Principal?.FindFirstValue("token_version"), out var v) ? v : 0;

                // إبطال: حساب ملغي/معطل، أو نسخة توكن قديمة (تغيّرت كلمة المرور/الدور/الفرع — S1).
                if (user is null || !user.IsActive || user.TokenVersion != claimVersion)
                {
                    context.Fail("token revoked");
                    return;
                }

                // تحقق دفاعي للشعبة (قرار §2.16): `section_id` غير رقمي يُرفض،
                // والمختلف عن شعبة الحساب الحالية (نقل دائرة/إحلال) يُبطل الرمز —
                // يُجبر إعادة الدخول بدل العمل بنطاق قديم بصمت.
                var claimSectionRaw = context.Principal?.FindFirstValue("section_id");
                if (claimSectionRaw is not null)
                {
                    if (!int.TryParse(claimSectionRaw, out var claimSection) || user.SectionId != claimSection)
                        context.Fail("token section mismatch");
                }
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // سياسة الرئاسة الموحدة (قرار §2.21): رئيس قسم أو شعبة — تُطبَّق على السمات
    // مرحليًا مع فلترة النطاق لكل منطقة (§5–§12)، لا دفعة واحدة قبل اكتمالها.
    options.AddPolicy("HeadOrSubHead", policy => policy.RequireRole("head", "subhead"));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ForwardedHeaders موثوق: يعالج X-Forwarded-For / X-Forwarded-Proto فقط إذا جاء الطلب من
// وكيل معروف صراحةً في الإعدادات (Security:KnownProxies، عنوان IP أو نطاق CIDR). بلا أي وكيل
// معروف يبقى النظام مغلقًا ضد التزوير: أي ترويسة X-Forwarded-For يرسلها عميل مباشر تُتجاهَل.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
    ForwardedHeadersSetup.Configure(o, builder.Configuration.GetSection("Security:KnownProxies").Get<string[]>()));

builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "DocGenerator API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        },
    });
});

// رايات الحماية لكل بيئة (S3): الافتراضي مغلق لمنصة تتولى TLS بنفسها؛ المخدّم الخاص
// يفعّل عبر Security__HstsEnabled / Security__HttpsRedirectionEnabled (انظر RUN_GUIDE.md §9).
// تُقرأ قبل البناء لأن AddHsts تسجيل خدمة لا يجوز بعد تجميد الحاوية.
var security = builder.Configuration.GetSection("Security").Get<SecurityOptions>() ?? new SecurityOptions();
// التحقق دائم (لا عند التفعيل فقط): `preload` بلا `HstsEnabled` يُتجاهَل بصمت، وقيم
// `HstsMaxAgeDays` غير الموجبة أو التي تتجاوز السقف خطأ إعداد يُكتشف عند الإقلاع لا في المتصفح.
HstsSetup.Validate(security);
// التحقق من سياسة جمع تقارير CSP عند الإقلاع أيضًا: القيمة الخاطئة لم تعد تُتجاهل
// بصمت (كانت التقارير تُفقد بلا أثر)، بل تمنع الإقلاع برسالة تسمّي المفتاح والمثال.
CspSetup.Validate(security);
if (security.HstsEnabled)
{
    // المدة و`preload` مستقلان عن التفعيل: `preload` التزام شبه دائم في قوائم المتصفحات،
    // فلا يُفعَّل ضمنيًا مع `HstsEnabled`.
    builder.Services.AddHsts(o => HstsSetup.Configure(o, security));
}

var app = builder.Build();

// H-02b/H-04: تحذيران إقلاع صريحان بدل تدهور صامت — يُقرآن في سجل النشر مباشرة:
// (1) بلا وكيل معلَن تُتجاهَل `X-Forwarded-*` فيظهر كل الزوار بعنوان الوكيل (تقسيم حد المعدل
//     وسجلات التدقيق)، (2) `AllowedHosts` فارغ أو `*` يُعطّل فلترة المضيف التي يفرضها ملحق النقل.
// تحذير لا رفض: التعريض المباشر بلا وكيل حالة مشروعة. يُكتب عبر مسجّل المضيف (`app.Logger`)
// لا المسجّل العام `Log.Logger` حتى لا يتأثر أي مستهلك آخر للسجل الثابت (ومنها اختبارات المسبار).
if (!isDevelopmentHost)
{
    var configuredProxies = builder.Configuration.GetSection("Security:KnownProxies").Get<string[]>() ?? [];
    if (!configuredProxies.Any(entry => !string.IsNullOrWhiteSpace(entry)))
    {
        app.Logger.LogWarning(
            "Security:KnownProxies is empty outside Development: X-Forwarded-For/X-Forwarded-Proto are ignored, "
            + "so every client appears as the proxy address (rate-limit partitions and audit logs). "
            + "Set Security__KnownProxies to the reverse proxy address.");
    }

    // `AllowedHosts` يقبل أنماط نطاقات فرعية (`*.gov.sy`) وهي تضييق مشروع لا تعطيل؛
    // التحذير يخصّ الفراغ أو المدخل `*` وحده (الذي يُلغي الفلترة فعليًا).
    var allowedHosts = builder.Configuration["AllowedHosts"];
    var hostEntries = (allowedHosts ?? string.Empty)
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (hostEntries.Length == 0 || hostEntries.Contains("*", StringComparer.Ordinal))
    {
        app.Logger.LogWarning(
            "AllowedHosts is '{AllowedHosts}': host filtering is disabled. Set it to the final host plus the "
            + "proxy/health-check address (for example \"docgen.gov.sy;127.0.0.1\").",
            allowedHosts ?? "(unset)");
    }
}

// أول وسيط في السلسلة حتى تعكس Request.Scheme وRemoteIpAddress البروتوكول والعنوان الحقيقيين
// للعميل (المعالجة تعتمد على وكلاء معروفين فقط؛ وبلا وكيل معلَن تُعطَّل المعالجة كليًا
// — `ForwardedHeaders.None` في `ForwardedHeadersSetup` — فتبقى الحالة مغلقة).
app.UseForwardedHeaders();
// ترويسات الأمان على كل رد (S3) — قبل معالج الاستثناءات فتبقى حتى مع الردود الاستثنائية.
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseExceptionHandler();

if (security.HstsEnabled)
    app.UseHsts();
if (security.HttpsRedirectionEnabled)
    app.UseHttpsRedirection();

app.UseMiddleware<CsrfMiddleware>();

// توزيع من أصل واحد: الخلفية تخدم الواجهة المبنية (wwwroot) بنفس الأصل فيغني عن CORS في الإنتاج.
if (!builder.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    // ترويسات الكاش: قشرة SPA تُعيد التحقق دائمًا، والأصول المُبصَّمة تُخزَّن طويلًا —
    // وإلا بقي المتصفح على حزمة قديمة بعد النشر رغم تحديث الخادم.
    app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = SpaCacheHeaders.Apply });
}

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    initializer.InitializeAsync(
        builder.Environment.IsDevelopment(),
        builder.Configuration["Bootstrap:AdminPassword"],
        // تجاوز كلمة بذر التطوير (للاختبارات المعزولة فقط عبر ApiFactory) — بلا قيمة
        // هنا فيولّد البذر كلمة عشوائية تُطبَع على الكونسول المحلي (RF-006).
        devSeedPassword: builder.Configuration["Bootstrap:DevSeedPassword"])
        .GetAwaiter().GetResult();
}

// سطر إقلاع واحد يثبت في السجلات أي محرك قاعدة اشتغل فعلًا (مفيد في الاستضافة حيث لا فحص للآلة).
// يُطبع اسم المحرك فقط عمدًا — سلسلة الاتصال تحوي كلمة سر ولا تُسجَّل أبدًا.
var autoMigrate = builder.Configuration.GetValue("Database:AutoMigrate", builder.Environment.IsDevelopment());
Log.Information(
    "Database provider active: {DbProvider} (auto-migrate at startup: {AutoMigrate})",
    "postgres",
    autoMigrate);

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// الإنتاج أحادي الأصل (wwwroot بنفس الأصل) فلا يحتاج CORS: السياسة للتطوير فقط (S3)،
// وإبقاؤها عالميًا يُبقي أصلي localhost مسموحة مع AllowCredentials في الإنتاج.
if (builder.Environment.IsDevelopment())
    app.UseCors("Vite");
app.UseAuthentication();
// عزل بنيوي لدور مندوب الجهة: يُمنع من كل مسارات API عدا بوابته القرائية (المرحلة 3).
// RF-004: قبل التفويض عمدًا (بعد المصادقة التي تغذيه بالهوية) — فرفض الدور يُسجَّل
// (`portal_forbidden` في الحارس نفسه) قبل أن يقطعه وسيط التفويض بصمت عند تضييق
// سمات `[Authorize(Roles=...)]`؛ ولغير المندوب تمرير خالص بلا أي تغيير سلوكي.
app.UseMiddleware<EntityManagerPortalGuard>();
app.UseAuthorization();
// حد المعدل العام (S4) — بعد المصادقة عمدًا: مفاتيح التقسيم لكل مستخدم (userId) لا تتوفر
// في HttpContext.User إلا بعدها؛ وضعه قبلها كان يُسقط الكل إلى مفتاح IP فيتقاسم مستخدمو
// الشبكة الواحدة نفس الدلو. ثمنه المقبول: طلبات التوكن تعبر تحقق JWT (رخيص) قبل الخنق،
// بينما يبقى العمل المكلف (تصدير/PBKDF2) محميًا خلفه (انظر docs/SECURITY_PLAN.md §6).
app.UseRateLimiter();
// إثراء LogContext بالهوية بعد المصادقة (قبل المتحكمات) — Anonymous لغير المعتمد.
// ملاحظة الترتيب: معالج الاستثناءات أعلى السلسلة، لذا يسجل GlobalExceptionHandler
// المعرّف والهوية صراحة من HttpContext ولا يعتمد على نطاق LogContext هنا.
app.UseMiddleware<RequestLoggingEnricherMiddleware>();

// M-01 (جاهزية الإنتاج — دفاع عمقي): كل إجراءات المتحكمات تتطلب مصادقة افتراضيًا، فأي
// متحكم/إجراء جديد يُكتب بلا [Authorize] يبقى محميًا تلقائيًا. الاستثناءات صريحة بـ
// [AllowAnonymous] على الإجراء نفسه (الدخول/الخروج/سنة الخادم) وهي تتقدّم على هذا الافتراضي.
// قرار: تعمّدنا عدم استخدام AuthorizationOptions.FallbackPolicy لأنه يُطبَّق أيضًا على الطلبات
// بلا نقطة مطابقة (بفضل قيد `:nonfile` في احتياطي SPA) فيحوّل كل 404 إلى 401؛ وهذا الافتراضي
// على المتحكمات يحقق الهدف نفسه مع إبقاء دلالة «غير موجود» سليمة للعميل والموزّع.
app.MapControllers().RequireAuthorization();

// RF-016: نقطة الصحة قبل احتياطي SPA — لا تُبتلَع بواسطة index.html في الإنتاج.
// نقطة بنية عامة عمدًا (المنسّق/الموازن غير مصادقين) — نُثبّت [AllowAnonymous] صراحةً.
app.MapHealthChecks("/healthz").AllowAnonymous();

// كل مسارات SPA غير المعروفة تعود إلى index.html (تُستخدم مع خدمة الملفات الثابتة أعلاه).
// مسار عميل مجهول عمدًا (صفحة الدخول) — نُثبّت [AllowAnonymous] صراحةً.
if (!builder.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html",
        new StaticFileOptions { OnPrepareResponse = SpaCacheHeaders.Apply }).AllowAnonymous();
}

    app.Run();
}
catch (Exception ex)
{
    // إعادة الرمي إلزامية: ابتلاع استثناء الإقلاع يخفي الفشل عن المنسّق (خروج 0 بلا مستمع)
    // ويكسر WebApplicationFactory («exited without ever building an IHost»).
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
