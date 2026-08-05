using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DocGenerator.Application.Common.Interfaces;
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
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            try { File.Delete(_dbPath + suffix); } catch { /* ignore */ }
        }
    }

    public async Task<LoginResult?> LoginAsync(string username, string password)
    {
        var client = CreateClient();
        var body = JsonSerializer.Serialize(new { username, password });
        var response = await client.PostAsync("/api/auth/login",
            new StringContent(body, Encoding.UTF8, "application/json"));
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            return new LoginResult(null, (int)response.StatusCode, content);

        using var doc = JsonDocument.Parse(content);
        var token = doc.RootElement.GetProperty("token").GetString();
        return new LoginResult(token, (int)response.StatusCode, content);
    }

    public HttpClient AuthorizedClient(string username, string password = "123456")
    {
        var login = LoginAsync(username, password).GetAwaiter().GetResult();
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login?.Token ?? string.Empty);
        return client;
    }

    public async Task<User> CreateUserAsync(string username, UserRole role, int? branchId = null, string password = "123456")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = new User
        {
            Username = username,
            FullName = username,
            Role = role,
            BranchId = branchId,
            PasswordHash = hasher.Hash(password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<int> CreateDocumentAsync(string token, string borrowerName = "مقترض",
        string? applicant = "المدعي", string? court = "دمشق",
        string? borrowerFather = null, string? borrowerFamily = null,
        string? lawyer = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var body = JsonSerializer.Serialize(new
        {
            documentType = "بيان دعوى",
            borrowerName,
            borrowerFather,
            borrowerFamily,
            applicant,
            court,
            lawyer,
            contractType = "تعهد",
            amountNumeric = 500,
            branchName = "الفرع الرئيسي - دمشق",
        });
        var response = await client.PostAsync("/api/documents",
            new StringContent(body, Encoding.UTF8, "application/json"));
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("id").GetInt32();
    }
}

public sealed record LoginResult(string? Token, int StatusCode, string Content);
