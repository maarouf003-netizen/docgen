using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبارات توصيف RF-001 للسلوك الحالي لانتقالات الحالة المتزامنة (INT-002/INT-012).
/// خضراء على الكود الحالي عمدًا — توثّق الواقع الذي ستغيّره RF-010 (تزامن متفائل + 409).
/// ملاحظة حتمية: الانتقالان المتوازيان قد يُسلسَلا (الثاني 400) أو يتداخلا (كلاهما 200) حسب
/// توقيت الالتزام — لذلك يؤكَّد هنا الثابت فقط: لا 500 أبدًا، والحالة النهائية متسقة.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class RF001ConcurrentStatusCharacterizationTests
{
    private readonly ApiFactory _factory;

    public RF001ConcurrentStatusCharacterizationTests(ApiFactory factory) => _factory = factory;

    private static object DeferralPayload(string number) => new
    {
        status = "تريث",
        fields = new { tarithNumber = number, tarithDate = "1/1/2024" },
    };

    private static async Task<string?> FinalStatusAsync(HttpClient client, int id)
    {
        using var doc = await (await client.GetAsync($"/api/documents/{id}")).Content.ReadFromJsonAsync<JsonDocument>();
        var prop = doc!.RootElement.GetProperty("execStatus");
        return prop.ValueKind == JsonValueKind.Null ? null : prop.GetString();
    }

    [Fact]
    public async Task SequentialDeferralTwice_SecondRejected_CurrentBehavior()
    {
        // الآلة مطبَّقة خدميًا اليوم: تريث → تريث مرفوض (AllowedStatusChanges/CanRevert).
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token, borrowerName: "سباق تسلسلي");

        var first = await client.PostAsJsonAsync($"/api/documents/{id}/status", DeferralPayload("5"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync($"/api/documents/{id}/status", DeferralPayload("6"));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        Assert.Equal("تريث", await FinalStatusAsync(client, id));
    }

    [Fact]
    public async Task ParallelDeferrals_Never500_FinalStateConsistent_CurrentBehavior()
    {
        // السلوك الحالي (INT-012): بلا قفل متفائل — الفائز الأخير يكتب (أو يُرفَض الثاني إن تسلسلا).
        // بعد RF-010 يجب أن تُعكَس: المتعارض المتأخر 409 Conflict دائمًا.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token, borrowerName: "سباق متوازٍ");

        var both = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/documents/{id}/status", DeferralPayload("5")),
            client.PostAsJsonAsync($"/api/documents/{id}/status", DeferralPayload("6")));

        Assert.All(both, r => Assert.True(
            r.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
            $"unexpected {(int)r.StatusCode}"));

        var final = await FinalStatusAsync(client, id);
        Assert.True(final is "" or "تريث", $"inconsistent final state '{final}'");
    }
}
