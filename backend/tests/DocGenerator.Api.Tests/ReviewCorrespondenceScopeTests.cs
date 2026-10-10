using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// نطاق المطالعات والمراسلات عبر الـ API (المرحلة 7 — §10):
/// توجيه المستلم، والرؤية والرد والعدّاد بالنطاق.
/// قاعدة معزولة لكل صنف (مصنع خاص) — بلا تلوث متبادل.
/// </summary>
public sealed class ReviewCorrespondenceScopeTests : IAsyncLifetime
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

    private async Task<(string Token, int Id)> CreateSubHeadAsync(int sectionId)
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
        using var createdDoc = await created.Content.ReadFromJsonAsync<JsonDocument>();
        var token = (await _factory.LoginAsync(username, "123456"))!.Token!;
        return (token, createdDoc!.RootElement.GetProperty("id").GetInt32());
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

    private async Task<int> CreateFileAsync(string token, int circuitId)
    {
        var client = _factory.ClientWithToken(token);
        var response = await client.PostAsJsonAsync("/api/documents", new
        {
            documentType = "بيان دعوى",
            borrowerName = Unique("مقترض"),
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

    private static async Task<int> PendingLetterCountAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/review-letters/pending-count");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("count").GetInt32();
    }

    [Fact]
    public async Task LetterFlow_OwnedBySection_SubReplies_HeadDenied()
    {
        var sectionId = await CreateSectionAsync(Unique("شعبة"));
        var circuitId = await CreateCircuitAsync(Unique("دائرة"));
        await TransferCircuitAsync(circuitId, sectionId);
        var (subToken, _) = await CreateSubHeadAsync(sectionId);
        var subClient = _factory.ClientWithToken(subToken);
        var (_, lawyerToken) = await NewLawyerAsync();
        var fileId = await CreateFileAsync(lawyerToken, circuitId);
        var head = _factory.AuthorizedClient("head1");

        // التسطير على ملف الشعبة — المستلم تلقائي (بلا منسدل).
        var lawyerClient = _factory.ClientWithToken(lawyerToken);
        var created = await lawyerClient.PostAsJsonAsync("/api/review-letters", new
        {
            documentId = fileId,
            bodyHtml = "<p>نطلب التوجيه</p>",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdDoc = await created.Content.ReadFromJsonAsync<JsonDocument>();
        var letterId = createdDoc!.RootElement.GetProperty("id").GetInt32();
        Assert.Equal(sectionId, createdDoc.RootElement.GetProperty("recipientSectionId").GetInt32());

        // الجرس بالنطاق: الشعبة ترى والقسم لا.
        Assert.Equal(1, await PendingLetterCountAsync(subClient));
        Assert.Equal(0, await PendingLetterCountAsync(head));

        // الرد للمالك: الشعبة تنجح والقسم يُمنع.
        var subReply = await subClient.PostAsJsonAsync($"/api/review-letters/{letterId}/replies",
            new { bodyHtml = "<p>يوجَّه بالتالي</p>" });
        Assert.Equal(HttpStatusCode.OK, subReply.StatusCode);
        var headReply = await head.PostAsJsonAsync($"/api/review-letters/{letterId}/replies",
            new { bodyHtml = "<p>رد</p>" });
        Assert.Equal(HttpStatusCode.Forbidden, headReply.StatusCode);
    }

    [Fact]
    public async Task LetterFlow_GeneralWithSection_SubSees_HeadDoesNot()
    {
        var sectionId = await CreateSectionAsync(Unique("شعبة"));
        var (subToken, _) = await CreateSubHeadAsync(sectionId);
        var subClient = _factory.ClientWithToken(subToken);
        var (_, lawyerToken) = await NewLawyerAsync();
        var lawyerClient = _factory.ClientWithToken(lawyerToken);
        var head = _factory.AuthorizedClient("head1");

        // بلا ملف: اختيار الشعبة من المنسدل.
        var created = await lawyerClient.PostAsJsonAsync("/api/review-letters", new
        {
            documentId = (int?)null,
            bodyHtml = "<p>كتاب عام للشعبة</p>",
            recipientSectionId = sectionId,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdDoc = await created.Content.ReadFromJsonAsync<JsonDocument>();
        var letterId = createdDoc!.RootElement.GetProperty("id").GetInt32();

        Assert.Equal(1, await PendingLetterCountAsync(subClient));
        Assert.Equal(0, await PendingLetterCountAsync(head));
        Assert.Equal(HttpStatusCode.OK, (await subClient.GetAsync($"/api/review-letters/{letterId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await head.GetAsync($"/api/review-letters/{letterId}")).StatusCode);
    }

    [Fact]
    public async Task CorrespondenceFlow_GeneralToSectionHead_VisibleToSub()
    {
        var sectionId = await CreateSectionAsync(Unique("شعبة"));
        var (subToken, subId) = await CreateSubHeadAsync(sectionId);
        var subClient = _factory.ClientWithToken(subToken);
        var (_, lawyerToken) = await NewLawyerAsync();
        var lawyerClient = _factory.ClientWithToken(lawyerToken);

        // مراسلة عامة لرئيس الشعبة مع تجميد الشعبة.
        var created = await lawyerClient.PostAsJsonAsync("/api/correspondence", new
        {
            documentId = (int?)null,
            targetUserId = subId,
            importance = "normal",
            bodyHtml = "<p>إلى الشعبة</p>",
            recipientSectionId = sectionId,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdDoc = await created.Content.ReadFromJsonAsync<JsonDocument>();
        var letterId = createdDoc!.RootElement.GetProperty("id").GetInt32();

        // المستلم (رئيس الشعبة) يراها في بحثه وتفاصيلها.
        var search = await subClient.GetAsync("/api/correspondence?perPage=100");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        using var searchDoc = await search.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Contains(searchDoc!.RootElement.GetProperty("items").EnumerateArray(),
            e => e.GetProperty("id").GetInt32() == letterId);
        Assert.Equal(HttpStatusCode.OK, (await subClient.GetAsync($"/api/correspondence/{letterId}")).StatusCode);
    }
}
