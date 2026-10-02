using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبارات RF-010 (INT-002 + INT-012): التعارض `409` بدل المحو الصامت.
/// تفشل قبل الإصلاح (نجاح ماحٍ `200`) وتخضر بعده.
/// التوافق: غياب `version` يُقبل (عملاء قدامى)؛ النوع المخالف `400`.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class RF010ConcurrencyTests
{
    private readonly ApiFactory _factory;

    public RF010ConcurrencyTests(ApiFactory factory) => _factory = factory;

    private static async Task<long?> GetVersionAsync(HttpClient client, int id)
    {
        using var doc = await (await client.GetAsync($"/api/documents/{id}")).Content.ReadFromJsonAsync<JsonDocument>();
        if (doc!.RootElement.TryGetProperty("version", out var v)
            && v.ValueKind == JsonValueKind.Number
            && v.TryGetInt64(out var n))
            return n;
        return null;
    }

    [Fact]
    public async Task FreshEdit_Succeeds()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token, borrowerName: "تزامن طازج");

        var version = await GetVersionAsync(client, id);
        var response = await client.PutAsJsonAsync($"/api/documents/{id}", new
        {
            borrowerName = "تزامن طازج معدل",
            version,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FreshEdit_BumpsVersion()
    {
        // الحفظ الناجح يزيد العدّاد — فتُكشَف القراءة القديمة لاحقًا.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token, borrowerName: "زيادة العدّاد");

        var before = await GetVersionAsync(client, id);
        var response = await client.PutAsJsonAsync($"/api/documents/{id}", new
        {
            borrowerName = "زيادة العدّاد معدل",
            version = before,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await GetVersionAsync(client, id);
        Assert.NotNull(after);
        Assert.True(after > (before ?? -1), $"expected bump, was {before} now {after}");
    }

    [Fact]
    public async Task StaleEdit_SecondPut_Conflict()
    {
        // قراءتان لنفس الملف → حفظ الأول → إعادة حفظ الثاني بالنسخة القديمة.
        // اليوم: `200` (محو صامت)؛ بعد RF-010: `409`.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token, borrowerName: "تحرير متعارض");

        var stale = await GetVersionAsync(client, id);

        var first = await client.PutAsJsonAsync($"/api/documents/{id}", new
        {
            borrowerName = "الفائز الأول",
            version = stale,
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PutAsJsonAsync($"/api/documents/{id}", new
        {
            borrowerName = "المتأخر الممحي",
            version = stale,
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task StaleStatus_SecondPost_Conflict()
    {
        // «تسوية» صالحة من متداول ومن تريث معًا — فيعزل الفحصُ الإصدارَ عن الآلة:
        // اليوم: `200` (انتقال ضائع)؛ بعد RF-010: `409`.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token, borrowerName: "حالة متعارضة");

        var stale = await GetVersionAsync(client, id);

        var defer = await client.PostAsJsonAsync($"/api/documents/{id}/status", new
        {
            status = "تريث",
            fields = new { tarithNumber = "9", tarithDate = "1/1/2024" },
            version = stale,
        });
        Assert.Equal(HttpStatusCode.OK, defer.StatusCode);

        var settled = await client.PostAsJsonAsync($"/api/documents/{id}/status", new
        {
            status = "منفذ بالتسوية",
            fields = new { baraetNumber = "3", baraetDate = "2/1/2024" },
            version = stale,
        });
        Assert.Equal(HttpStatusCode.Conflict, settled.StatusCode);
    }

    [Fact]
    public async Task MissingVersion_Accepted_BackwardCompatible()
    {
        // عملاء قدامى بلا الحقل: يُقبلون قبل الإصلاح وبعده (لا كسر).
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token, borrowerName: "توافق قديم");

        var response = await client.PutAsJsonAsync($"/api/documents/{id}", new
        {
            borrowerName = "توافق قديم معدل",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MalformedVersion_BadRequest()
    {
        // نوع مخالف للعدّاد → `400` (اليوم: يُتجاهل فينجح `200`).
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token, borrowerName: "نسخة تالفة");

        var response = await client.PutAsJsonAsync($"/api/documents/{id}", new
        {
            borrowerName = "نسخة تالفة معدلة",
            version = "!!!not-a-number!!!",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
