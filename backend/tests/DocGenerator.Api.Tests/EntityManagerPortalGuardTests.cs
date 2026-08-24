using System.Security.Claims;
using DocGenerator.Api.Middleware;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace DocGenerator.Api.Tests;

/// <summary>
/// العزل البنيوي لدور مندوب الجهة: يُمنع من كل مسارات API عدا بوابته القرائية
/// و«من أنا/تسجيل الخروج» — بغضّ النظر عن أي متحكم قائم أو لاحق.
/// </summary>
public class EntityManagerPortalGuardTests
{
    private static HttpContext BuildContext(string path, bool authenticated, string? role = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (authenticated)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "9") };
            if (role is not null) claims.Add(new Claim(ClaimTypes.Role, role));
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
        }
        return context;
    }

    private static async Task<int> InvokeAsync(HttpContext context)
    {
        var guard = new EntityManagerPortalGuard(_ =>
        {
            if (context.Response.StatusCode == 0) context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });
        await guard.InvokeAsync(context);
        return context.Response.StatusCode == 0 ? StatusCodes.Status200OK : context.Response.StatusCode;
    }

    [Fact]
    public async Task EntityManager_OnLegacyApi_IsForbidden()
    {
        var context = BuildContext("/api/documents", authenticated: true, role: nameof(UserRole.EntityManager).ToLowerInvariant());

        var status = await InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, status);
    }

    [Theory]
    [InlineData("/api/portal/files")]
    [InlineData("/api/portal/my-scope")]
    [InlineData("/api/auth/me")]
    public async Task EntityManager_OnPortalAndAuthPaths_PassesThrough(string path)
    {
        var context = BuildContext(path, authenticated: true, role: "entitymanager");

        var status = await InvokeAsync(context);

        Assert.NotEqual(StatusCodes.Status403Forbidden, status);
    }

    [Fact]
    public async Task OtherRoles_AreNeverBlocked()
    {
        foreach (var role in new[] { UserRole.Lawyer, UserRole.Head, UserRole.Manager, UserRole.Admin })
        {
            var context = BuildContext("/api/documents", authenticated: true, role.ToString().ToLowerInvariant());

            var status = await InvokeAsync(context);

            Assert.NotEqual(StatusCodes.Status403Forbidden, status);
        }
    }

    [Fact]
    public async Task Anonymous_Request_PassesThrough_ToAuthentication()
    {
        var context = BuildContext("/api/documents", authenticated: false);

        var status = await InvokeAsync(context);

        Assert.NotEqual(StatusCodes.Status403Forbidden, status);
    }
}
