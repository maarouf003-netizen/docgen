using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using DocGenerator.Domain.Enums;
using Microsoft.IdentityModel.Tokens;

namespace DocGenerator.Api.Tests;

/// <summary>
/// رمز بلا دور (أو بدور غريب): إغلاق صريح للفشل — يُرفض بـ403 لا يُتدهور إلى
/// دور افتراضي ولا ينفجر 500. يُسكّ الرمز بنفس سرعة الإصدار/الجمهور في
/// `ApiFactory` مع إسقاط `ClaimTypes.Role` وحدها.
/// </summary>
public sealed class MissingRoleClaimTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private int _lawyerId;

    public async Task InitializeAsync()
    {
        var user = await _factory.CreateUserAsync("norole_lawyer", UserRole.Lawyer, branchId: null);
        _lawyerId = user.Id;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static string MintToken(int userId, string username, string? role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            "integration-test-secret-0123456789-0123456789-0123456789"));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(ClaimTypes.Name, username),
            new("token_version", "0"),
        };
        if (role is not null)
            claims.Add(new Claim(ClaimTypes.Role, role));
        var token = new JwtSecurityToken(
            issuer: "DocGenerator",
            audience: "DocGeneratorClients",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private HttpClient ClientWithRoleClaim(string? role)
    {
        var client = _factory.CreateClient();
        client.SetAuthCookie(MintToken(_lawyerId, "norole_lawyer", role));
        return client;
    }

    [Fact]
    public async Task TokenWithoutRoleClaim_IsForbidden_NotServerError()
    {
        var response = await ClientWithRoleClaim(null).GetAsync("/api/correspondence");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TokenWithUnknownRole_IsForbidden_NotServerError()
    {
        var response = await ClientWithRoleClaim("ghost").GetAsync("/api/correspondence");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TokenWithValidRole_StillListsOwnScope()
    {
        // عاقل الضبط: الرمز السليم بالدور الصحيح يمرّ طبيعيًا (200 مع صفحة
        // فارغة) — فيُثبت أن الرفض أعلاه من غياب الدور وحده لا من سكّ الرمز.
        var response = await ClientWithRoleClaim("lawyer").GetAsync("/api/correspondence");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
