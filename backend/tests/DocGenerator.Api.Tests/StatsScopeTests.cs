using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// نطاق الإحصاءات والتصدير والتنبيهات عبر الـ API (المرحلة 8 — قرارات 20/27/29):
/// القسم لقسمه، والشعبة لشعبتها، والإدارة للكل، وقراءة التنبيهات بالمستلم.
/// قاعدة معزولة لكل صنف (مصنع خاص) — بلا تلوث متبادل.
/// </summary>
public sealed class StatsScopeTests : IAsyncLifetime
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
            applicant = "المدعي",
            contractType = "تعهد",
            amountNumeric = 100,
            executionCircuitId = circuitId,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<int> ManagerTotalAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/stats/manager");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("totalFiles").GetInt32();
    }

    private static List<string> XlsxHeaders(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var doc = SpreadsheetDocument.Open(stream, false);
        var sheetData = doc.WorkbookPart!.WorksheetParts.First().Worksheet.GetFirstChild<SheetData>()!;
        return sheetData.Elements<Row>().First().Elements<Cell>()
            .Select(c => c.InlineString?.Text?.Text ?? string.Empty).ToList();
    }

    private static List<string> XlsxColumn(byte[] bytes, int index)
    {
        using var stream = new MemoryStream(bytes);
        using var doc = SpreadsheetDocument.Open(stream, false);
        var sheetData = doc.WorkbookPart!.WorksheetParts.First().Worksheet.GetFirstChild<SheetData>()!;
        return sheetData.Elements<Row>().Skip(1)
            .Select(r => r.Elements<Cell>().ElementAt(index).InlineString?.Text?.Text ?? string.Empty).ToList();
    }

    [Fact]
    public async Task CircuitPickers_SubHeadAllowed_HeadScoped()
    {
        var sectionId = await CreateSectionAsync(Unique("شعبة"));
        var sectionCircuit = await CreateCircuitAsync(Unique("شعبة-دائرة"));
        await TransferCircuitAsync(sectionCircuit, sectionId);
        var (subToken, _) = await CreateSubHeadAsync(sectionId);
        var subClient = _factory.ClientWithToken(subToken);

        // كانت 403 قبل فتح السمات للشعبة (§8 مراجعة).
        Assert.Equal(HttpStatusCode.OK, (await subClient.GetAsync("/api/execution-circuits/for-lawyer")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await subClient.GetAsync("/api/execution-circuits/for-delegation")).StatusCode);
    }

    [Fact]
    public async Task ManagerStats_HeadDivision_SubSection_ManagerAll()
    {
        var sectionId = await CreateSectionAsync(Unique("شعبة"));
        var divisionCircuit = await CreateCircuitAsync(Unique("قسم"));
        var sectionCircuit = await CreateCircuitAsync(Unique("شعبة-دائرة"));
        await TransferCircuitAsync(sectionCircuit, sectionId);
        var (subToken, _) = await CreateSubHeadAsync(sectionId);
        var subClient = _factory.ClientWithToken(subToken);
        var (_, lawyerToken) = await NewLawyerAsync();
        await CreateFileAsync(lawyerToken, divisionCircuit);
        await CreateFileAsync(lawyerToken, sectionCircuit);
        var head = _factory.AuthorizedClient("head1");
        var manager = _factory.AuthorizedClient("manager");

        // قرار 27: القسم لقسمه (1)، والشعبة لشعبتها (1)، والإدارة للكل (2).
        Assert.Equal(1, await ManagerTotalAsync(head));
        Assert.Equal(1, await ManagerTotalAsync(subClient));
        Assert.Equal(2, await ManagerTotalAsync(manager));
    }

    [Fact]
    public async Task CircuitStats_CarriesSection_AndExportsIt()
    {
        var sectionId = await CreateSectionAsync(Unique("شعبة"));
        var sectionCircuit = await CreateCircuitAsync(Unique("شعبة-دائرة"));
        await TransferCircuitAsync(sectionCircuit, sectionId);
        var manager = _factory.AuthorizedClient("manager");

        var stats = await manager.GetAsync("/api/execution-circuits/stats");
        Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
        using var statsDoc = await stats.Content.ReadFromJsonAsync<JsonDocument>();
        var row = statsDoc!.RootElement.EnumerateArray()
            .First(e => e.GetProperty("circuitId").GetInt32() == sectionCircuit);
        Assert.Equal(sectionId, row.GetProperty("sectionId").GetInt32());
        Assert.NotNull(row.GetProperty("sectionName").GetString());

        // تصدير الجدول الرباعي بالنطاق نفسه مع عمود الشعبة.
        var export = await manager.GetAsync("/api/execution-circuits/stats/export");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        // اسم الملف بتوقيت الخادم (ServerClock) — يُكتفى هنا بوجود اسم xlsx
        // (الترميز RFC5987 للأسماء العربية)، والمحتوى أدناه هو الثابت الحقيقي.
        var disposition = export.Content.Headers.ContentDisposition;
        Assert.NotNull(disposition);
        Assert.EndsWith(".xlsx", disposition!.FileNameStar ?? disposition.FileName);
        var headers = XlsxHeaders(await export.Content.ReadAsByteArrayAsync());
        Assert.Equal(
            new[] { "الدائرة", "الفرع", "الشعبة", "الملفات", "المحامون النشطون", "المعلقات" },
            headers);
    }

    [Fact]
    public async Task DocumentsExport_HasSectionColumn_WithDivisionDefault()
    {
        var divisionCircuit = await CreateCircuitAsync(Unique("قسم"));
        var (_, lawyerToken) = await NewLawyerAsync();
        await CreateFileAsync(lawyerToken, divisionCircuit);

        var lawyer = _factory.ClientWithToken(lawyerToken);
        var export = await lawyer.GetAsync("/api/documents/export");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        var bytes = await export.Content.ReadAsByteArrayAsync();
        var headers = XlsxHeaders(bytes);
        var sectionIndex = headers.IndexOf("الشعبة");
        Assert.True(sectionIndex >= 0);
        // ملف دائرة القسم: الشعبة = «القسم».
        Assert.Contains("القسم", XlsxColumn(bytes, sectionIndex));
    }

    [Fact]
    public async Task Alerts_HeadAndSub_SeeOnlyOwn()
    {
        var sectionId = await CreateSectionAsync(Unique("شعبة"));
        var (subToken, subId) = await CreateSubHeadAsync(sectionId);
        var subClient = _factory.ClientWithToken(subToken);
        var head = _factory.AuthorizedClient("head1");

        var subAlert = await (await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "head",
            documentId = (int?)null,
            targetLawyerId = (int?)null,
            message = Unique("للشعبة"),
            recipientUserId = subId,
        })).Content.ReadFromJsonAsync<JsonDocument>();
        var headAlert = await (await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "head",
            documentId = (int?)null,
            targetLawyerId = (int?)null,
            message = Unique("للقسم"),
            recipientUserId = await UserIdAsync("head1"),
        })).Content.ReadFromJsonAsync<JsonDocument>();

        async Task<List<int>> AlertIdsAsync(HttpClient client)
        {
            using var doc = await (await client.GetAsync("/api/alerts")).Content.ReadFromJsonAsync<JsonDocument>();
            return doc!.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToList();
        }

        var subIds = await AlertIdsAsync(subClient);
        Assert.Contains(subAlert!.RootElement.GetProperty("id").GetInt32(), subIds);
        Assert.DoesNotContain(headAlert!.RootElement.GetProperty("id").GetInt32(), subIds);

        var headIds = await AlertIdsAsync(head);
        Assert.Contains(headAlert.RootElement.GetProperty("id").GetInt32(), headIds);
        Assert.DoesNotContain(subAlert.RootElement.GetProperty("id").GetInt32(), headIds);

        // تعليم القراءة يعمل للمستلم الرئيس.
        var mark = await subClient.PatchAsJsonAsync(
            $"/api/alerts/{subAlert.RootElement.GetProperty("id").GetInt32()}/read", new { });
        Assert.Equal(HttpStatusCode.NoContent, mark.StatusCode);
    }

    private async Task<int> UserIdAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        await Task.CompletedTask;
        return db.Users.Single(u => u.Username == username).Id;
    }
}
