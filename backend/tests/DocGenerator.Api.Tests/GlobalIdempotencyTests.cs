using System.Net;
using System.Text;
using System.Text.Json;
using DocGenerator.Api.Tests.TestHelpers;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// P0-04: إزالة التكرار العامة (middleware) تغطي عملية كتابة لم تكن محمية
/// بحارس خدمي — إنشاء فرع. تثبت إعادة الحالة الأصلية (201 + Location) والجسم
/// حرفيًا، وعدم التنفيذ مرة ثانية، و<b>عدم</b> تغيير عقد RF-011 القائم:
/// نفس المفتاح بجسم مختلف = نية جديدة تُنفَّذ طازجة.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class GlobalIdempotencyTests
{
    private readonly ApiFactory _factory;

    public GlobalIdempotencyTests(ApiFactory factory) => _factory = factory;

    private static string BranchPayloadJson(string name, string code) =>
        JsonSerializer.Serialize(new
        {
            name,
            code,
            address = (string?)null,
            phone = (string?)null,
            governorate = "دمشق",
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static async Task<HttpResponseMessage> PostJsonAsync(
        HttpClient client, string url, string json, string? key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        if (key is not null)
            request.Headers.Add("X-Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task DoubleCreate_SameKey_Replays201BodyAndLocation_WithoutSecondRow()
    {
        var client = _factory.AuthorizedClient("admin");
        var key = Guid.NewGuid().ToString();
        var name = $"فرع تكرار {Guid.NewGuid():N}"[..24];
        var code = $"ID_{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var json = BranchPayloadJson(name, code);

        var first = await PostJsonAsync(client, "/api/branches", json, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstLocation = first.Headers.Location?.ToString();
        Assert.False(string.IsNullOrWhiteSpace(firstLocation));
        var firstBody = await first.Content.ReadAsStringAsync();

        var second = await PostJsonAsync(client, "/api/branches", json, key);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(firstLocation, second.Headers.Location?.ToString());
        Assert.Equal(firstBody, await second.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        Assert.Equal(1, await db.Branches.CountAsync(b => b.Code == code));
    }

    [Fact]
    public async Task SameKey_DifferentBody_ExecutesFresh_PreservingRf011Contract()
    {
        var client = _factory.AuthorizedClient("admin");
        var key = Guid.NewGuid().ToString();
        var code1 = $"ID_{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var code2 = $"ID_{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        var first = await PostJsonAsync(
            client, "/api/branches", BranchPayloadJson($"فرع أول {Guid.NewGuid():N}"[..24], code1), key);
        var second = await PostJsonAsync(
            client, "/api/branches", BranchPayloadJson($"فرع ثانٍ {Guid.NewGuid():N}"[..24], code2), key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        Assert.Equal(1, await db.Branches.CountAsync(b => b.Code == code1));
        Assert.Equal(1, await db.Branches.CountAsync(b => b.Code == code2));
    }

    [Fact]
    public async Task PendingReservation_SameFingerprint_ReturnsConflict()
    {
        var adminId = await _factory.UserIdAsync("admin");
        var key = Guid.NewGuid().ToString();
        var json = BranchPayloadJson($"فرع انشغال {Guid.NewGuid():N}"[..24], $"ID_{Guid.NewGuid():N}"[..12].ToUpperInvariant());
        var fingerprint = IdempotencyGuard.FingerprintRaw(Encoding.UTF8.GetBytes(json));

        using (var scope = _factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
            var begin = await store.BeginAsync(
                key, "POST api/branches", adminId, fingerprint, DateTime.UtcNow.AddMinutes(5));
            Assert.NotNull(begin.Ticket);
        }

        var response = await PostJsonAsync(_factory.AuthorizedClient("admin"), "/api/branches", json, key);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ConcurrentSameKey_ExecutesAtMostOnce()
    {
        var client = _factory.AuthorizedClient("admin");
        var key = Guid.NewGuid().ToString();
        var code = $"ID_{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var json = BranchPayloadJson($"فرع تزامن {Guid.NewGuid():N}"[..24], code);

        var responses = await Task.WhenAll(
            PostJsonAsync(client, "/api/branches", json, key),
            PostJsonAsync(client, "/api/branches", json, key));

        Assert.All(responses, r =>
            Assert.True(
                r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
                $"unexpected status {(int)r.StatusCode}"));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        Assert.Equal(1, await db.Branches.CountAsync(b => b.Code == code));
    }

    [Fact]
    public async Task InvalidKey_ReturnsBadRequest_AndDoesNotCreate()
    {
        var code = $"ID_{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var response = await PostJsonAsync(
            _factory.AuthorizedClient("admin"),
            "/api/branches",
            BranchPayloadJson($"فرع مفتاح سيئ {Guid.NewGuid():N}"[..24], code),
            "bad key / with spaces");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        Assert.Equal(0, await db.Branches.CountAsync(b => b.Code == code));
    }
}
