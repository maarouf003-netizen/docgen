using System.Security.Claims;
using System.Text;
using DocGenerator.Api.Auth;
using DocGenerator.Api.Middleware;
using DocGenerator.Api.Security;
using DocGenerator.Application;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Options;
using DocGenerator.Infrastructure;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

// يدعم المضيفون السحابيون ربط قاعدة Postgres فيحقنون DATABASE_URL تلقائيًا بصيغة postgres://؛
// وهو مصدر موثوق يغني عن اللصق اليدوي لسلسلة الاتصال.
var databaseUrl = builder.Configuration["DATABASE_URL"];
var usePostgres = builder.Configuration.GetValue<bool>("Database:UsePostgres")
    || !string.IsNullOrWhiteSpace(databaseUrl);

var rawConn = databaseUrl
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=docgen.db";
var conn = usePostgres ? PostgresConnectionString.Normalize(rawConn) : rawConn;

if (usePostgres)
{
    // بعد التحويل يجب أن تكون القيمة بصيغة كلامية يقبلها Npgsql دائمًا؛ هذا الحارس يفضح أي تشوّه.
    var parseable = false;
    if (!string.IsNullOrWhiteSpace(conn))
    {
        try
        {
            _ = new NpgsqlConnectionStringBuilder(conn);
            parseable = true;
        }
        catch (ArgumentException)
        {
        }
    }
    if (!parseable)
    {
        throw new InvalidOperationException(
            "Database:UsePostgres=true requires a valid Postgres connection string "
            + $"(e.g. Host=...;Port=...;Database=... or a postgres:// URL). "
            + $"Raw value (masked): {DescribeRawValue(rawConn)}. "
            + "Provide ConnectionStrings__DefaultConnection, or set the DATABASE_URL environment "
            + "variable to a postgres:// URL (as injected by most hosting platforms), then redeploy.");
    }
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

builder.Services
    .AddSingleton(jwt)
    .Configure<DocGenerator.Application.Common.ExportOptions>(builder.Configuration.GetSection("Export"))
    .Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimiting"))
    .Configure<SecurityOptions>(builder.Configuration.GetSection("Security"))
    .Configure<LockoutOptions>(builder.Configuration.GetSection("Lockout"))
    .Configure<LoggingOptions>(builder.Configuration.GetSection("Logging:File"))
    .Configure<WordTemplatesOptions>(o =>
    {
        o.Path = wordTemplates.Path;
        o.Templates = new Dictionary<string, string>(wordTemplates.Templates);
    })
    .AddApplication(builder.Configuration)
    .AddInfrastructure(conn, usePostgres)
    .AddCors(o => o.AddPolicy("Vite", p => p
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader().AllowAnyMethod().AllowCredentials()))
    .AddControllers();

builder.Services.AddMemoryCache();
builder.Services.AddRateLimiter(RateLimitingSetup.Configure);
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
                    context.Fail("token revoked");
            }
        };
    });

builder.Services.AddAuthorization();
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
if (security.HstsEnabled)
{
    // سنة + includeSubDomains + preload؛ لا يُفعَّل preload إلا على النطاق الإنتاجي الحقيقي.
    builder.Services.AddHsts(o =>
    {
        o.MaxAge = TimeSpan.FromDays(365);
        o.IncludeSubDomains = true;
        o.Preload = true;
    });
}

var app = builder.Build();

// أول وسيط في السلسلة حتى تعكس Request.Scheme وRemoteIpAddress البروتوكول والعنوان الحقيقيين
// للعميل (المعالجة تعتمد على وكلاء معروفين فقط؛ بلا وكيل تُتجاهَل كل الترويسات فتبقى الحالة مغلقة).
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
    app.UseStaticFiles();
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
Log.Information("Database provider active: {DbProvider} (migrations applied at startup)", usePostgres ? "postgres" : "sqlite");

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
// عزل بنيوي لدور مندوب الجهة: يُمنع من كل مسارات API عدا بوابته القرائية (المرحلة 3).
app.UseMiddleware<EntityManagerPortalGuard>();

app.MapControllers();

// RF-016: نقطة الصحة قبل احتياطي SPA — لا تُبتلَع بواسطة index.html في الإنتاج.
app.MapHealthChecks("/healthz");

// كل مسارات SPA غير المعروفة تعود إلى index.html (تُستخدم مع خدمة الملفات الثابتة أعلاه).
if (!builder.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html");
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
