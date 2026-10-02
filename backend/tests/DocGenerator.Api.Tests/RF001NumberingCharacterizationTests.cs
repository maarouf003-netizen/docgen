using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبارات RF-001/RF-009 لوحدانية رقم الأساس (INT-001) وتفاعلها مع
/// الحذف المنطقي/الاستعادة (INT-008). عُكست توقعاتها في RF-009: التكرار مرفوض 409.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class RF001NumberingCharacterizationTests
{
    private readonly ApiFactory _factory;

    public RF001NumberingCharacterizationTests(ApiFactory factory) => _factory = factory;

    private static object NumberedPayload(string borrower, string number) => new
    {
        documentType = "بيان دعوى",
        borrowerName = borrower,
        applicant = "المدعي",
        court = "دمشق",
        contractType = "تعهد",
        amountNumeric = 100,
        fileNumber = number,
        fileYear = "2026",
        fileRegistrationDate = "1/8/2026",
    };

    private static async Task<int> CreateIdAsync(HttpClient client, object payload)
    {
        var response = await client.PostAsJsonAsync("/api/documents", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return body!.RootElement.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task SameBasisNumber_TwoCreates_SecondConflict()
    {
        // RF-009: القيد الفريد الجزئي + الفحص الخدمي — الثاني 409 برسالة ودية.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var number = $"9{Random.Shared.Next(100000, 999999)}";

        var first = await CreateIdAsync(client, NumberedPayload("تكرار أول", number));

        var second = await client.PostAsJsonAsync("/api/documents", NumberedPayload("تكرار ثانٍ", number));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/documents/{first}")).StatusCode);
    }

    [Fact]
    public async Task RestoreAfterNumberReuse_Conflict()
    {
        // RF-009 (INT-008): الاستعادة تفحص تعارض المفتاح الفعّال — 409 بدل تكرار ظاهري.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var number = $"8{Random.Shared.Next(100000, 999999)}";

        var doomed = await CreateIdAsync(client, NumberedPayload("سيُحذف", number));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/documents/{doomed}")).StatusCode);

        await CreateIdAsync(client, NumberedPayload("إعادة استعمال", number));

        var restore = await client.PostAsync($"/api/documents/{doomed}/restore", null);
        Assert.Equal(HttpStatusCode.Conflict, restore.StatusCode);
    }
}
