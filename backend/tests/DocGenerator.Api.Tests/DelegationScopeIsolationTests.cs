using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// عزل نطاق الإنابات عبر الـ API (المرحلة 6 — قرار §2.1 + §7):
/// الاعتماد لمالك الدائرة المنابة، والتوجيه للشعبة والتراجع، والرفض للتصحيح.
/// قاعدة معزولة لكل صنف (مصنع خاص) — بلا تلوث متبادل.
/// </summary>
public sealed class DelegationScopeIsolationTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static string Unique(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..24];

    private async Task<int> BranchIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var branch = await db.Branches.SingleAsync(b => b.Code == "DAM");
        if (string.IsNullOrWhiteSpace(branch.Governorate))
        {
            branch.Governorate = "دمشق";
            await db.SaveChangesAsync();
        }
        return branch.Id;
    }

    private async Task<int> CreateSectionAsync(string name)
    {
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.PostAsJsonAsync("/api/sections", new
        {
            branchId = await BranchIdAsync(),
            name,
        });
        response.EnsureSuccessStatusCode();
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("id").GetInt32();
    }

    private async Task<string> CreateSubHeadTokenAsync(int sectionId)
    {
        var admin = _factory.AuthorizedClient("admin");
        var username = Unique("sub");
        var created = await admin.PostAsJsonAsync("/api/users", new
        {
            username,
            fullName = username,
            role = "subhead",
            branchId = await BranchIdAsync(),
            password = "123456",
            sectionId,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await _factory.LoginAsync(username, "123456"))!.Token!;
    }

    private async Task<int> CreateCircuitAsync(string name)
    {
        var head = _factory.AuthorizedClient("head1");
        var response = await head.PostAsJsonAsync("/api/execution-circuits", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("id").GetInt32();
    }

    private async Task TransferCircuitAsync(int circuitId, int? sectionId)
    {
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.PostAsJsonAsync($"/api/execution-circuits/{circuitId}/transfer", new
        {
            targetSectionId = sectionId,
        });
        response.EnsureSuccessStatusCode();
    }

    private async Task<(int Id, string Token)> NewLawyerAsync()
    {
        var admin = _factory.AuthorizedClient("admin");
        var username = Unique("law");
        var created = await admin.PostAsJsonAsync("/api/users/lawyers", new
        {
            username,
            fullName = username,
            password = "123456",
            branchId = await BranchIdAsync(),
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var doc = await created.Content.ReadFromJsonAsync<JsonDocument>();
        var token = (await _factory.LoginAsync(username, "123456"))!.Token!;
        return (doc!.RootElement.GetProperty("id").GetInt32(), token);
    }

    private async Task<int> CreateFileWithAssetAsync(string token, int circuitId, string borrower)
    {
        var client = _factory.ClientWithToken(token);
        var response = await client.PostAsJsonAsync("/api/documents", new
        {
            documentType = "بيان دعوى",
            borrowerName = borrower,
            borrowerFather = "أب",
            borrowerFamily = "العائلة",
            applicant = "المدعي",
            contractType = "تعهد",
            amountNumeric = 100,
            executionCircuitId = circuitId,
            fileNumber = $"9{Guid.NewGuid():N}"[..7],
            fileYear = "2026",
            fileRegistrationDate = "1/8/2026",
            assets = new[]
            {
                new
                {
                    assetKind = "عقار",
                    property = "بيت",
                    propertyNumber = "12345",
                    propertyDistrict = "المزة",
                    landRegistry = "الصالحية",
                    shareType = "تمام العقار",
                    owners = new[] { "المدعى عليه" },
                    seizureDate = "1/8/2026",
                },
            },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("id").GetInt32();
    }

    private async Task<int> AssetIdAsync(int docId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return await db.Assets.Where(a => a.DocumentId == docId).Select(a => a.Id).SingleAsync();
    }

    private async Task<int> CreateDelegationAsync(string token, int docId, int circuitId, int assetId)
    {
        var client = _factory.ClientWithToken(token);
        var response = await client.PostAsJsonAsync($"/api/documents/{docId}/delegations", new
        {
            delegatedCourt = (string?)null,
            isExternal = false,
            externalBranchId = (int?)null,
            delegationDate = "1/8/2026",
            delegationText = "الإنابة على العقار المذكور",
            depositBookNumber = "كتاب-1",
            depositBookDate = "2/8/2026",
            assetIds = new[] { assetId },
            delegatedCircuitId = circuitId,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<List<int>> PendingIdsAsync(HttpClient client, bool rejectedOnly = false)
    {
        var url = rejectedOnly ? "/api/delegations/pending?rejectedOnly=true" : "/api/delegations/pending";
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("id").GetInt32()).ToList();
    }

    [Fact]
    public async Task PendingAssign_SectionCircuit_SubHeadOnly()
    {
        var sectionId = await CreateSectionAsync(Unique("شعبة"));
        var circuitId = await CreateCircuitAsync(Unique("دائرة"));
        await TransferCircuitAsync(circuitId, sectionId);
        var subToken = await CreateSubHeadTokenAsync(sectionId);
        var subClient = _factory.ClientWithToken(subToken);
        var (_, lawyerToken) = await NewLawyerAsync();
        var (targetId, _) = await NewLawyerAsync();
        var fileId = await CreateFileWithAssetAsync(lawyerToken, circuitId, Unique("مقترض"));
        var delegationId = await CreateDelegationAsync(lawyerToken, fileId, circuitId, await AssetIdAsync(fileId));
        var head = _factory.AuthorizedClient("head1");

        // العزل: الشعبة تراها والقسم لا.
        Assert.Contains(delegationId, await PendingIdsAsync(subClient));
        Assert.DoesNotContain(delegationId, await PendingIdsAsync(head));

        // رئيس القسم مرفوض، ورئيس الشعبة يعتمد — والمناب بدائرة الشعبة.
        var headAssign = await head.PostAsJsonAsync($"/api/delegations/{delegationId}/assign",
            new { assignedLawyerId = targetId });
        Assert.Equal(HttpStatusCode.BadRequest, headAssign.StatusCode);
        var subAssign = await subClient.PostAsJsonAsync($"/api/delegations/{delegationId}/assign",
            new { assignedLawyerId = targetId });
        Assert.Equal(HttpStatusCode.OK, subAssign.StatusCode);
        using var assigned = await subAssign.Content.ReadFromJsonAsync<JsonDocument>();
        var targetDocId = assigned!.RootElement.GetProperty("targetDocumentId").GetInt32();
        Assert.Equal(circuitId, assigned.RootElement.GetProperty("delegatedCircuitId").GetInt32());

        // المناب مرئي لرئيس الشعبة عبر ملفه.
        Assert.Equal(HttpStatusCode.OK, (await subClient.GetAsync($"/api/documents/{targetDocId}")).StatusCode);
    }

    [Fact]
    public async Task Reject_WrongCircuit_LawyerSeesReasonAndCorrects()
    {
        var divisionCircuit = await CreateCircuitAsync(Unique("قسم"));
        var (_, lawyerToken) = await NewLawyerAsync();
        var fileId = await CreateFileWithAssetAsync(lawyerToken, divisionCircuit, Unique("مقترض"));
        var delegationId = await CreateDelegationAsync(lawyerToken, fileId, divisionCircuit, await AssetIdAsync(fileId));
        var head = _factory.AuthorizedClient("head1");
        var lawyerClient = _factory.ClientWithToken(lawyerToken);

        // الرفض بلا سبب مرفوض، وبرسالة يُظهرها فلتر المرفوض وعدّاده وبطاقة الملف.
        var emptyReject = await head.PostAsJsonAsync($"/api/delegations/{delegationId}/reject", new { reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, emptyReject.StatusCode);
        var reject = await head.PostAsJsonAsync($"/api/delegations/{delegationId}/reject",
            new { reason = "الدائرة غير مختصة مكانيًا" });
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);

        Assert.DoesNotContain(delegationId, await PendingIdsAsync(head));
        Assert.Contains(delegationId, await PendingIdsAsync(head, rejectedOnly: true));
        var countResponse = await head.GetAsync("/api/delegations/pending-count");
        using var countDoc = await countResponse.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(1, countDoc!.RootElement.GetProperty("rejectedCount").GetInt32());

        var card = await lawyerClient.GetAsync($"/api/documents/{fileId}/delegations");
        Assert.Equal(HttpStatusCode.OK, card.StatusCode);
        using var cardDoc = await card.Content.ReadFromJsonAsync<JsonDocument>();
        var item = Assert.Single(cardDoc!.RootElement.EnumerateArray());
        Assert.Equal("الدائرة غير مختصة مكانيًا", item.GetProperty("rejectReason").GetString());

        // الاعتماد محظور حتى التصحيح، وبعده تعود للمعلّق.
        var blocked = await head.PostAsJsonAsync($"/api/delegations/{delegationId}/assign",
            new { assignedLawyerId = (await NewLawyerAsync()).Id });
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
    }
}
