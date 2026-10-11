using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبارات تكامل HTTP للمنتدى: المصادقة/البوابة (401/403) + العقد
/// (الأشكال والحالات) + تسلسل الزمن بـ `Z` — عبر المصنع المشترك.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class ForumApiTests
{
    private readonly ApiFactory _factory;

    public ForumApiTests(ApiFactory factory) => _factory = factory;

    private static string Unique(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..20];

    private int BranchId(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return db.Branches.Single(b => b.Code == code).Id;
    }

    private async Task<User> NewUserAsync(string prefix, UserRole role, string? branchCode = "DAM")
        => await _factory.CreateUserAsync(Unique(prefix), role,
            branchCode is null ? null : BranchId(branchCode));

    private HttpClient ClientFor(User user) => _factory.ClientForUser(user);

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    [Fact]
    public async Task AnonymousList_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/forum/messages");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DelegateList_Returns403()
    {
        var gate = await NewUserAsync("fdel", UserRole.EntityManager, null);
        var response = await ClientFor(gate).GetAsync("/api/forum/messages");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LawyerPost_ThenListContainsIt_WithUtcTimestamp()
    {
        var lawyer = await NewUserAsync("flaw", UserRole.Lawyer);
        var client = ClientFor(lawyer);

        var post = await client.PostAsJsonAsync("/api/forum/messages", new { body = "تحية المنتدى" });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        using var created = await ReadJsonAsync(post);
        Assert.Equal("تحية المنتدى", created.RootElement.GetProperty("body").GetString());
        // تسلسل الزمن UTC بلاحقة `Z` (درس إصلاح المنطقة الزمنية).
        Assert.EndsWith("Z", created.RootElement.GetProperty("createdAt").GetString());

        var list = await client.GetAsync("/api/forum/messages?limit=30");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var page = await ReadJsonAsync(list);
        Assert.True(page.RootElement.TryGetProperty("items", out _));
        Assert.True(page.RootElement.TryGetProperty("hasOlder", out _));
        Assert.Contains("تحية المنتدى", await list.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PostEmptyBody_Returns400WithArabicMessage()
    {
        var lawyer = await NewUserAsync("flaw", UserRole.Lawyer);
        var response = await ClientFor(lawyer).PostAsJsonAsync("/api/forum/messages", new { body = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("نص الرسالة مطلوب", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PostMissingQuote_Returns400()
    {
        var lawyer = await NewUserAsync("flaw", UserRole.Lawyer);
        var response = await ClientFor(lawyer).PostAsJsonAsync(
            "/api/forum/messages", new { body = "رد", quotedMessageId = 987654 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Readers_OwnerSees_OthersForbidden()
    {
        var author = await NewUserAsync("flaw", UserRole.Lawyer);
        var other = await NewUserAsync("flaw", UserRole.Lawyer);
        var authorClient = ClientFor(author);
        var otherClient = ClientFor(other);
        var post = await authorClient.PostAsJsonAsync("/api/forum/messages", new { body = "اقرأني" });
        var id = (await ReadJsonAsync(post)).RootElement.GetProperty("id").GetInt32();
        var marked = await otherClient.PostAsJsonAsync("/api/forum/read", new { upToMessageId = id });
        Assert.Equal(HttpStatusCode.OK, marked.StatusCode);

        var own = await authorClient.GetAsync($"/api/forum/messages/{id}/readers");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using var readers = await ReadJsonAsync(own);
        var first = Assert.Single(readers.RootElement.EnumerateArray());
        Assert.True(first.TryGetProperty("userId", out _));
        Assert.True(first.TryGetProperty("userName", out _));
        Assert.True(first.TryGetProperty("readAtUtc", out _));

        var foreign = await otherClient.GetAsync($"/api/forum/messages/{id}/readers");
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
    }

    [Fact]
    public async Task Pin_LawyerForbidden_AdminSucceeds()
    {
        var lawyer = await NewUserAsync("flaw", UserRole.Lawyer);
        var admin = await NewUserAsync("fadm", UserRole.Admin, null);
        var post = await ClientFor(lawyer).PostAsJsonAsync("/api/forum/messages", new { body = "ثبّتني" });
        var id = (await ReadJsonAsync(post)).RootElement.GetProperty("id").GetInt32();

        var lawyerPin = await ClientFor(lawyer).PostAsync($"/api/forum/messages/{id}/pin", null);
        Assert.Equal(HttpStatusCode.Forbidden, lawyerPin.StatusCode);

        var adminPin = await ClientFor(admin).PostAsync($"/api/forum/messages/{id}/pin", null);
        Assert.Equal(HttpStatusCode.OK, adminPin.StatusCode);
        using var pinnedDto = await ReadJsonAsync(adminPin);
        Assert.True(pinnedDto.RootElement.GetProperty("isPinned").GetBoolean());

        var pinned = await ClientFor(lawyer).GetAsync("/api/forum/pinned");
        Assert.Equal(HttpStatusCode.OK, pinned.StatusCode);
        Assert.Contains("ثبّتني", await pinned.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UpdateAndDelete_OthersForbidden()
    {
        var author = await NewUserAsync("flaw", UserRole.Lawyer);
        var other = await NewUserAsync("flaw", UserRole.Lawyer);
        var post = await ClientFor(author).PostAsJsonAsync("/api/forum/messages", new { body = "لي" });
        var id = (await ReadJsonAsync(post)).RootElement.GetProperty("id").GetInt32();
        var otherClient = ClientFor(other);

        var update = await otherClient.PutAsJsonAsync($"/api/forum/messages/{id}", new { body = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);

        var delete = await otherClient.DeleteAsync($"/api/forum/messages/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task UnreadCount_ShapeAndDecrement()
    {
        var author = await NewUserAsync("flaw", UserRole.Lawyer);
        var reader = await NewUserAsync("flaw", UserRole.Lawyer);
        await ClientFor(author).PostAsJsonAsync("/api/forum/messages", new { body = "جديدة" });
        var readerClient = ClientFor(reader);

        using var before = await ReadJsonAsync(await readerClient.GetAsync("/api/forum/unread-count"));
        // قاعدة المصنع مشتركة بين الاختبارات — المهم وجود غير المقروء لا عدده المضبوط.
        Assert.True(before.RootElement.GetProperty("count").GetInt32() >= 1);

        var list = await ReadJsonAsync(await readerClient.GetAsync("/api/forum/messages?limit=30"));
        var items = list.RootElement.GetProperty("items");
        // الشريحة تصاعدية — الأحدث هو الأخير.
        var latestId = items[items.GetArrayLength() - 1].GetProperty("id").GetInt32();
        await readerClient.PostAsJsonAsync("/api/forum/read", new { upToMessageId = latestId });

        using var after = await ReadJsonAsync(await readerClient.GetAsync("/api/forum/unread-count"));
        Assert.Equal(0, after.RootElement.GetProperty("count").GetInt32());
    }
}
