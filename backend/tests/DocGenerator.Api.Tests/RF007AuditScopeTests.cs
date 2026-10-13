using System.Net;
using System.Net.Http.Json;
using static DocGenerator.Api.Tests.TestHelpers.ApiFixtureExtensions;
using System.Text.Json;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبارات إظهار RF-007 (SEC-013 HIGH): رئيس القسم يرى تدقيق فرعه فقط.
/// تفشل قبل الإصلاح (يرى فرعًا آخر) وتخضر بعده. الترشيح عبر userName دقيق
/// (أسماء فريدة لكل تشغيل — بلا تصادم مع القاعدة المشتركة).
/// </summary>
[Collection(ApiTestCollection.Name)]
public class RF007AuditScopeTests
{
    private readonly ApiFactory _factory;

    public RF007AuditScopeTests(ApiFactory factory) => _factory = factory;


    private async Task<string> LawyerWithDocumentAsync(string branchCode)
    {
        var user = await _factory.CreateUserAsync(UniqueName("lawyer_scope"), UserRole.Lawyer, await _factory.GetBranchIdAsync(branchCode));
        var token = (await _factory.LoginAsync(user.Username, "123456"))!.Token!;
        await _factory.CreateDocumentAsync(token, borrowerName: $"نطاق {Guid.NewGuid():N}"[..30]);
        return user.Username;
    }

    private static async Task<int> TotalForAsync(HttpClient client, string userName)
    {
        using var response = await client.GetAsync($"/api/audit-logs?userName={userName}&perPage=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return body!.RootElement.GetProperty("totalCount").GetInt32();
    }

    [Fact]
    public async Task HeadSeesOwnBranchAudit_NotOtherBranches()
    {
        var damLawyer = await LawyerWithDocumentAsync("DAM");
        var alpLawyer = await LawyerWithDocumentAsync("ALP");

        var head = _factory.AuthorizedClient("head1");
        Assert.True(await TotalForAsync(head, damLawyer) > 0);
        Assert.Equal(0, await TotalForAsync(head, alpLawyer));
    }

    [Fact]
    public async Task ManagerStillSeesAllBranches()
    {
        var damLawyer = await LawyerWithDocumentAsync("DAM");
        var alpLawyer = await LawyerWithDocumentAsync("ALP");

        var manager = _factory.AuthorizedClient("manager");
        Assert.True(await TotalForAsync(manager, damLawyer) > 0);
        Assert.True(await TotalForAsync(manager, alpLawyer) > 0);
    }
}
