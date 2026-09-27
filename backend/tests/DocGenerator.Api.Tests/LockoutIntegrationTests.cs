using System.Net;
using System.Net.Http.Json;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

[Collection(ApiTestCollection.Name)]
public class LockoutIntegrationTests
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

    /// <summary>
    /// التراجع الأسّي (S6): قفل متتالٍ دون نجاح بينهما يضاعف المدة (15 ← 30)،
    /// والنجاح بين القفلين يعيد المستوى إلى الأساس.
    /// </summary>
    [Fact]
    public async Task RepeatedLockouts_BackoffDoublesDuration()
    {
        var username = $"lb_{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(username, UserRole.Lawyer, password: "123456");

        using var factory = CreateLockoutFactory(maxFailedAttempts: 2, lockoutMinutes: 15);
        await LoginAsync(factory, username, "wrong");
        await LoginAsync(factory, username, "wrong");
        Assert.Equal(15, await LockoutMinutesRemainingAsync(factory, username), 1);

        ExpireLockout(factory, username);
        await LoginAsync(factory, username, "wrong");
        Assert.Equal(30, await LockoutMinutesRemainingAsync(factory, username), 1);
    }

    [Fact]
    public async Task SuccessfulLogin_BetweenLockouts_ResetsBackoffLevel()
    {
        var username = $"lr_{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(username, UserRole.Lawyer, password: "123456");

        using var factory = CreateLockoutFactory(maxFailedAttempts: 2, lockoutMinutes: 15);
        await LoginAsync(factory, username, "wrong");
        await LoginAsync(factory, username, "wrong");
        ExpireLockout(factory, username);

        var ok = await LoginAsync(factory, username, "123456");
        Assert.Equal((int)HttpStatusCode.OK, ok.StatusCode);

        await LoginAsync(factory, username, "wrong");
        await LoginAsync(factory, username, "wrong");
        Assert.Equal(15, await LockoutMinutesRemainingAsync(factory, username), 1);
    }

    private static async Task<double> LockoutMinutesRemainingAsync(
        WebApplicationFactory<Program> factory, string username)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var user = db.Users.Single(u => u.Username == username);
        Assert.NotNull(user.LockoutEndUtc);
        return (user.LockoutEndUtc!.Value - DateTime.UtcNow).TotalMinutes;
    }

    private static void ExpireLockout(WebApplicationFactory<Program> factory, string username)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var user = db.Users.Single(u => u.Username == username);
        user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(-1);
        db.SaveChanges();
    }

    private int BranchId(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return db.Branches.Single(b => b.Code == code).Id;
    }

    /// <summary>
    /// إشارة القفل للهويات متعددة الفروع (R1): إن كانت كل الحسابات المرشحة مقفلة يُرجع
    /// `423` مباشرة بدل منتقي فرع لحسابات لا يقبل أيٌّ منها الدخول.
    /// </summary>
    [Fact]
    public async Task AllCandidatesLocked_Returns423InsteadOfBranchSelection()
    {
        var username = $"ml_{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(username, UserRole.Lawyer, branchId: BranchId("DAM"), password: "123456");
        await _factory.CreateUserAsync(username, UserRole.Lawyer, branchId: BranchId("ALP"), password: "123456");

        using var factory = CreateLockoutFactory(maxFailedAttempts: 2);
        // قفل الحسابين: فشلتان لكل فرع عبر اختيار الفرع صراحةً.
        foreach (var code in new[] { "DAM", "ALP" })
        {
            var client = factory.CreateClient();
            for (var i = 0; i < 2; i++)
            {
                await client.PostAsJsonAsync("/api/auth/login",
                    new { username, password = "wrong", branchId = BranchId(code) });
            }
        }

        var locked = await LoginAsync(factory, username, "123456");
        Assert.Equal((int)HttpStatusCode.Locked, locked.StatusCode);
    }

    [Fact]
    public async Task MixedLockState_ReturnsBranchSelection()
    {
        var username = $"mx_{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(username, UserRole.Lawyer, branchId: BranchId("DAM"), password: "123456");
        await _factory.CreateUserAsync(username, UserRole.Lawyer, branchId: BranchId("ALP"), password: "123456");

        using var factory = CreateLockoutFactory(maxFailedAttempts: 2);
        var damClient = factory.CreateClient();
        await damClient.PostAsJsonAsync("/api/auth/login",
            new { username, password = "wrong", branchId = BranchId("DAM") });
        await damClient.PostAsJsonAsync("/api/auth/login",
            new { username, password = "wrong", branchId = BranchId("DAM") });

        // حساب واحد مقفل والآخر مفتوح → يبقى منتقي الفرع (تجربة مشروعة).
        var probe = factory.CreateClient();
        var response = await probe.PostAsJsonAsync("/api/auth/login", new { username, password = "123456" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("requiresBranchSelection", body);
    }
}
