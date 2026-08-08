using System.Security.Claims;
using System.Text;
using DocGenerator.Api.Middleware;
using DocGenerator.Application;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Options;
using DocGenerator.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var conn = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=docgen.db";
var usePostgres = builder.Configuration.GetValue<bool>("Database:UsePostgres");

if (usePostgres)
{
    // التحقق عبر محلّل Npgsql نفسه: أي سلسلة يقبلها (postgres:// أو postgresql:// أو كلمات مفتاحية)
    // تعمل؛ ولا نمنع إلا ما لا يمكن تحليله فعلًا ليعطي رسالة تشخيصية واضحة.
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
            "Database:UsePostgres=true requires ConnectionStrings__DefaultConnection to be a "
            + "valid Postgres connection string (e.g. postgres://user:pass@host:5432/dbname). "
            + $"Current value (masked): {(string.IsNullOrWhiteSpace(conn) ? "empty" : $"first char '{conn[0]}', length {conn.Length}")}. "
            + "Set the variable in Render > Service docgen > Settings > Environment exactly as "
            + "ConnectionStrings__DefaultConnection and redeploy.");
    }
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

app.UseExceptionHandler();

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

app.MapControllers();

// كل مسارات SPA غير المعروفة تعود إلى index.html (تُستخدم مع خدمة الملفات الثابتة أعلاه).
if (!builder.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html");
}

app.Run();

public partial class Program { }
