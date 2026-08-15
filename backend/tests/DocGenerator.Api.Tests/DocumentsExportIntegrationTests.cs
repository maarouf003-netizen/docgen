using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DocGenerator.Api.Tests;

public class DocumentsExportIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DocumentsExportIntegrationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Export_AsLawyer_ReturnsXlsxWithHeaders()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        await _factory.CreateDocumentAsync(token, "مستند تصدير", applicant: "المدعي", court: "دمشق");

        var client = _factory.AuthorizedClient("lawyer1");
        var response = await client.GetAsync("/api/documents/export");
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

    [Fact]
    public async Task Export_ProducesOpenableWorkbookWithValidAutoFilterReference()
    {
        var client = _factory.AuthorizedClient("manager");
        var response = await client.GetAsync("/api/documents/export");
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var stream = new MemoryStream(bytes);
        using var doc = SpreadsheetDocument.Open(stream, false);
        var worksheetPart = doc.WorkbookPart!.WorksheetParts.First();
        var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()!;
        var headerRow = sheetData.Elements<Row>().First();

        // رؤوس المدير الكاملة بتسلسلها المعتمد:
        // فرع الإدارة، الحالة، طالب التنفيذ، الفرع، المنفذ عليه، دائرة التنفيذ،
        // رقم الملف، ملحق العقد، المحامي المختص، الإجراءات والملاحظات، عدد المشاهدات.
        var headers = headerRow.Elements<Cell>()
            .Select(c => c.InlineString?.Text?.Text ?? string.Empty)
            .ToList();
        Assert.Equal(new[]
        {
            "فرع الإدارة", "الحالة", "طالب التنفيذ", "الفرع", "المنفذ عليه",
            "دائرة التنفيذ", "رقم الملف", "ملحق العقد", "المحامي المختص", "الإجراءات والملاحظات", "عدد المشاهدات",
        }, headers);

        // AutoFilter يملك Reference صالحًا يغطي العنوان والبيانات (صالح في إكسل).
        var autoFilter = worksheetPart.Worksheet.GetFirstChild<AutoFilter>();
        Assert.NotNull(autoFilter);
        Assert.False(string.IsNullOrWhiteSpace(autoFilter!.Reference?.Value));
    }

    [Fact]
    public async Task Export_FilteredByLawyer_ForbiddenForLawyerRole()
    {
        var client = _factory.AuthorizedClient("lawyer1");
        var response = await client.GetAsync("/api/documents/export?lawyer=مقترض");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Export_AsManager_OkAndForbiddenForAnonymous()
    {
        var client = _factory.AuthorizedClient("manager");
        var response = await client.GetAsync("/api/documents/export");
        response.EnsureSuccessStatusCode();

        var anonymous = _factory.CreateClient();
        var forbidden = await anonymous.GetAsync("/api/documents/export");
        Assert.Equal(HttpStatusCode.Unauthorized, forbidden.StatusCode);
    }

    [Fact]
    public async Task Export_IncludesAnnexNumberInItsColumn()
    {
        // عمود «ملحق العقد» في التصدير يعرض رقم الملحق للمصرفي.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var create = await client.PostAsJsonAsync("/api/documents", new
        {
            generalEntitySide = "applicant",
            borrowerName = "مقترض تصدير ملحق",
            applicant = "المصرف",
            contractType = "تعهد",
            contractTypeSelector = "مصرفي",
            annexNumber = "A-12345",
            amountNumeric = 100,
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.GetAsync("/api/documents/export");
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
}
