using System.Net;
using System.Net.Http.Json;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبارات إظهار RF-005 (SEC-006/SEC-007): الأحداث الأمنية الصامتة يجب أن تُدوَّن.
/// تفشل قبل الإصلاح (لا صف) وتخضر بعده. الاستعلام من AuditLogs مباشرة عبر نطاق.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class RF005SecurityLoggingTests
{
    private readonly ApiFactory _factory;

    public RF005SecurityLoggingTests(ApiFactory factory) => _factory = factory;

    private static string NewName(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 16, 40)];

    private Task<int> BranchIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return Task.FromResult(db.Branches.Single(b => b.Code == code).Id);
    }

    private Task<bool> HasAuditRowAsync(string actionType, string userName)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return Task.FromResult(db.AuditLogs.Any(a => a.ActionType == actionType && a.UserName == userName));
    }

    [Fact]
    public async Task GuardRejection_WritesPortalForbiddenRow()
    {
        var username = NewName("em_sec");
        await _factory.CreateUserAsync(username, UserRole.EntityManager);
        var client = _factory.AuthorizedClient(username);

        var response = await client.GetAsync("/api/documents?perPage=1");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        Assert.True(await HasAuditRowAsync("portal_forbidden", username));
    }

    [Fact]
    public async Task LockedRetry_WritesLockedRetryRow()
    {
        var username = NewName("lock_sec");
        await _factory.CreateUserAsync(username, UserRole.Lawyer, await BranchIdAsync("DAM"));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var user = db.Users.Single(u => u.Username == username);
            user.FailedLoginCount = 5;
            user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(15);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password = "wrong-pass" });
        Assert.Equal((HttpStatusCode)423, response.StatusCode);

        Assert.True(await HasAuditRowAsync("login_locked_retry", username));
    }

    [Fact]
    public async Task BranchSelection_WritesBranchChoicesRow()
    {
        var username = NewName("brsel_sec");
        await _factory.CreateUserAsync(username, UserRole.Lawyer, await BranchIdAsync("DAM"));
        await _factory.CreateUserAsync(username, UserRole.Lawyer, await BranchIdAsync("ALP"));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password = "123456" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(await HasAuditRowAsync("login_branch_choices", username));
    }
}
