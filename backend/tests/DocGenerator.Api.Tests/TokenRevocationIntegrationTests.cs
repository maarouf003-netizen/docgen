using System.Net;
using System.Net.Http.Json;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

public class TokenRevocationIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TokenRevocationIntegrationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ChangePassword_InvalidatesPreviouslyIssuedTokens()
    {
        var username = $"rev_{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(username, UserRole.Lawyer, password: "123456");

        var firstLogin = await _factory.LoginAsync(username, "123456");
        Assert.Equal((int)HttpStatusCode.OK, firstLogin!.StatusCode);

        var client = _factory.CreateClient();
        client.SetAuthCookie(firstLogin.Token!);
        var change = await client.PostAsJsonAsync("/api/auth/change-password",
            new { oldPassword = "123456", newPassword = "654321" });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        var revoked = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);

        var newLogin = await _factory.LoginAsync(username, "654321");
        Assert.Equal(HttpStatusCode.OK, (HttpStatusCode)newLogin!.StatusCode);

        var freshClient = _factory.CreateClient();
        freshClient.SetAuthCookie(newLogin.Token!);
        var ok = await freshClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task DeactivatedAccount_TokensBecomeInvalid()
    {
        var username = $"deact_{Guid.NewGuid():N}"[..16];
        await _factory.CreateUserAsync(username, UserRole.Lawyer, password: "123456");

        var login = await _factory.LoginAsync(username, "123456");
        Assert.Equal((int)HttpStatusCode.OK, login!.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var user = db.Users.Single(u => u.Username == username);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.SetAuthCookie(login.Token!);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
