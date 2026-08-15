using System.Net;
using System.Net.Http.Json;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

public class LockoutIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public LockoutIntegrationTests(ApiFactory factory) => _factory = factory;

    /// <summary>
    /// نسخة من المصنع تشترك بنفس ملف القاعدة لكن برفع سقف محدد IP+username
    /// حتى يظهر قفل الحساب قبل تحديد المعدل، وبهوامش قفل قابلة للضبط.
    /// </summary>
    private WebApplicationFactory<Program> CreateLockoutFactory(int maxFailedAttempts = 3, int lockoutMinutes = 15)
        => _factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("RateLimiting:MaxLoginAttempts", "100");
            b.UseSetting("Lockout:MaxFailedAttempts", maxFailedAttempts.ToString());
            b.UseSetting("Lockout:LockoutMinutes", lockoutMinutes.ToString());
        });

    private static async Task<(int StatusCode, string? Token)> LoginAsync(
        WebApplicationFactory<Program> factory, string username, string password)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        var token = ApiFactory.ExtractCookieValue(response, "docgen_token");
        return ((int)response.StatusCode, token);
    }

    [Fact]
    public async Task RepeatedFailures_EventuallyLockAccount_Return423()
    {
        var username = $"lk_{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(username, UserRole.Lawyer, password: "123456");

        using var factory = CreateLockoutFactory(maxFailedAttempts: 3);
        for (var i = 0; i < 3; i++)
        {
            var failed = await LoginAsync(factory, username, "wrong");
            Assert.Equal((int)HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        var locked = await LoginAsync(factory, username, "123456");
        Assert.Equal((int)HttpStatusCode.Locked, locked.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            Assert.Contains(db.AuditLogs, a => a.UserName == username && a.ActionType == "login_locked");
        }
    }

    [Fact]
    public async Task LockedAccount_CorrectPassword_Returns423()
    {
        var username = $"l2_{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(username, UserRole.Lawyer, password: "123456");

        using var factory = CreateLockoutFactory(maxFailedAttempts: 2);
        await LoginAsync(factory, username, "wrong");
        await LoginAsync(factory, username, "wrong");

        var result = await LoginAsync(factory, username, "123456");
        Assert.Equal((int)HttpStatusCode.Locked, result.StatusCode);
    }

    [Fact]
    public async Task AfterLockoutExpires_CorrectPasswordSucceeds()
    {
        var username = $"l3_{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(username, UserRole.Lawyer, password: "123456");

        using var factory = CreateLockoutFactory(maxFailedAttempts: 2, lockoutMinutes: 15);
        await LoginAsync(factory, username, "wrong");
        await LoginAsync(factory, username, "wrong");

        // محاكاة انتهاء مدة القفل بتمرير نهايتها إلى الماضي
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var user = db.Users.Single(u => u.Username == username);
            user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var result = await LoginAsync(factory, username, "123456");
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
    }

    [Fact]
    public async Task SuccessfulLogin_ResetsFailedAttempts()
    {
        var username = $"l4_{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(username, UserRole.Lawyer, password: "123456");

        using var factory = CreateLockoutFactory(maxFailedAttempts: 5);
        await LoginAsync(factory, username, "wrong");
        await LoginAsync(factory, username, "wrong");

        var ok = await LoginAsync(factory, username, "123456");
        Assert.Equal((int)HttpStatusCode.OK, ok.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var user = db.Users.Single(u => u.Username == username);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockoutEndUtc);
    }
}
