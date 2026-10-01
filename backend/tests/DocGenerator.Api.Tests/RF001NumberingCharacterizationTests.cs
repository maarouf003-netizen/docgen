using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبارات توصيف RF-001 للسلوك الحالي لوحدانية رقم الأساس (INT-001) وتفاعلها مع
/// الحذف المنطقي/الاستعادة (INT-008). هذه الاختبارات خضراء على الكود الحالي عمدًا —
/// توثّق الواقع الذي ستغيّره RF-009 (قيد فريد جزئي + 409).
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
    public async Task SameBasisNumber_TwoCreates_BothSucceed_CurrentBehavior()
    {
        // السلوك الحالي (INT-001): لا قيد قاعدة ولا فحص خدمي — التكرار يُقبَل بصمت.
        // بعد RF-009 يجب أن تُعكَس هذه التوقعات: الثاني 409 Conflict.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var number = $"9{Random.Shared.Next(100000, 999999)}";

        var first = await CreateIdAsync(client, NumberedPayload("تكرار أول", number));
        var second = await CreateIdAsync(client, NumberedPayload("تكرار ثانٍ", number));

        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/documents/{first}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/documents/{second}")).StatusCode);
    }

    [Fact]
    public async Task RestoreAfterNumberReuse_BothVisible_CurrentBehavior()
    {
        // السلوك الحالي (INT-008): الاستعادة لا تفحص تعارض الأرقام — ملفان ظاهران بنفس الرقم.
        // بعد RF-009 يجب أن تُعكَس: الاستعادة المتعارضة 409.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var number = $"8{Random.Shared.Next(100000, 999999)}";

        var doomed = await CreateIdAsync(client, NumberedPayload("سيُحذف", number));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/documents/{doomed}")).StatusCode);

        var reuse = await CreateIdAsync(client, NumberedPayload("إعادة استعمال", number));

        var restore = await client.PostAsync($"/api/documents/{doomed}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/documents/{doomed}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/documents/{reuse}")).StatusCode);
    }
}
