using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Api.Tests.TestHelpers;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبارات RF-009 (INT-001/INT-008 + ARC-001): التكرار مرفوض 409.
/// تفشل قبل الإصلاح (نجاح مكرر) وتخضر بعده.
/// </summary>
[Collection(PostgresApiTestCollection.Name)]
public class RF009NumberingTests : IAsyncLifetime
{
    private readonly PostgresApiFactory _factory;

    public RF009NumberingTests(PostgresApiFactory factory) => _factory = factory;

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
        fileType = "عادي",
        fileYear = "2026",
        fileRegistrationDate = "1/8/2026",
        executionCircuitId = circuitId,
        warningToken,
    };

    private static int s_numberSeq;

    [PostgresFact]
    public async Task DuplicateNumber_SecondCreate_Conflict()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.ClientWithToken(token);
        var number = $"7{System.Threading.Interlocked.Increment(ref s_numberSeq):D6}";
        var circuitId = await _factory.EnsureCircuitAsync("DAM", "دائرة تنفيذ دمشق");

        var first = await client.PostAsJsonAsync("/api/documents", NumberedPayload("أول فريد", number, circuitId));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/documents", NumberedPayload("ثانٍ مكرر", number, circuitId));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [PostgresFact]
    public async Task RestoreConflictingNumber_ReturnsToPendingWithoutConflict()
    {
        // v2: الاستعادة تُفرِّغ الرقم وتعيد الملف إلى «بانتظار إعادة القيد» بلا فحص تفرد.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.ClientWithToken(token);
        var number = $"6{System.Threading.Interlocked.Increment(ref s_numberSeq):D6}";
        var circuitId = await _factory.EnsureCircuitAsync("DAM", "دائرة تنفيذ دمشق");

        var createA = await client.PostAsJsonAsync("/api/documents", NumberedPayload("سيُحذف", number, circuitId));
        using var bodyA = await createA.Content.ReadFromJsonAsync<JsonDocument>();
        var doomed = bodyA!.RootElement.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/documents/{doomed}")).StatusCode);

        var createB = await client.PostAsJsonAsync("/api/documents", NumberedPayload("إعادة استعمال", number, circuitId));
        // الرقم يبقى «معروضًا» بعد الحذف (سجل أساس)، فإعادة استخدامه تتطلب تأكيدًا
        // صريحًا: 409 مع `warningToken`، ثم الإنشاء بالتوكن يُنجح.
        Assert.Equal(HttpStatusCode.Conflict, createB.StatusCode);
        using var conflictBody = await createB.Content.ReadFromJsonAsync<JsonDocument>();
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
