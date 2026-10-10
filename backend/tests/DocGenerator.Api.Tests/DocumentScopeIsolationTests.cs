using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// عزل نطاق الملفات (المرحلة 4أ/4ب — قرار §2.1 + §5): رئيس القسم لدوائر القسم
/// وبلا دائرة، ورئيس الشعبة لدوائر شعبته، والنقل بتقاطع النطاق.
/// قاعدة معزولة لكل اختبار (مصنع خاص) — بلا تلوث متبادل.
///
public sealed class DocumentScopeIsolationTests : IAsyncLifetime
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

    private async Task SetCircuitSectionAsync(int circuitId, int? sectionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        (await db.ExecutionCircuits.SingleAsync(c => c.Id == circuitId)).SectionId = sectionId;
        await db.SaveChangesAsync();
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

    private async Task<int> CreateFileAsync(string token, int? circuitId, string borrower)
    {
        var client = _factory.CreateClient();
        client.SetAuthCookie(token);
        var response = await client.PostAsJsonAsync("/api/documents", new
        {
            documentType = "بيان دعوى",
            borrowerName = borrower,
            applicant = "المدعي",
            contractType = "تعهد",
            amountNumeric = 100,
            executionCircuitId = circuitId,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("id").GetInt32();
    }

    private async Task<int> CreateNullCircuitFileAsync(int lawyerId, string borrower)
    {
        // ملف بلا دائرة (انتقالي/خارجي/قديم — قرار §2.8): لا يُنشأ عبر الـ API
        // بعد تعبئة السجل (الإلزامية)، فيُزرع مباشرةً كما تتركه المسارات الاستثنائية.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var branchId = await BranchIdAsync();
        var doc = new DocGenerator.Domain.Entities.Document
        {
            BranchId = branchId,
            CreatedById = lawyerId,
            BorrowerName = borrower,
            GeneralEntitySide = "applicant",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();
        return doc.Id;
    }

    private static async Task<List<int>> SearchIdsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/documents?perPage=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("id").GetInt32()).ToList();
    }

    [Fact]
    public async Task Search_HeadSeesDivisionAndNull_SubHeadSeesOwnOnly()
    {
        var divisionCircuit = await CreateCircuitAsync(Unique("قسم"));
        var sectionId = await CreateSectionAsync(Unique("شعبة"));
        var sectionCircuit = await CreateCircuitAsync(Unique("شعبة-دائرة"));
        await SetCircuitSectionAsync(sectionCircuit, sectionId);
        var (lawyerId, token) = await NewLawyerAsync();
        var fDivision = await CreateFileAsync(token, divisionCircuit, Unique("مقترض-قسم"));
        var fSection = await CreateFileAsync(token, sectionCircuit, Unique("مقترض-شعبة"));
        var fNull = await CreateNullCircuitFileAsync(lawyerId, Unique("مقترض-بلا"));

        var headIds = await SearchIdsAsync(_factory.AuthorizedClient("head1"));
        Assert.Contains(fDivision, headIds);
        Assert.Contains(fNull, headIds);
        Assert.DoesNotContain(fSection, headIds);

        var subToken = await CreateSubHeadTokenAsync(sectionId);
        var subClient = _factory.CreateClient();
        subClient.SetAuthCookie(subToken);
        var subIds = await SearchIdsAsync(subClient);
        Assert.Contains(fSection, subIds);
        Assert.DoesNotContain(fDivision, subIds);
        Assert.DoesNotContain(fNull, subIds);
    }

    [Fact]
    public async Task Get_SingleDoc_CrossScope_Forbidden_ForwardException_AllowsHead()
    {
        var sectionId = await CreateSectionAsync(Unique("شعبة"));
        var sectionCircuit = await CreateCircuitAsync(Unique("شعبة-دائرة"));
        await SetCircuitSectionAsync(sectionCircuit, sectionId);
        var (_, token) = await NewLawyerAsync();
        var fileId = await CreateFileAsync(token, sectionCircuit, Unique("مقترض"));

        var subToken = await CreateSubHeadTokenAsync(sectionId);
        var subClient = _factory.CreateClient();
        subClient.SetAuthCookie(subToken);
        Assert.Equal(HttpStatusCode.OK, (await subClient.GetAsync($"/api/documents/{fileId}")).StatusCode);

        var head = _factory.AuthorizedClient("head1");
        Assert.Equal(HttpStatusCode.Forbidden, (await head.GetAsync($"/api/documents/{fileId}")).StatusCode);

        // الاستثناء القرائي (22′): إحالة مفتوحة → القسم يرى الملف حتى الحسم.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var lawyerId = (await db.Users.SingleAsync(u => u.Username.StartsWith("law_"))).Id;
            db.DocumentAppeals.Add(new DocGenerator.Domain.Entities.DocumentAppeal
            {
                DocumentId = fileId,
                Direction = "appellants",
                Status = "pending",
                AppellantsJson = "[]",
                AppelleesJson = "[]",
                ForwardState = "ForwardedToHead",
                CreatedById = lawyerId,
            });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await head.GetAsync($"/api/documents/{fileId}")).StatusCode);
    }

    [Fact]
    public async Task Transfer_Single_OutOfScope_Forbidden()
    {
        var divisionCircuit = await CreateCircuitAsync(Unique("قسم"));
        var (_, token) = await NewLawyerAsync();
        var fileId = await CreateFileAsync(token, divisionCircuit, Unique("مقترض"));
        var sectionId = await CreateSectionAsync(Unique("شعبة"));
        var subToken = await CreateSubHeadTokenAsync(sectionId);
        var subClient = _factory.CreateClient();
        subClient.SetAuthCookie(subToken);
        var target = await NewLawyerAsync();

        var transfer = await subClient.PostAsJsonAsync($"/api/documents/{fileId}/transfer",
            new { targetLawyerId = target.Id });
        Assert.Equal(HttpStatusCode.Forbidden, transfer.StatusCode);
    }

    [Fact]
    public async Task TransferAll_Intersection_PreviewMatchesMoved()
    {
        var divisionCircuit = await CreateCircuitAsync(Unique("قسم"));
        var sectionId = await CreateSectionAsync(Unique("شعبة"));
        var sectionCircuit = await CreateCircuitAsync(Unique("شعبة-دائرة"));
        await SetCircuitSectionAsync(sectionCircuit, sectionId);
        var (sourceId, sourceToken) = await NewLawyerAsync();
        await CreateFileAsync(sourceToken, divisionCircuit, Unique("مقترض-قسم"));
        await CreateFileAsync(sourceToken, sectionCircuit, Unique("مقترض-شعبة"));
        var (targetId, _) = await NewLawyerAsync();
        var subToken = await CreateSubHeadTokenAsync(sectionId);
        var subClient = _factory.CreateClient();
        subClient.SetAuthCookie(subToken);

        // المعاينة بالنطاق (§5.5): ملف الشعبة فقط.
        var preview = await subClient.GetAsync($"/api/documents/owner/{sourceId}/count");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var previewDoc = await preview.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(1, previewDoc!.RootElement.GetProperty("count").GetInt32());

        // النقل ينقل ملف الشعبة فقط.
        var transfer = await subClient.PostAsJsonAsync("/api/documents/transfer-all",
            new { sourceLawyerId = sourceId, targetLawyerId = targetId });
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
        using var transferDoc = await transfer.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(1, transferDoc!.RootElement.GetProperty("transferredCount").GetInt32());

        // ورئيس القسم ينقل الباقي (ملف القسم).
        var head = _factory.AuthorizedClient("head1");
        var second = await head.PostAsJsonAsync("/api/documents/transfer-all",
            new { sourceLawyerId = sourceId, targetLawyerId = targetId });
        using var secondDoc = await second.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(1, secondDoc!.RootElement.GetProperty("transferredCount").GetInt32());
    }

    private async Task<List<string>> LawyerNamesAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/users/lawyers{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("username").GetString() ?? string.Empty).ToList();
    }

    [Fact]
    public async Task Lawyers_MineMode_UnionRule_ViaHttp()
    {
        var divisionCircuit = await CreateCircuitAsync(Unique("قسم"));
        var sectionId = await CreateSectionAsync(Unique("شعبة"));
        var sectionCircuit = await CreateCircuitAsync(Unique("شعبة-دائرة"));
        await SetCircuitSectionAsync(sectionCircuit, sectionId);
        // أ: ملف قسم. ب: ملف شعبة. ج: صفري (لاختبار التقاطع اللاحق).
        var (aId, aToken) = await NewLawyerAsync();
        var (bId, bToken) = await NewLawyerAsync();
        await NewLawyerAsync();
        await CreateFileAsync(aToken, divisionCircuit, Unique("مقترض-أ"));
        await CreateFileAsync(bToken, sectionCircuit, Unique("مقترض-ب"));

        var headNames = await LawyerNamesAsync(_factory.AuthorizedClient("head1"), "?mode=mine");
        var subToken = await CreateSubHeadTokenAsync(sectionId);
        var subClient = _factory.CreateClient();
        subClient.SetAuthCookie(subToken);
        var subNames = await LawyerNamesAsync(subClient, "?mode=mine");

        // العزل: كل رئيس يرى محامي ملفات نطاقه دون الآخر.
        var bUsername = await UsernameAsync(bId);
        var aUsername = await UsernameAsync(aId);
        Assert.DoesNotContain(bUsername, headNames);
        Assert.Contains(aUsername, headNames);
        Assert.Contains(bUsername, subNames);
        Assert.DoesNotContain(aUsername, subNames);
    }

    [Fact]
    public async Task Lawyers_BranchMode_FullList_BadMode_Rejected()
    {
        var head = _factory.AuthorizedClient("head1");
        var branch = await LawyerNamesAsync(head, "?mode=branch");
        var mine = await LawyerNamesAsync(head, "");
        Assert.Equal(branch.Count, mine.Count);

        var bad = await head.GetAsync("/api/users/lawyers?mode=everyone");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    private async Task<string> UsernameAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return (await db.Users.SingleAsync(u => u.Id == userId)).Username;
    }

    private async Task<int> SeedDelegationWithTargetAsync(int sourceDocId, int targetCircuitId, int targetLawyerId, string targetLawyerName)
    {
        // إنابة داخلية معتمدة بملف مناب حي — تُزرع مباشرةً (التدفق الكامل مغطى
        // باختبارات الإنابات؛ هنا البطاقة فقط).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var branchId = await BranchIdAsync();
        var delegation = new DocGenerator.Domain.Entities.DocumentDelegation
        {
            SourceDocumentId = sourceDocId,
            DelegatedCircuitId = targetCircuitId,
            IsExternal = false,
            Status = DocGenerator.Domain.Enums.DelegationStatusCatalog.Assigned,
            AssignedLawyerId = targetLawyerId,
            CreatedById = targetLawyerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.DocumentDelegations.Add(delegation);
        await db.SaveChangesAsync();
        var target = new DocGenerator.Domain.Entities.Document
        {
            BranchId = branchId,
            CreatedById = targetLawyerId,
            ExecutionCircuitId = targetCircuitId,
            SourceDelegationId = delegation.Id,
            BorrowerName = "مناب",
            GeneralEntitySide = "applicant",
            Lawyer = targetLawyerName,
            Court = "دائرة الشعبة",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Documents.Add(target);
        await db.SaveChangesAsync();
        return delegation.Id;
    }

    [Fact]
    public async Task Delegations_SubHeadSeesOwnDocCard_WithLiveTargetFields()
    {
        // البطاقة (§5.7): ملخص المناب الحي داخل عرض المنيب — قراءة بلا ملاحة.
        var divisionCircuit = await CreateCircuitAsync(Unique("قسم"));
        var sectionId = await CreateSectionAsync(Unique("شعبة"));
        var sectionCircuit = await CreateCircuitAsync(Unique("شعبة-دائرة"));
        await SetCircuitSectionAsync(sectionCircuit, sectionId);
        var (_, sourceToken) = await NewLawyerAsync();
        var sourceId = await CreateFileAsync(sourceToken, divisionCircuit, Unique("مقترض-منيب"));
        var (targetLawyerId, _) = await NewLawyerAsync();
        await SeedDelegationWithTargetAsync(sourceId, sectionCircuit, targetLawyerId, "محامي المناب");

        // رئيس القسم يرى تشعبات ملف القسم مع حقول البطاقة الحية.
        var head = _factory.AuthorizedClient("head1");
        var response = await head.GetAsync($"/api/documents/{sourceId}/delegations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var item = Assert.Single(doc!.RootElement.EnumerateArray());
        Assert.Equal("محامي المناب", item.GetProperty("targetLawyerName").GetString());
        Assert.Equal("دائرة الشعبة", item.GetProperty("delegatedCourt").GetString());
        var branchId = await BranchIdAsync();
        Assert.Equal(branchId, item.GetProperty("targetBranchId").GetInt32());
        Assert.NotNull(item.GetProperty("targetBranchName").GetString());
    }

    [Fact]
    public async Task Delegations_CrossSection_Forbidden()
    {
        var sectionId = await CreateSectionAsync(Unique("شعبة-أ"));
        var otherId = await CreateSectionAsync(Unique("شعبة-ب"));
        var sectionCircuit = await CreateCircuitAsync(Unique("شعبة-دائرة"));
        await SetCircuitSectionAsync(sectionCircuit, sectionId);
        var (_, sourceToken) = await NewLawyerAsync();
        var sourceId = await CreateFileAsync(sourceToken, sectionCircuit, Unique("مقترض-منيب"));
        var (targetLawyerId, _) = await NewLawyerAsync();
        await SeedDelegationWithTargetAsync(sourceId, sectionCircuit, targetLawyerId, "محامي المناب");

        var subToken = await CreateSubHeadTokenAsync(otherId);
        var subClient = _factory.CreateClient();
        subClient.SetAuthCookie(subToken);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await subClient.GetAsync($"/api/documents/{sourceId}/delegations")).StatusCode);
    }
}
