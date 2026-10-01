using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبارات إثبات RF-003 لشبهات التفويض (SEC-016 + T-4 + T-5 من خطة 02-security §15).
/// النتيجة المثبتة مسبقًا بالقراءة: الملكية مفروضة خدميًا لكن الرفض عبر 400 لا 403/404 —
/// هذه الاختبارات تثبت السلوك الحالي وتغلق NOT VERIFIED (أو تولّد وحدة إصلاح دقيقة).
/// (SEC-017 مغطى أصلًا في EntityRegistryHeadScopeIntegrationTests — لا تكرار هنا.)
/// </summary>
[Collection(ApiTestCollection.Name)]
public class RF003AuthzProofTests
{
    private readonly ApiFactory _factory;

    public RF003AuthzProofTests(ApiFactory factory) => _factory = factory;

    private static string NewName(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 16, 40)];

    private Task<int> BranchIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return Task.FromResult(db.Branches.Single(b => b.Code == code).Id);
    }

    private async Task<(int DocId, int AppealId)> CreateAppealAsync(HttpClient lawyer, string token)
    {
        var docId = await _factory.CreateDocumentAsync(token, borrowerName: $"مستأنف {Guid.NewGuid():N}"[..30],
            borrowerFather: "أب", borrowerFamily: "العائلة", withEstate: false, registered: true);
        var created = await lawyer.PostAsJsonAsync($"/api/documents/{docId}/appeals", new
        {
            direction = "against-us",
            appellants = new[] { new { kind = "borrower", partyId = docId } },
            appealedDecisionText = "قرار رئيس التنفيذ المطلوب استئنافه",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var appeal = await created.Content.ReadFromJsonAsync<AppealDto>();
        return (docId, appeal!.Id);
    }

    private async Task<(int DocId, int DelegationId)> CreateDelegationAsync(HttpClient lawyer, string token)
    {
        var docId = await _factory.CreateDocumentAsync(token, borrowerName: $"منيب {Guid.NewGuid():N}"[..30],
            borrowerFather: "أب", borrowerFamily: "العائلة", withEstate: true, registered: true);
        int assetId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            assetId = db.Assets.Single(a => a.DocumentId == docId).Id;
        }
        var created = await lawyer.PostAsJsonAsync($"/api/documents/{docId}/delegations", new
        {
            delegatedCourt = "دائرة تنفيذ حلب",
            isExternal = false,
            externalBranchId = (int?)null,
            delegationDate = "1/8/2026",
            delegationText = "الإنابة على العقار المذكور",
            depositBookNumber = "كتاب-1",
            depositBookDate = "2/8/2026",
            assetIds = new[] { assetId },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var delegation = await created.Content.ReadFromJsonAsync<DelegationDto>();
        return (docId, delegation!.Id);
    }

    [Fact]
    public async Task T1a_CrossOwner_AppealUpdate_Rejected_AndUnchanged()
    {
        var token1 = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var lawyer1 = _factory.WithToken(token1);
        var (_, appealId) = await CreateAppealAsync(lawyer1, token1);

        var stranger = await _factory.CreateUserAsync(NewName("lawyer_t1a"), UserRole.Lawyer, await BranchIdAsync("DAM"));
        var lawyer2 = _factory.AuthorizedClient(stranger.Username);

        var update = await lawyer2.PutAsJsonAsync($"/api/appeals/{appealId}", new
        {
            direction = "against-us",
            appellants = new[] { new { kind = "borrower", partyId = 1 } },
            appealedDecisionText = "نص دخيل",
        });
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);

        var original = await lawyer1.GetAsync($"/api/appeals/{appealId}");
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        using var body = await original.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("قرار رئيس التنفيذ المطلوب استئنافه",
            body!.RootElement.GetProperty("appealedDecisionText").GetString());
    }

    [Fact]
    public async Task T1b_CrossOwner_AppealDelete_Rejected_AndExists()
    {
        var token1 = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var lawyer1 = _factory.WithToken(token1);
        var (_, appealId) = await CreateAppealAsync(lawyer1, token1);

        var stranger = await _factory.CreateUserAsync(NewName("lawyer_t1b"), UserRole.Lawyer, await BranchIdAsync("DAM"));
        var lawyer2 = _factory.AuthorizedClient(stranger.Username);

        var delete = await lawyer2.DeleteAsync($"/api/appeals/{appealId}");
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await lawyer1.GetAsync($"/api/appeals/{appealId}")).StatusCode);
    }

    [Fact]
    public async Task T1c_CrossOwner_DelegationMutations_Rejected_AndUnchanged()
    {
        var token1 = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var lawyer1 = _factory.WithToken(token1);
        var (docId, delegationId) = await CreateDelegationAsync(lawyer1, token1);

        var stranger = await _factory.CreateUserAsync(NewName("lawyer_t1c"), UserRole.Lawyer, await BranchIdAsync("DAM"));
        var lawyer2 = _factory.AuthorizedClient(stranger.Username);

        int assetId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            assetId = db.Assets.Single(a => a.DocumentId == docId).Id;
        }
        var update = await lawyer2.PutAsJsonAsync($"/api/delegations/{delegationId}", new
        {
            delegatedCourt = "دائرة تنفيذ دخيلة",
            isExternal = false,
            externalBranchId = (int?)null,
            delegationDate = "1/8/2026",
            delegationText = "نص دخيل",
            depositBookNumber = "كتاب-2",
            depositBookDate = "2/8/2026",
            assetIds = new[] { assetId },
        });
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);

        var delete = await lawyer2.DeleteAsync($"/api/delegations/{delegationId}");
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);

        var mine = await lawyer1.GetAsync($"/api/documents/{docId}/delegations");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
    }

    [Fact]
    public async Task T4_LoginWithoutBranch_ReturnsBranchChoices_CurrentBehavior()
    {
        // السلوك الحالي: تعداد أسماء الفروع قبل كلمة المرور (SEC-006) — يُثبَّت هنا، وتقييمه لاحقًا.
        var username = NewName("brsel");
        await _factory.CreateUserAsync(username, UserRole.Lawyer, await BranchIdAsync("DAM"));
        await _factory.CreateUserAsync(username, UserRole.Lawyer, await BranchIdAsync("ALP"));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password = "123456" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.True(body!.RootElement.GetProperty("requiresBranchSelection").GetBoolean());
        Assert.Equal(2, body.RootElement.GetProperty("branches").GetArrayLength());
    }

    [Fact]
    public async Task T5_StaleToken_AfterBranchMove_Rejected()
    {
        // نقل المحامي يبطل توكنه القديم عبر TokenVersion — النافذة مغلقة خدميًا.
        var admin = _factory.AuthorizedClient("admin");
        var created = await admin.PostAsJsonAsync("/api/users/lawyers", new
        {
            username = NewName("l"),
            fullName = "محامي النافذة",
            password = "123456",
            branchId = await BranchIdAsync("DAM"),
        });
        created.EnsureSuccessStatusCode();
        var lawyer = (await created.Content.ReadFromJsonAsync<LawyerListItemDto>())!;

        var staleToken = (await _factory.LoginAsync(lawyer.Username, "123456"))!.Token!;
        var staleClient = _factory.WithToken(staleToken);
        Assert.Equal(HttpStatusCode.OK, (await staleClient.GetAsync("/api/documents?perPage=1")).StatusCode);

        var move = await admin.PutAsJsonAsync($"/api/users/{lawyer.Id}", new
        {
            fullName = (string?)null,
            role = (string?)null,
            branchId = await BranchIdAsync("ALP"),
            isActive = true,
            password = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, move.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await staleClient.GetAsync("/api/documents?perPage=1")).StatusCode);
    }
}
