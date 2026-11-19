using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Api.Tests.TestHelpers;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبارات RF-001/RF-009 لوحدانية رقم الأساس (INT-001) وتفاعلها مع
/// الحذف المنطقي/الاستعادة (INT-008). عُكست توقعاتها في RF-009: التكرار مرفوض 409.
/// </summary>
[Collection(PostgresApiTestCollection.Name)]
public class RF001NumberingCharacterizationTests : IAsyncLifetime
{
    private readonly PostgresApiFactory _factory;

    public RF001NumberingCharacterizationTests(PostgresApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private object NumberedPayload(string borrower, string number, int circuitId, string? warningToken = null) => new
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
        executionCircuitId = circuitId,
        warningToken,
    };

    private static async Task<int> CreateIdAsync(HttpClient client, object payload)
    {
        var response = await client.PostAsJsonAsync("/api/documents", payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return body!.RootElement.GetProperty("id").GetInt32();
    }

    private static int s_numberSeq;

    [PostgresFact]
    public async Task SameBasisNumber_TwoCreates_SecondConflict()
    {
        // RF-009: القيد الفريد الجزئي + الفحص الخدمي — الثاني 409 برسالة ودية.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.ClientWithToken(token);
        var number = $"9{System.Threading.Interlocked.Increment(ref s_numberSeq):D6}";
        var circuitId = await _factory.EnsureCircuitAsync("DAM", "دائرة تنفيذ دمشق");

        var first = await CreateIdAsync(client, NumberedPayload("تكرار أول", number, circuitId));

        var second = await client.PostAsJsonAsync("/api/documents", NumberedPayload("تكرار ثانٍ", number, circuitId));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/documents/{first}")).StatusCode);
    }

    [PostgresFact]
    public async Task RestoreAfterNumberReuse_ReturnsToPendingWithoutConflict()
    {
        // v2: الاستعادة = NeedsRegistration + رقم جديد؛ لا فحص تفرد لأن الرقم يُفرَّغ.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.ClientWithToken(token);
        var number = $"8{System.Threading.Interlocked.Increment(ref s_numberSeq):D6}";
        var circuitId = await _factory.EnsureCircuitAsync("DAM", "دائرة تنفيذ دمشق");

        var doomed = await CreateIdAsync(client, NumberedPayload("سيُحذف", number, circuitId));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/documents/{doomed}")).StatusCode);

        // الرقم يبقى معروضًا في سجل الأساس بعد الحذف، فإعادة استخدامه تتطلب تأكيدًا.
        var conflict = await client.PostAsJsonAsync(
            "/api/documents", NumberedPayload("إعادة استعمال", number, circuitId));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var conflictBody = await conflict.Content.ReadFromJsonAsync<JsonDocument>();
        var confirmToken = conflictBody!.RootElement.GetProperty("warningToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(confirmToken));

        var reused = await client.PostAsJsonAsync(
            "/api/documents", NumberedPayload("إعادة استعمال", number, circuitId, confirmToken));
        Assert.Equal(HttpStatusCode.Created, reused.StatusCode);

        var restore = await client.PostAsync($"/api/documents/{doomed}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);

        var restored = await client.GetFromJsonAsync<JsonElement>($"/api/documents/{doomed}");
        Assert.True(restored.GetProperty("needsRegistration").GetBoolean());
        Assert.True(string.IsNullOrEmpty(restored.GetProperty("fileNumber").GetString()));
    }
}
