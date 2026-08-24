using System.Net;
using System.Security.Claims;
using System.Text;
using DocGenerator.Api.Auth;
using DocGenerator.Api.Middleware;
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

var builder = WebApplication.CreateBuilder(args);

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
if (string.IsNullOrWhiteSpace(jwt.Secret))
    throw new InvalidOperationException(
        "Jwt:Secret is required. Configure it via appsettings.Development.json, `dotnet user-secrets`, or the Jwt__Secret environment variable.");

var swaggerEnabled = builder.Environment.IsDevelopment()
    || builder.Configuration.GetValue<bool>("Swagger:Enabled");

var wordTemplates = builder.Configuration.GetSection("WordTemplates").Get<WordTemplatesOptions>()
    ?? new WordTemplatesOptions();
if (!Path.IsPathRooted(wordTemplates.Path))
    wordTemplates.Path = Path.Combine(builder.Environment.ContentRootPath, wordTemplates.Path);

builder.Services
    .AddSingleton(jwt)
    .Configure<DocGenerator.Application.Common.ExportOptions>(builder.Configuration.GetSection("Export"))
    .Configure<RateLimitOptions>(builder.Configuration.GetSection("RateLimiting"))
    .Configure<LockoutOptions>(builder.Configuration.GetSection("Lockout"))
    .Configure<WordTemplatesOptions>(o =>
    {
        o.Path = wordTemplates.Path;
        o.Templates = new Dictionary<string, string>(wordTemplates.Templates);
    })
    .AddApplication()
    .AddInfrastructure(conn, usePostgres)
    .AddCors(o => o.AddPolicy("Vite", p => p
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader().AllowAnyMethod().AllowCredentials()))
    .AddControllers();

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

                // إبطال: حساب ملغي/معطل، أو نسخة توكن قديمة (تغيّرت كلمة المرور)
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
{
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;

    var known = builder.Configuration.GetSection("Security:KnownProxies").Get<string[]>() ?? [];
    foreach (var entry in known)
    {
        var item = entry.Trim();
        if (item.Length == 0)
            continue;
        if (item.Contains('/'))
        {
            var parts = item.Split('/');
            if (parts.Length == 2
                && IPAddress.TryParse(parts[0], out var networkAddress)
                && int.TryParse(parts[1], out var prefix))
            {
                o.KnownIPNetworks.Add(new System.Net.IPNetwork(networkAddress, prefix));
                continue;
            }
        }
        if (IPAddress.TryParse(item, out var ip))
            o.KnownProxies.Add(ip);
    }
});

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

var app = builder.Build();

// أول وسيط في السلسلة حتى تعكس Request.Scheme وRemoteIpAddress البروتوكول والعنوان الحقيقيين
// للعميل (المعالجة تعتمد على وكلاء معروفين فقط؛ بلا وكيل تُتجاهَل كل الترويسات فتبقى الحالة مغلقة).
app.UseForwardedHeaders();
app.UseExceptionHandler();
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
        builder.Configuration["Bootstrap:AdminPassword"])
        .GetAwaiter().GetResult();
}

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Vite");
app.UseAuthentication();
app.UseAuthorization();
// عزل بنيوي لدور مندوب الجهة: يُمنع من كل مسارات API عدا بوابته القرائية (المرحلة 3).
app.UseMiddleware<EntityManagerPortalGuard>();

app.MapControllers();

// كل مسارات SPA غير المعروفة تعود إلى index.html (تُستخدم مع خدمة الملفات الثابتة أعلاه).
if (!builder.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html");
}

app.Run();

public partial class Program { }
