using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Api.Tests;

public class WordGenerationIntegrationTests : IClassFixture<ApiFactory>
{
    private const string WordContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly ApiFactory _factory;

    public WordGenerationIntegrationTests(ApiFactory factory) => _factory = factory;

    private async Task<string> LoginLawyerAsync() =>
        (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;

    private static async Task<int> CreateFullDocumentAsync(ApiFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.SetAuthCookie(token);
        var response = await client.PostAsJsonAsync("/api/documents", new
        {
            borrowerName = "أحمد",
            borrowerFather = "خالد",
            borrowerFamily = "الخطيب",
            borrowerAddress = "المزة",
            borrowerAddressType = "موطن مختار",
            court = "دمشق",
            contractType = "تعهد",
            contractTypeSelector = "مصرفي",
            contractNumber = "12/2024",
            amountNumeric = 500,
            applicant = "المدير العام",
            lawyer = "المحامي",
            fileNumber = "520",
            fileType = "أساس",
            fileYear = "2024",
            fileRegistrationDate = "01/01/2024",
            seizureDate = "15/03/2025",
            guarantors = new[]
            {
                new
                {
                    guarantorNumber = 1,
                    name = "سمير",
                    father = "حسن",
                    family = "علي",
                    address = "حلب",
                    addressType = "موطن مختار",
                },
            },
            realEstates = new[]
            {
                new
                {
                    owners = new[] { "أحمد خالد الخطيب" },
                    property = "منزل",
                    propertyNumber = "12",
                    propertyDistrict = "المزة",
                    landRegistry = "سجل 3",
                    shareType = "كامل",
                },
            },
        });
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<int> GetFirstEstateIdAsync(HttpClient client, int docId)
    {
        var response = await client.GetAsync($"/api/documents/{docId}");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("realEstates")[0].GetProperty("id").GetInt32();
    }

    private static async Task<(int DocId, int HeirId, int EstateId)> CreateDocumentWithHeirsAndEstateAsync(
        ApiFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.SetAuthCookie(token);
        var response = await client.PostAsJsonAsync("/api/documents", new
        {
            borrowerName = "أحمد",
            borrowerFather = "خالد",
            borrowerFamily = "الخطيب",
            court = "دمشق",
            contractType = "تعهد",
            contractTypeSelector = "مصرفي",
            amountNumeric = 500,
            borrowerHeirs = new[]
            {
                new { name = "محمود الحلبي", addressType = "عنوان", address = "المزة" },
            },
            realEstates = new[]
            {
                new
                {
                    owners = new[] { "أحمد خالد الخطيب" },
                    property = "منزل",
                    propertyNumber = "12",
                    propertyDistrict = "المزة",
                    landRegistry = "سجل 3",
                    shareType = "كامل",
                },
            },
        });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var docId = doc.RootElement.GetProperty("id").GetInt32();
        var heirId = doc.RootElement.GetProperty("borrowerHeirs")[0].GetProperty("id").GetInt32();
        var estateId = doc.RootElement.GetProperty("realEstates")[0].GetProperty("id").GetInt32();
        return (docId, heirId, estateId);
    }

    private static async Task<string> ReadDocumentXmlAsync(HttpResponseMessage response)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var ms = new MemoryStream(bytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var entry = zip.GetEntry("word/document.xml");
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open(), Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task Generate_SeizureTemplate_ReturnsValidDocxWithFilledPlaceholders()
    {
        var token = await LoginLawyerAsync();
        var id = await CreateFullDocumentAsync(_factory, token);

        var client = _factory.CreateClient();
        client.SetAuthCookie(token);
        var response = await client.GetAsync($"/api/documents/{id}/generate?template=004");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WordContentType, response.Content.Headers.ContentType!.MediaType);
        Assert.EndsWith(".docx", response.Content.Headers.ContentDisposition!.FileName!);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000);

        using var ms = new MemoryStream(bytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var contentTypes = zip.GetEntry("[Content_Types].xml");
        var document = zip.GetEntry("word/document.xml");
        Assert.NotNull(contentTypes);
        Assert.NotNull(document);

        using var reader = new StreamReader(document!.Open(), Encoding.UTF8);
        var xml = await reader.ReadToEndAsync();
        Assert.DoesNotContain("{{", xml);
        Assert.DoesNotContain("}}", xml);
        Assert.Contains("دائرة تنفيذ دمشق", xml);
        Assert.Contains("أحمد", xml);
        Assert.Contains("سمير", xml);
        Assert.Contains("520 أساس", xml.Replace("&#10;", "\n"));
    }

    [Fact]
    public async Task Generate_MissingTemplateParam_ReturnsBadRequest()
    {
        var token = await LoginLawyerAsync();
        var id = await CreateFullDocumentAsync(_factory, token);

        var client = _factory.CreateClient();
        client.SetAuthCookie(token);
        var response = await client.GetAsync($"/api/documents/{id}/generate");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_UnknownTemplateCode_ReturnsBadRequest()
    {
        var token = await LoginLawyerAsync();
        var id = await CreateFullDocumentAsync(_factory, token);

        var client = _factory.CreateClient();
        client.SetAuthCookie(token);
        var response = await client.GetAsync($"/api/documents/{id}/generate?template=999");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_NotFoundDocument_ReturnsNotFound()
    {
        var token = await LoginLawyerAsync();

        var client = _factory.CreateClient();
        client.SetAuthCookie(token);
        var response = await client.GetAsync($"/api/documents/999999/generate?template=004");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Generate_ByLawyerFromAnotherBranch_ReturnsForbidden()
    {
        var token = await LoginLawyerAsync();
        var id = await CreateFullDocumentAsync(_factory, token);

        var other = await _factory.CreateUserAsync("other_lawyer", UserRole.Lawyer, branchId: 2);
        Assert.NotNull(other);

        var client = _factory.AuthorizedClient("other_lawyer");
        var response = await client.GetAsync($"/api/documents/{id}/generate?template=004");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Generate_GuarantorNotice_WithRecipient_ReturnsValidDocx()
    {
        var token = await LoginLawyerAsync();
        var id = await CreateFullDocumentAsync(_factory, token);

        var client = _factory.CreateClient();
        client.SetAuthCookie(token);
        var response = await client.GetAsync($"/api/documents/{id}/generate?template=003&recipient=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WordContentType, response.Content.Headers.ContentType!.MediaType);
        var xml = await ReadDocumentXmlAsync(response);
        Assert.DoesNotContain("{{", xml);
        Assert.DoesNotContain("}}", xml);
        Assert.Contains("سمير حسن علي", xml);
    }

    [Fact]
    public async Task Generate_PropertySale_WithoutEstate_ReturnsBadRequest()
    {
        var token = await LoginLawyerAsync();
        var id = await CreateFullDocumentAsync(_factory, token);

        var client = _factory.CreateClient();
        client.SetAuthCookie(token);
        var response = await client.GetAsync($"/api/documents/{id}/generate?template=005");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_PropertySale_WithEstate_ReturnsValidDocx()
    {
        var token = await LoginLawyerAsync();
        var id = await CreateFullDocumentAsync(_factory, token);

        var client = _factory.CreateClient();
        client.SetAuthCookie(token);
        var estateId = await GetFirstEstateIdAsync(client, id);
        var response = await client.GetAsync($"/api/documents/{id}/generate?template=005&estateIds={estateId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WordContentType, response.Content.Headers.ContentType!.MediaType);
        var xml = await ReadDocumentXmlAsync(response);
        Assert.DoesNotContain("{{", xml);
        Assert.DoesNotContain("}}", xml);
        Assert.Contains("تمام عقارك رقم 12 من المنطقة العقارية المزة", xml);
    }

    [Fact]
    public async Task Generate_PropertySalePaper_WithEstate_ReturnsValidDocx()
    {
        var token = await LoginLawyerAsync();
        var id = await CreateFullDocumentAsync(_factory, token);

        var client = _factory.CreateClient();
        client.SetAuthCookie(token);
        var estateId = await GetFirstEstateIdAsync(client, id);
        var response = await client.GetAsync($"/api/documents/{id}/generate?template=006&estateIds={estateId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WordContentType, response.Content.Headers.ContentType!.MediaType);
        var xml = await ReadDocumentXmlAsync(response);
        Assert.DoesNotContain("{{", xml);
        Assert.DoesNotContain("}}", xml);
        Assert.Contains("تمام عقارك رقم 12 من المنطقة العقارية المزة", xml);
    }

    [Fact]
    public async Task Generate_NoticePaper_WithRecipient_ReturnsValidDocx()
    {
        var token = await LoginLawyerAsync();
        var id = await CreateFullDocumentAsync(_factory, token);

        var client = _factory.CreateClient();
        client.SetAuthCookie(token);
        var response = await client.GetAsync($"/api/documents/{id}/generate?template=007&recipient=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WordContentType, response.Content.Headers.ContentType!.MediaType);
        var xml = await ReadDocumentXmlAsync(response);
        Assert.DoesNotContain("{{", xml);
        Assert.DoesNotContain("}}", xml);
        Assert.Contains("أحمد", xml);
    }

    [Fact]
    public async Task Generate_HeirNotice_WithHeirId_ReturnsValidDocx()
    {
        var token = await LoginLawyerAsync();
        var (id, heirId, _) = await CreateDocumentWithHeirsAndEstateAsync(_factory, token);

        var client = _factory.CreateClient();
        client.SetAuthCookie(token);
        var response = await client.GetAsync($"/api/documents/{id}/generate?template=003&heirId={heirId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WordContentType, response.Content.Headers.ContentType!.MediaType);
        var xml = await ReadDocumentXmlAsync(response);
        Assert.DoesNotContain("{{", xml);
        Assert.DoesNotContain("}}", xml);
        Assert.Contains("محمود الحلبي إضافة لتركة المتوفى (أحمد خالد الخطيب)", xml);
        Assert.Contains("عنوانه المزة", xml);
    }

    [Fact]
    public async Task Generate_PropertySaleHeir_WithHeirId_ReturnsValidDocx()
    {
        var token = await LoginLawyerAsync();
        var (id, heirId, estateId) = await CreateDocumentWithHeirsAndEstateAsync(_factory, token);

        var client = _factory.CreateClient();
        client.SetAuthCookie(token);
        var response = await client.GetAsync($"/api/documents/{id}/generate?template=005&estateIds={estateId}&heirId={heirId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WordContentType, response.Content.Headers.ContentType!.MediaType);
        var xml = await ReadDocumentXmlAsync(response);
        Assert.DoesNotContain("{{", xml);
        Assert.DoesNotContain("}}", xml);
        Assert.Contains("محمود الحلبي إضافة لتركة المتوفى (أحمد خالد الخطيب)", xml);
    }

    [Fact]
    public async Task Generate_HeirPaperNotice_WithHeirId_ReturnsValidDocx()
    {
        var token = await LoginLawyerAsync();
        var (id, heirId, _) = await CreateDocumentWithHeirsAndEstateAsync(_factory, token);

        var client = _factory.CreateClient();
        client.SetAuthCookie(token);
        var response = await client.GetAsync($"/api/documents/{id}/generate?template=007&heirId={heirId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WordContentType, response.Content.Headers.ContentType!.MediaType);
        var xml = await ReadDocumentXmlAsync(response);
        Assert.DoesNotContain("{{", xml);
        Assert.DoesNotContain("}}", xml);
        Assert.Contains("محمود الحلبي إضافة لتركة المتوفى (أحمد خالد الخطيب)", xml);
        Assert.DoesNotContain("عنوانه المزة", xml);
    }

    [Fact]
    public async Task Generate_PropertySeizure_WithEstate_ReturnsValidDocx()
    {
        var token = await LoginLawyerAsync();
        var id = await CreateFullDocumentAsync(_factory, token);

        var client = _factory.CreateClient();
        client.SetAuthCookie(token);
        var estateId = await GetFirstEstateIdAsync(client, id);
        var response = await client.GetAsync($"/api/documents/{id}/generate?template=PS&estateIds={estateId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(WordContentType, response.Content.Headers.ContentType!.MediaType);
        var xml = await ReadDocumentXmlAsync(response);
        Assert.DoesNotContain("{{", xml);
        Assert.DoesNotContain("}}", xml);
        Assert.Contains("سجل 3", xml);
        Assert.Contains("12", xml);
    }
}
