using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Api.Security;
using DocGenerator.Api.Tests.TestHelpers;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

[Collection(PostgresApiTestCollection.Name)]
public class DocumentsExportIntegrationTests : IAsyncLifetime
{
    private readonly PostgresApiFactory _factory;

    public DocumentsExportIntegrationTests(PostgresApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.ResetAsync();
        // المسارات المباشرة تُرسل court نصيًا؛ عند وجود دوائر يجب أن تطابقه.
        await _factory.EnsureCircuitAsync("DAM", "دمشق");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static int ExportNumberSeq;

    /// <summary>
    /// فلتر إلزامي لنجاح التصدير (`W5-F11`): «تحت رفع» يطابق المسودات التي ينشئها
    /// `CreateDocumentAsync` بلا رقم قيد؛ «متداول» للملفات المسجّلة غير المسودة.
    /// </summary>
    private static readonly string DraftExport =
        "/api/documents/export?status=" + Uri.EscapeDataString("تحت رفع");

    private static readonly string CirculatingExport =
        "/api/documents/export?status=" + Uri.EscapeDataString("متداول");

    [PostgresFact]
    public async Task Export_AsLawyer_ReturnsXlsxWithHeaders()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        await _factory.CreateDocumentAsync(token, "مستند تصدير", applicant: "المدعي", court: "دمشق");

        var client = _factory.AuthorizedClient("lawyer1");
        var response = await client.GetAsync(DraftExport);
        response.EnsureSuccessStatusCode();

        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        // توقيع حزمة ZIP الخاصة بـ xlsx (PK).
        Assert.True(bytes.Length > 100, "الملف المصدر فارغ أو صغير جدًا");
        Assert.Equal('P', (char)bytes[0]);
        Assert.Equal('K', (char)bytes[1]);
    }

    [PostgresFact]
    public async Task Export_ProducesOpenableWorkbookWithValidAutoFilterReference()
    {
        var client = _factory.AuthorizedClient("manager");
        var response = await client.GetAsync(DraftExport);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var stream = new MemoryStream(bytes);
        using var doc = SpreadsheetDocument.Open(stream, false);
        var worksheetPart = doc.WorkbookPart!.WorksheetParts.First();
        var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()!;
        var headerRow = sheetData.Elements<Row>().First();

        // رؤوس المدير الكاملة بتسلسلها المعتمد:
        // فرع الإدارة، الحالة، طالب التنفيذ، الفرع، المنفذ عليه، دائرة التنفيذ،
        // الشعبة، رقم الملف، لعام، ملحق العقد، المحامي المختص، الإجراءات والملاحظات.
        var headers = headerRow.Elements<Cell>()
            .Select(c => c.InlineString?.Text?.Text ?? string.Empty)
            .ToList();
        Assert.Equal(new[]
        {
            "فرع الإدارة", "الحالة", "طالب التنفيذ", "الفرع", "المنفذ عليه",
            "دائرة التنفيذ", "الشعبة", "رقم الملف", "لعام", "ملحق العقد", "المحامي المختص", "الإجراءات والملاحظات",
        }, headers);

        // AutoFilter يملك Reference صالحًا يغطي العنوان والبيانات (صالح في إكسل).
        var autoFilter = worksheetPart.Worksheet.GetFirstChild<AutoFilter>();
        Assert.NotNull(autoFilter);
        Assert.False(string.IsNullOrWhiteSpace(autoFilter!.Reference?.Value));
    }

    [PostgresFact]
    public async Task Export_FilteredByLawyer_ForbiddenForLawyerRole()
    {
        var client = _factory.AuthorizedClient("lawyer1");
        var response = await client.GetAsync("/api/documents/export?lawyer=مقترض");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [PostgresFact]
    public async Task Export_AsManager_OkAndForbiddenForAnonymous()
    {
        var client = _factory.AuthorizedClient("manager");
        var response = await client.GetAsync(DraftExport);
        response.EnsureSuccessStatusCode();

        var anonymous = _factory.CreateClient();
        var forbidden = await anonymous.GetAsync(DraftExport);
        Assert.Equal(HttpStatusCode.Unauthorized, forbidden.StatusCode);
    }

    [PostgresFact]
    public async Task Export_WithoutAnyFilter_IsRejectedWithValidationMessage()
    {
        // W5-F11: المنع خلفي — حتى المدير لا يصدّر كل الملفات بلا فلتر.
        var response = await _factory.AuthorizedClient("manager").GetAsync("/api/documents/export");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("فلتر", body.RootElement.GetProperty("message").GetString());
    }

    [PostgresFact]
    public async Task Export_IncludesAnnexNumberInItsColumn()
    {
        // عمود «ملحق العقد» في التصدير يعرض رقم الملحق للمصرفي.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.ClientWithToken(token);
        var create = await client.PostAsJsonAsync("/api/documents", new
        {
            generalEntitySide = "applicant",
            borrowerName = "مقترض تصدير ملحق",
            applicant = "المصرف",
            court = "دمشق",
            contractType = "تعهد",
            contractTypeSelector = "مصرفي",
            annexNumber = "A-12345",
            amountNumeric = 100,
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.GetAsync(DraftExport);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var stream = new MemoryStream(bytes);
        using var doc = SpreadsheetDocument.Open(stream, false);
        var sheetData = doc.WorkbookPart!.WorksheetParts.First().Worksheet.GetFirstChild<SheetData>()!;
        var headerRow = sheetData.Elements<Row>().First();
        var headers = headerRow.Elements<Cell>()
            .Select(c => c.InlineString?.Text?.Text ?? string.Empty)
            .ToList();
        var annexCol = headers.IndexOf("ملحق العقد");
        Assert.True(annexCol >= 0, "عمود «ملحق العقد» غير موجود في التصدير");

        var dataRows = sheetData.Elements<Row>()
            .Skip(1)
            .Select(r => r.Elements<Cell>().Select(c => c.InlineString?.Text?.Text ?? string.Empty).ToList())
            .ToList();
        Assert.Contains(dataRows, row => row.Count > annexCol && row[annexCol] == "A-12345");
    }

    [PostgresFact]
    public async Task Export_DefaultExcludesReferred_AndTextSearchIncludesIt()
    {
        // التصدير يمر عبر نفس الحجب المشروط بالبحث: الافتراضي يستبعد المحال، ومع نص بحث
        // مطابق يشمله (بحث شامل لكل الملفات غير المحذوفة أيا كانت حالتها).
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.ClientWithToken(token);
        // ملف متداول مسجل (غير مسودة) كي يمر انتقال «محال الى البداية» من آلة الحالات.
        var create = await client.PostAsJsonAsync("/api/documents", new
        {
            generalEntitySide = "applicant",
            borrowerName = "مقترض إحالة",
            applicant = "المدعي",
            court = "دمشق",
            contractType = "تعهد",
            amountNumeric = 500,
            fileNumber = $"520{System.Threading.Interlocked.Increment(ref ExportNumberSeq):D4}",
            fileYear = "2024",
            fileRegistrationDate = "1/1/2024",
            branchName = "الفرع الرئيسي - دمشق",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonDocument>())!.RootElement.GetProperty("id").GetInt32();
        var referred = await client.PostAsJsonAsync($"/api/documents/{id}/status",
            new { status = "محال الى البداية", fields = new { noFundsDemandNumber = "5", noFundsDemandDate = "1/1/2024", startReferralNumber = "6", startReferralDate = "2/1/2024" } });
        Assert.Equal(HttpStatusCode.OK, referred.StatusCode);

        var manager = _factory.AuthorizedClient("manager");

        var plain = await manager.GetAsync(
            "/api/documents/export?court=" + Uri.EscapeDataString("دمشق"));
        Assert.True(plain.IsSuccessStatusCode);
        var plainRows = DataRows(await plain.Content.ReadAsByteArrayAsync());
        var plainStatuses = plainRows
            .Select(row => row[1]) // عمود «الحالة» بعد «فرع الإدارة».
            .ToList();
        Assert.DoesNotContain("محال الى البداية", plainStatuses);

        var searched = await manager.GetAsync("/api/documents/export?q=" + Uri.EscapeDataString("مقترض"));
        Assert.True(searched.IsSuccessStatusCode);
        var searchedRows = DataRows(await searched.Content.ReadAsByteArrayAsync());
        Assert.Contains(searchedRows, row => row.Count > 1 && row[1] == "محال الى البداية");
    }

    [PostgresFact]
    public async Task Export_RowProjection_ValuesMatch()
    {
        // الإسقاط العمودي: قيم الخلايا مطابقة لمسار الكيان الكامل
        // (الحالة محلولة، طالب التنفيذ، المنفذ عليه، نص الإجراء الأول).
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var id = await _factory.CreateDocumentAsync(token, "مقترض تصدير غني", applicant: "المصرف", court: "دمشق");
        var lawyer = _factory.ClientWithToken(token);
        var action = await lawyer.PostAsJsonAsync($"/api/documents/{id}/actions",
            new { text = "إجراء متابعة صريح", actionDate = "1/8/2026" });
        Assert.Equal(HttpStatusCode.OK, action.StatusCode);

        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.GetAsync(DraftExport);
        response.EnsureSuccessStatusCode();

        var rows = DataRows(await response.Content.ReadAsByteArrayAsync());
        // الأعمدة (مدير): 0 فرع الإدارة، 1 الحالة، 2 طالب التنفيذ، 3 الفرع،
        // 4 المنفذ عليه، 5 الدائرة، 6 الشعبة، 7 رقم الملف، 8 لعام، 9 الملحق،
        // 10 المحامي، 11 الإجراءات، 12 المشاهدات.
        var row = Assert.Single(rows, r => r.Count > 4 && r[4] == "مقترض تصدير غني");
        Assert.Equal("المصرف", row[2]);
        Assert.False(string.IsNullOrWhiteSpace(row[1]));
        Assert.Equal("إجراء متابعة صريح", row[11]);
    }

    [PostgresFact]
    public async Task Export_WhileSlotHeld_Returns429ThenSucceedsAfterRelease()
    {
        // حتمي بلا سباق: نحجز خانة المستخدم يدويًا بنفس صيغة مفتاح الفلتر،
        // فالطلب يُردّ 429 برسالة الانشغال، وبعد التحرير ينجح — ويثبت هذا
        // أيضًا أن الخانة لا تتسرب (الصادرات المتسلسلة في بقية الاختبارات خضراء).
        var guard = _factory.Services.GetRequiredService<ExportConcurrencyGuard>();
        int lawyerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            lawyerId = await db.Users.Where(u => u.Username == "lawyer1").Select(u => u.Id).SingleAsync();
        }
        var key = ExportConcurrencyGuard.KeyFor(lawyerId.ToString(), null);
        Assert.True(guard.TryEnter(key));
        try
        {
            var client = _factory.AuthorizedClient("lawyer1");
            var busy = await client.GetAsync(DraftExport);
            Assert.Equal(HttpStatusCode.TooManyRequests, busy.StatusCode);
            // الجسم `JSON` بهروب `\uXXXX` (المسرِّع الافتراضي) فيُفكّ قبل المقارنة.
            using var body = JsonDocument.Parse(await busy.Content.ReadAsStringAsync());
            Assert.Contains("قيد التنفيذ", body.RootElement.GetProperty("message").GetString());
        }
        finally
        {
            guard.Exit(key);
        }

        var retry = await _factory.AuthorizedClient("lawyer1").GetAsync(DraftExport);
        Assert.True(retry.IsSuccessStatusCode);
    }

    [PostgresFact]
    public async Task Export_LawyerScope_ExcludesOtherLawyersDocuments()
    {
        // W2/F17: عزل التصدير بمالك المستند. كان الاختبار السابق ينشئ مستند محامٍ واحد
        // فقط، فحذف فلتر `VisibleUserId` لا يُفشل أي اختبار (طفرة ناجية).
        var lawyer1Token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        await _factory.CreateDocumentAsync(lawyer1Token, "زبون المحامي الأول", court: "محكمة", registered: true);

        var other = await _factory.CreateUserAsync(
            $"exp_other_{Guid.NewGuid():N}"[..20],
            UserRole.Lawyer,
            await _factory.GetBranchIdAsync("DAM"));
        var otherLogin = await _factory.LoginAsync(other.Username, "123456");
        Assert.NotNull(otherLogin?.Token);
        await _factory.CreateDocumentAsync(otherLogin!.Token!, "زبون المحامي الثاني", court: "محكمة", registered: true);

        var response = await _factory.AuthorizedClient("lawyer1").GetAsync(CirculatingExport);
        response.EnsureSuccessStatusCode();
        var rows = DataRows(await response.Content.ReadAsByteArrayAsync());

        // أعمدة الصف بعد العنوان: 0 الحالة، 1 المدعي، 2 الفرع، 3 المدعى عليه (المقترض)، 4 المحكمة.
        Assert.Contains(rows, row => row.Count > 3 && row[3] == "زبون المحامي الأول");
        Assert.DoesNotContain(rows, row => row.Count > 3 && row[3] == "زبون المحامي الثاني");
    }

    private static List<List<string>> DataRows(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var doc = SpreadsheetDocument.Open(stream, false);
        var sheetData = doc.WorkbookPart!.WorksheetParts.First().Worksheet.GetFirstChild<SheetData>()!;
        return sheetData.Elements<Row>()
            .Skip(1)
            .Select(r => r.Elements<Cell>().Select(c => c.InlineString?.Text?.Text ?? string.Empty).ToList())
            .ToList();
    }
}
