using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
