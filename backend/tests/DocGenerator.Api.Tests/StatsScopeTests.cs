using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Api.Tests.TestHelpers;
using static DocGenerator.Api.Tests.TestHelpers.ApiFixtureExtensions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// نطاق الإحصاءات والتصدير والتنبيهات عبر الـ API (المرحلة 8 — قرارات 20/27/29):
/// القسم لقسمه، والشعبة لشعبتها، والإدارة للكل، وقراءة التنبيهات بالمستلم.
/// قاعدة معزولة لكل صنف (مصنع خاص) — بلا تلوث متبادل.
/// </summary>
public sealed class StatsScopeTests : IsolatedApiTestBase
{

    private async Task<(string Token, int Id)> CreateSubHeadAsync(int sectionId)
    {
        var admin = Factory.AuthorizedClient("admin");
        var username = UniqueName("sub");
        var created = await admin.PostAsJsonAsync("/api/users", new
        {
            username,
            fullName = username,
            role = "subhead",
            branchId = await Factory.GetDamascusIdAsync(),
            password = "TestPass1234",
            sectionId,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdDoc = await created.Content.ReadFromJsonAsync<JsonDocument>();
        var token = (await Factory.LoginAsync(username, "TestPass1234"))!.Token!;
        return (token, createdDoc!.RootElement.GetProperty("id").GetInt32());
    }

    private async Task<int> CreateCircuitAsync(string name)
    {
        var head = Factory.AuthorizedClient("head1");
        var response = await head.PostAsJsonAsync("/api/execution-circuits", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("id").GetInt32();
    }

    private async Task TransferCircuitAsync(int circuitId, int? sectionId)
    {
        var manager = Factory.AuthorizedClient("manager");
        var response = await manager.PostAsJsonAsync($"/api/execution-circuits/{circuitId}/transfer", new
        {
            targetSectionId = sectionId,
        });
        response.EnsureSuccessStatusCode();
    }

    private async Task<int> CreateFileAsync(string token, int circuitId)
    {
        var client = Factory.ClientWithToken(token);
        var response = await client.PostAsJsonAsync("/api/documents", new
        {
            documentType = "بيان دعوى",
            borrowerName = UniqueName("مقترض"),
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

    /// <summary>
    /// يجلب معرّفات كل الصفحات حتى <c>totalPages</c> (P3-10): صفحة واحدة كانت
    /// تُخفي أي صف يتجاوز سقفها، وكانت التغطية تنجح بحجم بيانات صغير مصادفةً.
    /// </summary>
    private static async Task<List<int>> AllAlertIdsAsync(HttpClient client, int pageSize)
    {
        var ids = new List<int>();
        var page = 1;
        while (true)
        {
            var response = await client.GetAsync($"/api/alerts?page={page}&pageSize={pageSize}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
            var root = doc!.RootElement;
            var totalPages = root.GetProperty("totalPages").GetInt32();
            ids.AddRange(root.GetProperty("items").EnumerateArray()
                .Select(e => e.GetProperty("id").GetInt32()));
            if (page >= totalPages)
                break;
            page++;
        }
        return ids;
    }

    [Fact]
    public async Task CircuitPickers_SubHeadAllowed_HeadScoped()
    {
        var sectionId = await Factory.CreateSectionAsync(UniqueName("شعبة"));
        var sectionCircuit = await CreateCircuitAsync(UniqueName("شعبة-دائرة"));
        await TransferCircuitAsync(sectionCircuit, sectionId);
        var (subToken, _) = await CreateSubHeadAsync(sectionId);
        var subClient = Factory.ClientWithToken(subToken);

        // كانت 403 قبل فتح السمات للشعبة (§8 مراجعة).
        Assert.Equal(HttpStatusCode.OK, (await subClient.GetAsync("/api/execution-circuits/for-lawyer")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await subClient.GetAsync("/api/execution-circuits/for-delegation")).StatusCode);
    }

    [Fact]
    public async Task ManagerStats_HeadDivision_SubSection_ManagerAll()
    {
        var sectionId = await Factory.CreateSectionAsync(UniqueName("شعبة"));
        var divisionCircuit = await CreateCircuitAsync(UniqueName("قسم"));
        var sectionCircuit = await CreateCircuitAsync(UniqueName("شعبة-دائرة"));
        await TransferCircuitAsync(sectionCircuit, sectionId);
        var (subToken, _) = await CreateSubHeadAsync(sectionId);
        var subClient = Factory.ClientWithToken(subToken);
        var (_, lawyerToken) = await Factory.NewLawyerAsync();
        await CreateFileAsync(lawyerToken, divisionCircuit);
        await CreateFileAsync(lawyerToken, sectionCircuit);
        var head = Factory.AuthorizedClient("head1");
        var manager = Factory.AuthorizedClient("manager");

        // قرار 27: القسم لقسمه (1)، والشعبة لشعبتها (1)، والإدارة للكل (2).
        Assert.Equal(1, await ManagerTotalAsync(head));
        Assert.Equal(1, await ManagerTotalAsync(subClient));
        Assert.Equal(2, await ManagerTotalAsync(manager));
    }

    [Fact]
    public async Task CircuitStats_CarriesSection_AndExportsIt()
    {
        var sectionId = await Factory.CreateSectionAsync(UniqueName("شعبة"));
        var sectionCircuit = await CreateCircuitAsync(UniqueName("شعبة-دائرة"));
        await TransferCircuitAsync(sectionCircuit, sectionId);
        var manager = Factory.AuthorizedClient("manager");

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
        var divisionCircuit = await CreateCircuitAsync(UniqueName("قسم"));
        var (_, lawyerToken) = await Factory.NewLawyerAsync();
        await CreateFileAsync(lawyerToken, divisionCircuit);

        var lawyer = Factory.ClientWithToken(lawyerToken);
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
        var sectionId = await Factory.CreateSectionAsync(UniqueName("شعبة"));
        var (subToken, subId) = await CreateSubHeadAsync(sectionId);
        var subClient = Factory.ClientWithToken(subToken);
        var head = Factory.AuthorizedClient("head1");
        var branchId = await Factory.GetDamascusIdAsync();
        var headId = await Factory.UserIdAsync("head1");

        // ثلاثة تنبيهات للشعبة بأنواع مختلفة (المفتاح الدلالي يمنع التكرار بالنوع
        // نفسه) + واحد للقسم: حجم يتجاوز صفحة `pageSize=2` فيُثبت الجلب المتعدد.
        var subKinds = new[]
        {
            HeadAlertKindCatalog.DelegationPendingApproval,
            HeadAlertKindCatalog.AppealReferralToHead,
            HeadAlertKindCatalog.CircuitTransferred,
        };
        var subAlertIds = new List<int>();
        for (var i = 0; i < subKinds.Length; i++)
        {
            subAlertIds.Add(await HeadAlertTestSeed.SeedSingleHeadAsync(
                Factory, branchId, headId, subId, UniqueName($"للشعبة{i}"), subKinds[i]));
        }
        var headAlertId = await HeadAlertTestSeed.SeedSingleHeadAsync(
            Factory, branchId, headId, headId, UniqueName("للقسم"));

        var subIds = await AllAlertIdsAsync(subClient, pageSize: 2);
        Assert.True(subIds.Count >= 3, "التغطية تحتاج أكثر من صفحة واحدة");
        Assert.Equal(subIds.Count, subIds.Distinct().Count());
        foreach (var id in subAlertIds)
            Assert.Contains(id, subIds);
        Assert.DoesNotContain(headAlertId, subIds);

        var headIds = await AllAlertIdsAsync(head, pageSize: 2);
        Assert.Equal(headIds.Count, headIds.Distinct().Count());
        Assert.Contains(headAlertId, headIds);
        foreach (var id in subAlertIds)
            Assert.DoesNotContain(id, headIds);

        // تعليم القراءة يعمل للمستلم الرئيس.
        var mark = await subClient.PatchAsJsonAsync($"/api/alerts/{subAlertIds[0]}/read", new { });
        Assert.Equal(HttpStatusCode.NoContent, mark.StatusCode);
    }
}
