using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// تدفق الإحالة عبر الـ API (المرحلة 5ب — قرار §2.22):
/// إحالة شعبة → قسم (رؤية القسم للاستئناف وملفه) → تراجع (استرجاع المحيل / إعادة المستلم).
/// قاعدة معزولة لكل صنف (مصنع خاص) — بلا تلوث متبادل مع تدقيق `head1` المشترك.
/// </summary>
public sealed class AppealForwardFlowTests : IAsyncLifetime
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
        return (await db.Branches.SingleAsync(b => b.Code == "DAM")).Id;
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

    private async Task<int> CreateFileAsync(string token, int circuitId, string borrower)
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
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("id").GetInt32();
    }

    private async Task<int> CreateAppealAsync(string token, int docId)
    {
        var client = _factory.ClientWithToken(token);
        var response = await client.PostAsJsonAsync($"/api/documents/{docId}/appeals", new
        {
            direction = "against-us",
            appellants = new[] { new { kind = "borrower", partyId = docId } },
            appealedDecisionText = "قرار رئيس التنفيذ المطلوب استئنافه",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<List<int>> SearchAppealIdsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/appeals?perPage=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("id").GetInt32()).ToList();
    }

    private static async Task<string> ForwardStateAsync(HttpClient client, int appealId)
    {
        var response = await client.GetAsync($"/api/appeals/{appealId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("forwardState").GetString() ?? string.Empty;
    }

    [Fact]
    public async Task ForwardFlow_HeadGainsVisibility_ThenRecallRevokesIt()
    {
        var sectionId = await CreateSectionAsync(Unique("شعبة"));
        var circuitId = await CreateCircuitAsync(Unique("دائرة"));
        await TransferCircuitAsync(circuitId, sectionId);
        var subToken = await CreateSubHeadTokenAsync(sectionId);
        var subClient = _factory.ClientWithToken(subToken);
        var (_, lawyerToken) = await NewLawyerAsync();
        var fileId = await CreateFileAsync(lawyerToken, circuitId, Unique("مقترض"));
        var appealId = await CreateAppealAsync(lawyerToken, fileId);
        var head = _factory.AuthorizedClient("head1");

        // قبل الإحالة: القسم لا يرى استئناف الشعبة ولا ملفه ولا تفاصيله.
        Assert.DoesNotContain(appealId, await SearchAppealIdsAsync(head));
        Assert.Equal(HttpStatusCode.Forbidden, (await head.GetAsync($"/api/appeals/{appealId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await head.GetAsync($"/api/documents/{fileId}")).StatusCode);

        // الإحالة: شعبة → قسم.
        var forward = await subClient.PostAsJsonAsync($"/api/appeals/{appealId}/forward", new
        {
            reason = "يرجى التفضل بالإسناد",
        });
        Assert.Equal(HttpStatusCode.OK, forward.StatusCode);
        Assert.Equal("ForwardedToHead", await ForwardStateAsync(subClient, appealId));

        // بعد الإحالة: القسم يرى الاستئناف (بحث + تفاصيل) ويفتح الملف (استثناء 22′).
        Assert.Contains(appealId, await SearchAppealIdsAsync(head));
        Assert.Equal(HttpStatusCode.OK, (await head.GetAsync($"/api/appeals/{appealId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await head.GetAsync($"/api/documents/{fileId}")).StatusCode);

        // التراجع (استرجاع المحيل قبل الإسناد) يعيد العزل.
        var recall = await subClient.PostAsync($"/api/appeals/{appealId}/recall-forward", null);
        Assert.Equal(HttpStatusCode.OK, recall.StatusCode);
        Assert.Equal("Owned", await ForwardStateAsync(subClient, appealId));
        Assert.DoesNotContain(appealId, await SearchAppealIdsAsync(head));
        Assert.Equal(HttpStatusCode.Forbidden, (await head.GetAsync($"/api/appeals/{appealId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await head.GetAsync($"/api/documents/{fileId}")).StatusCode);
    }

    [Fact]
    public async Task ForwardFlow_HeadReturnForward_RestoresOwned()
    {
        var sectionId = await CreateSectionAsync(Unique("شعبة"));
        var circuitId = await CreateCircuitAsync(Unique("دائرة"));
        await TransferCircuitAsync(circuitId, sectionId);
        var subToken = await CreateSubHeadTokenAsync(sectionId);
        var subClient = _factory.ClientWithToken(subToken);
        var (_, lawyerToken) = await NewLawyerAsync();
        var fileId = await CreateFileAsync(lawyerToken, circuitId, Unique("مقترض"));
        var appealId = await CreateAppealAsync(lawyerToken, fileId);
        var head = _factory.AuthorizedClient("head1");

        var forward = await subClient.PostAsJsonAsync($"/api/appeals/{appealId}/forward", new
        {
            reason = "يرجى التفضل بالإسناد",
        });
        Assert.Equal(HttpStatusCode.OK, forward.StatusCode);
        Assert.Contains(appealId, await SearchAppealIdsAsync(head));

        // إعادة المستلم (رئيس القسم) الإحالة لمحيلها قبل الإسناد.
        var returned = await head.PostAsync($"/api/appeals/{appealId}/return-forward", null);
        Assert.Equal(HttpStatusCode.OK, returned.StatusCode);
        Assert.Equal("Owned", await ForwardStateAsync(subClient, appealId));
        Assert.DoesNotContain(appealId, await SearchAppealIdsAsync(head));
        Assert.Equal(HttpStatusCode.Forbidden, (await head.GetAsync($"/api/documents/{fileId}")).StatusCode);
    }

    [Fact]
    public async Task ForwardFlow_DecidedForwarded_HeadLosesException()
    {
        // الاستثناء القرائي حتى الحسم فقط (22′): بعد الحسم يعود العزل الدائري.
        var sectionId = await CreateSectionAsync(Unique("شعبة"));
        var circuitId = await CreateCircuitAsync(Unique("دائرة"));
        await TransferCircuitAsync(circuitId, sectionId);
        var subToken = await CreateSubHeadTokenAsync(sectionId);
        var subClient = _factory.ClientWithToken(subToken);
        var (_, lawyerToken) = await NewLawyerAsync();
        var (targetId, targetToken) = await NewLawyerAsync();
        var fileId = await CreateFileAsync(lawyerToken, circuitId, Unique("مقترض"));
        var appealId = await CreateAppealAsync(lawyerToken, fileId);
        var head = _factory.AuthorizedClient("head1");

        Assert.Equal(HttpStatusCode.OK,
            (await subClient.PostAsJsonAsync($"/api/appeals/{appealId}/forward", new { reason = "يرجى التفضل بالإسناد" })).StatusCode);
        Assert.Contains(appealId, await SearchAppealIdsAsync(head));

        // إسناد الاستثناء (القسم يسند رغم الدائرة) ثم حسم المتابع.
        var targetClient = _factory.ClientWithToken(targetToken);
        Assert.Equal(HttpStatusCode.OK,
            (await head.PostAsJsonAsync($"/api/appeals/{appealId}/assign", new { assignedLawyerId = targetId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await targetClient.PostAsJsonAsync($"/api/appeals/{appealId}/decide", new
            {
                decisionNumber = "5",
                decisionDate = "20/8/2026",
                decisionRuling = "المنطوق",
                outcome = "in-favor",
            })).StatusCode);

        Assert.DoesNotContain(appealId, await SearchAppealIdsAsync(head));
        Assert.Equal(HttpStatusCode.Forbidden, (await head.GetAsync($"/api/appeals/{appealId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await head.GetAsync($"/api/documents/{fileId}")).StatusCode);
    }
}
