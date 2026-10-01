using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبارات RF-009 (INT-001/INT-008 + ARC-001): التكرار مرفوض 409.
/// تفشل قبل الإصلاح (نجاح مكرر) وتخضر بعده.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class RF009NumberingTests
{
    private readonly ApiFactory _factory;

    public RF009NumberingTests(ApiFactory factory) => _factory = factory;

    private static object NumberedPayload(string borrower, string number) => new
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
    };

    [Fact]
    public async Task DuplicateNumber_SecondCreate_Conflict()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var number = $"7{Random.Shared.Next(100000, 999999)}";

        var first = await client.PostAsJsonAsync("/api/documents", NumberedPayload("أول فريد", number));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/documents", NumberedPayload("ثانٍ مكرر", number));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task RestoreConflictingNumber_Conflict()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var number = $"6{Random.Shared.Next(100000, 999999)}";

        var createA = await client.PostAsJsonAsync("/api/documents", NumberedPayload("سيُحذف", number));
        using var bodyA = await createA.Content.ReadFromJsonAsync<JsonDocument>();
        var doomed = bodyA!.RootElement.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/documents/{doomed}")).StatusCode);

        var createB = await client.PostAsJsonAsync("/api/documents", NumberedPayload("إعادة استعمال", number));
        Assert.Equal(HttpStatusCode.Created, createB.StatusCode);

        var restore = await client.PostAsync($"/api/documents/{doomed}/restore", null);
        Assert.Equal(HttpStatusCode.Conflict, restore.StatusCode);
    }
}
