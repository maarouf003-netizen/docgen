using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

public class AuthIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthIntegrationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenWithLowercaseRole()
    {
        var result = await _factory.LoginAsync("manager", "123456");

        Assert.Equal((int)HttpStatusCode.OK, result!.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));

        var claims = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(result.Token!);
        var role = claims.Claims.First(c => c.Type.EndsWith("role", StringComparison.Ordinal))
            .Value;
        Assert.Equal("manager", role);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var result = await _factory.LoginAsync("lawyer1", "wrong-password");

        Assert.Equal((int)HttpStatusCode.Unauthorized, result!.StatusCode);
    }

    [Fact]
    public async Task Login_AfterMaxFailedAttempts_ReturnsTooManyRequests()
    {
        var username = $"rl_{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(username, UserRole.Lawyer);

        var statuses = new List<int>();
        for (var i = 0; i < 6; i++)
        {
            var result = await _factory.LoginAsync(username, "bad");
            statuses.Add(result!.StatusCode);
        }

        Assert.Equal(5, statuses.Count(s => s == (int)HttpStatusCode.Unauthorized));
        Assert.Equal((int)HttpStatusCode.TooManyRequests, statuses[5]);
    }

    [Fact]
    public async Task ChangePassword_FullFlow_Works()
    {
        var username = $"cp_{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(username, UserRole.Lawyer, password: "123456");

        var client = _factory.AuthorizedClient(username);
        var changeBody = JsonSerializer.Serialize(new { oldPassword = "123456", newPassword = "654321" });
        var change = await client.PostAsync("/api/auth/change-password",
            new StringContent(changeBody, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        var oldLogin = await _factory.LoginAsync(username, "123456");
        Assert.Equal(HttpStatusCode.Unauthorized, (HttpStatusCode)oldLogin!.StatusCode);

        var newLogin = await _factory.LoginAsync(username, "654321");
        Assert.Equal(HttpStatusCode.OK, (HttpStatusCode)newLogin!.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithShortPassword_ReturnsBadRequest()
    {
        var client = _factory.AuthorizedClient("head1");
        var body = JsonSerializer.Serialize(new { oldPassword = "123456", newPassword = "123" });
        var response = await client.PostAsync("/api/auth/change-password",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MutatingRequest_WithoutCsrfHeader_ReturnsForbidden()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        // عميل يحمل Cookie المصادقة فقط دون ترويسة CSRF (محاكاة طلب مزيّف لم يقرأ الـ Cookie)
        var bare = _factory.CreateClient();
        bare.DefaultRequestHeaders.Add("Cookie", $"docgen_token={token}");

        var response = await bare.PostAsJsonAsync("/api/auth/change-password",
            new { oldPassword = "123456", newPassword = "654321" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MutatingRequest_WithMismatchedCsrfHeader_ReturnsForbidden()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var bare = _factory.CreateClient();
        bare.DefaultRequestHeaders.Add("Cookie", $"docgen_token={token}; docgen_csrf=cookie-value");
        bare.DefaultRequestHeaders.Add("X-CSRF-Token", "different-value");

        var response = await bare.PostAsJsonAsync("/api/auth/change-password",
            new { oldPassword = "123456", newPassword = "654321" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Login_SetsCsrfCookie_WithSameLifetimeAsAuthCookie()
    {
        // كان الـ CSRF Cookie جلسةً قصيرة تُحذف عند إغلاق المتصفح بينما يبقى Cookie الجلسة ساريًا،
        // فيستمر الدخول بعد إعادة فتح المتصفح لكن كل طلب يغيّر الحالة (تعديل/حفظ) يُرفض بـ 403.
        // العقدة الحاسمة: يجب أن يتزامن عمراهما بالضبط ليبقى التعديل يعمل ما دامت الجلسة حيّة.
        var client = _factory.CreateClient();
        var body = JsonSerializer.Serialize(new { username = "lawyer1", password = "123456" });
        var response = await client.PostAsync("/api/auth/login",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authMaxAge = MaxAgeOf(response, "docgen_token");
        var csrfMaxAge = MaxAgeOf(response, "docgen_csrf");

        Assert.NotNull(authMaxAge);
        Assert.NotNull(csrfMaxAge);
        Assert.Equal(authMaxAge, csrfMaxAge);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsUser()
    {
        var client = _factory.AuthorizedClient("lawyer1");
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("lawyer1", doc.RootElement.GetProperty("username").GetString());
    }

    [Fact]
    public async Task ProtectedEndpoints_WithoutToken_ReturnUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/documents");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_DuplicateTripartiteNameAcrossBranches_ReturnsBranchSelection()
    {
        var branch1 = await GetBranchIdAsync("DAM");
        var branch2 = await GetBranchIdAsync("ALP");
        await _factory.CreateUserAsync("محمد أحمد علي", UserRole.Lawyer, branch1);
        await _factory.CreateUserAsync("محمد أحمد علي", UserRole.Lawyer, branch2);

        var result = await _factory.LoginAsync("محمد أحمد علي", "123456");
        Assert.Equal((int)HttpStatusCode.OK, result!.StatusCode);

        using var doc = JsonDocument.Parse(result.Content);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("requiresBranchSelection").GetBoolean());
        var branches = root.GetProperty("branches").EnumerateArray()
            .Select(b => b.GetProperty("branchId").GetInt32())
            .OrderBy(x => x)
            .ToArray();
        Assert.Equal(new[] { branch1, branch2 }, branches);
    }

    [Fact]
    public async Task Login_DuplicateTripartiteName_SelectingBranch_LogsIntoCalledBranch()
    {
        var branch1 = await GetBranchIdAsync("DAM");
        var branch2 = await GetBranchIdAsync("ALP");
        await _factory.CreateUserAsync("خالد علي حسن", UserRole.Lawyer, branch1);
        await _factory.CreateUserAsync("خالد علي حسن", UserRole.Lawyer, branch2);

        var result = await _factory.LoginAsync("خالد علي حسن", "123456", branch2);
        Assert.Equal((int)HttpStatusCode.OK, result!.StatusCode);

        using var doc = JsonDocument.Parse(result.Content);
        var root = doc.RootElement;
        Assert.Equal("lawyer", root.GetProperty("user").GetProperty("role").GetString());
        Assert.Equal(branch2, root.GetProperty("user").GetProperty("branchId").GetInt32());
    }

    [Fact]
    public async Task Login_DuplicateTripartiteName_WrongBranch_ReturnsUnauthorized()
    {
        var branch1 = await GetBranchIdAsync("DAM");
        var branch2 = await GetBranchIdAsync("ALP");
        await _factory.CreateUserAsync("سامر أحمد محمود", UserRole.Lawyer, branch1);
        await _factory.CreateUserAsync("سامر أحمد محمود", UserRole.Lawyer, branch2);

        var result = await _factory.LoginAsync("سامر أحمد محمود", "123456", 9999);
        Assert.Equal((int)HttpStatusCode.Unauthorized, result!.StatusCode);
    }

    [Fact]
    public async Task Login_DuplicateNameWithInactiveAccount_NoBranchSelectionNeeded()
    {
        var branch1 = await GetBranchIdAsync("DAM");
        var branch2 = await GetBranchIdAsync("ALP");
        await _factory.CreateUserAsync("حسن عمر فارس", UserRole.Lawyer, branch1);
        await _factory.CreateUserAsync("حسن عمر فارس", UserRole.Lawyer, branch2, isActive: false);

        var result = await _factory.LoginAsync("حسن عمر فارس", "123456");
        Assert.Equal((int)HttpStatusCode.OK, result!.StatusCode);

        using var doc = JsonDocument.Parse(result.Content);
        var root = doc.RootElement;
        Assert.False(root.TryGetProperty("requiresBranchSelection", out _));
        Assert.Equal(branch1, root.GetProperty("user").GetProperty("branchId").GetInt32());
    }

    [Fact]
    public async Task Login_WithLegacyUnsaltedSha256_UpgradeHashTransparently()
    {
        var username = $"lg_{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(username, UserRole.Lawyer);

        int tokenVersionBefore;
        string legacyHash;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<DocGenerator.Application.Common.Interfaces.IPasswordHasher>();
            var user = await db.Users.FirstAsync(u => u.Username == username);
            legacyHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("123456")))
                .ToLowerInvariant();
            user.PasswordHash = legacyHash;
            await db.SaveChangesAsync();
            tokenVersionBefore = user.TokenVersion;
            Assert.True(hasher.NeedsUpgrade(legacyHash));
        }

        var login = await _factory.LoginAsync(username, "123456");
        Assert.Equal(HttpStatusCode.OK, (HttpStatusCode)login!.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<DocGenerator.Application.Common.Interfaces.IPasswordHasher>();
            var user = await db.Users.AsNoTracking().FirstAsync(u => u.Username == username);
            Assert.NotEqual(legacyHash, user.PasswordHash);
            Assert.False(hasher.NeedsUpgrade(user.PasswordHash));
            Assert.True(hasher.Verify("123456", user.PasswordHash));
            Assert.Equal(tokenVersionBefore, user.TokenVersion);
        }

        var secondLogin = await _factory.LoginAsync(username, "123456");
        Assert.Equal(HttpStatusCode.OK, (HttpStatusCode)secondLogin!.StatusCode);
    }

    [Fact]
    public async Task Login_WithCanonicalFormat_DoesNotRehash()
    {
        var username = $"cn_{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(username, UserRole.Lawyer);

        string canonical;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var user = await db.Users.FirstAsync(u => u.Username == username);
            canonical = user.PasswordHash;
        }

        var login = await _factory.LoginAsync(username, "123456");
        Assert.Equal(HttpStatusCode.OK, (HttpStatusCode)login!.StatusCode);

        using (var verifyScope = _factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var user = await db.Users.AsNoTracking().FirstAsync(u => u.Username == username);
            Assert.Equal(canonical, user.PasswordHash);
        }
    }

    private static long? MaxAgeOf(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            return null;
        foreach (var value in setCookies)
        {
            if (value.IndexOf(name + "=", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            var match = Regex.Match(value, @"Max-Age=(\d+)", RegexOptions.IgnoreCase);
            return match.Success ? long.Parse(match.Groups[1].Value) : null;
        }
        return null;
    }

    private async Task<int> GetBranchIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var branch = await db.Branches.AsNoTracking().FirstAsync(b => b.Code == code);
        return branch.Id;
    }
}

public static class TokenExtensions
{
    public static HttpClient WithToken(this ApiFactory factory, string token)
        => factory.ClientWithToken(token);
}
