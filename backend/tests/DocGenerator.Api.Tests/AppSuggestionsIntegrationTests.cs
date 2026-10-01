using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

[Collection(ApiTestCollection.Name)]
public class AppSuggestionsIntegrationTests
{
    private readonly ApiFactory _factory;

    public AppSuggestionsIntegrationTests(ApiFactory factory) => _factory = factory;

    private static string NewName(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 16, 40)];

    private Task<int> BranchIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return Task.FromResult(db.Branches.Single(b => b.Code == code).Id);
    }

    [Fact]
    public async Task Lawyer_CreatesAndSeesOwnHistory()
    {
        var lawyer = _factory.AuthorizedClient("lawyer1");

        var created = await lawyer.PostAsJsonAsync("/api/app-suggestions", new { message = "أضيفوا وضعًا ليليًا" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dto = await created.Content.ReadFromJsonAsync<AppSuggestionDto>();
        Assert.NotNull(dto);
        Assert.EndsWith($"/api/app-suggestions/{dto.Id}", created.Headers.Location?.ToString());
        Assert.False(dto.IsRead);

        var mine = await (await lawyer.GetAsync("/api/app-suggestions")).Content
            .ReadFromJsonAsync<List<AppSuggestionDto>>();
        Assert.NotNull(mine);
        Assert.Contains(mine, s => s.Id == dto.Id && s.Message == "أضيفوا وضعًا ليليًا");
    }

    [Fact]
    public async Task GetById_OwnerAdminMissingAndForeignViews()
    {
        var lawyer = _factory.AuthorizedClient("lawyer1");
        var created = await lawyer.PostAsJsonAsync("/api/app-suggestions", new { message = "اقتراح مفرد" });
        var dto = await created.Content.ReadFromJsonAsync<AppSuggestionDto>();
        Assert.NotNull(dto);

        var own = await lawyer.GetAsync($"/api/app-suggestions/{dto.Id}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        var ownDto = await own.Content.ReadFromJsonAsync<AppSuggestionDto>();
        Assert.NotNull(ownDto);
        Assert.Equal(dto.Id, ownDto.Id);
        Assert.Null(ownDto.SenderName);

        Assert.Equal(HttpStatusCode.NotFound, (await lawyer.GetAsync("/api/app-suggestions/999999")).StatusCode);

        var damId = await BranchIdAsync("DAM");
        var other = await _factory.CreateUserAsync(NewName("sugg_other"), UserRole.Lawyer, damId);
        var otherClient = _factory.AuthorizedClient(other.Username);
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync($"/api/app-suggestions/{dto.Id}")).StatusCode);

        var admin = _factory.AuthorizedClient("admin");
        var adminView = await admin.GetAsync($"/api/app-suggestions/{dto.Id}");
        Assert.Equal(HttpStatusCode.OK, adminView.StatusCode);
        var adminDto = await adminView.Content.ReadFromJsonAsync<AppSuggestionDto>();
        Assert.NotNull(adminDto);
        Assert.False(string.IsNullOrEmpty(adminDto.SenderName));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/app-suggestions/999999")).StatusCode);
    }

    [Fact]
    public async Task Lawyer_CreateInvalid_BadRequest()
    {
        var lawyer = _factory.AuthorizedClient("lawyer1");

        var empty = await lawyer.PostAsJsonAsync("/api/app-suggestions", new { message = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var tooLong = await lawyer.PostAsJsonAsync(
            "/api/app-suggestions", new { message = new string('ع', 2001) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task NonLawyerNonHead_CannotCreate()
    {
        foreach (var username in new[] { "manager", "admin" })
        {
            var client = _factory.AuthorizedClient(username);
            var response = await client.PostAsJsonAsync("/api/app-suggestions", new { message = "x" });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // الدور الخامس (مندوب الجهة) محظور أيضًا — أي توسيع لاحق لـ `CanSuggestApp`
        // دون تحديث هذا الاختبار سيفشل هنا عمدًا.
        var entityManager = await _factory.CreateUserAsync(NewName("sugg_delegate"), UserRole.EntityManager);
        var delegateClient = _factory.AuthorizedClient(entityManager.Username);
        var delegateResponse = await delegateClient.PostAsJsonAsync("/api/app-suggestions", new { message = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, delegateResponse.StatusCode);
    }

    [Fact]
    public async Task Head_CreatesAndSeesOwnHistory()
    {
        var lawyer = _factory.AuthorizedClient("lawyer1");
        var lawyerCreated = await lawyer.PostAsJsonAsync("/api/app-suggestions", new { message = "اقتراح محامٍ" });
        var lawyerDto = await lawyerCreated.Content.ReadFromJsonAsync<AppSuggestionDto>();
        Assert.NotNull(lawyerDto);

        var head = _factory.AuthorizedClient("head1");
        var created = await head.PostAsJsonAsync("/api/app-suggestions", new { message = "اقتراح رئيس القسم" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dto = await created.Content.ReadFromJsonAsync<AppSuggestionDto>();
        Assert.NotNull(dto);

        var mine = await (await head.GetAsync("/api/app-suggestions")).Content
            .ReadFromJsonAsync<List<AppSuggestionDto>>();
        Assert.NotNull(mine);
        Assert.Contains(mine, s => s.Id == dto.Id);
        // عزل الملكية: سجل الرئيس لا يكشف اقتراحات غيره، والقراءة المباشرة 404،
        // وتعليم المقروء محظور (المشرف فقط).
        Assert.DoesNotContain(mine, s => s.Id == lawyerDto.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await head.GetAsync($"/api/app-suggestions/{lawyerDto.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await head.PatchAsync($"/api/app-suggestions/{dto.Id}/read", null)).StatusCode);
    }

    [Fact]
    public async Task Admin_SeesAllAndMarksRead_LawyerSeesReadState()
    {
        var lawyer = _factory.AuthorizedClient("lawyer1");
        var created = await lawyer.PostAsJsonAsync("/api/app-suggestions", new { message = "اقتراح للقراءة" });
        var dto = await created.Content.ReadFromJsonAsync<AppSuggestionDto>();
        Assert.NotNull(dto);

        var admin = _factory.AuthorizedClient("admin");
        using var body = await (await admin.GetAsync("/api/app-suggestions")).Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(body);
        Assert.True(body!.RootElement.GetProperty("totalCount").GetInt32() >= 1);
        var entry = body.RootElement.GetProperty("items").EnumerateArray().First();
        Assert.Equal(dto.Id, entry.GetProperty("id").GetInt32());
        Assert.False(entry.GetProperty("isRead").GetBoolean());
        Assert.False(string.IsNullOrEmpty(entry.GetProperty("senderName").GetString()));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PatchAsync($"/api/app-suggestions/{dto.Id}/read", null)).StatusCode);

        var mine = await (await lawyer.GetAsync("/api/app-suggestions")).Content
            .ReadFromJsonAsync<List<AppSuggestionDto>>();
        Assert.NotNull(mine);
        Assert.Contains(mine, s => s.Id == dto.Id && s.IsRead);
    }

    [Fact]
    public async Task Admin_ListIsPaged()
    {
        var lawyer = _factory.AuthorizedClient("lawyer1");
        foreach (var text in new[] { "أول", "ثانٍ", "ثالث" })
            await lawyer.PostAsJsonAsync("/api/app-suggestions", new { message = text });

        var admin = _factory.AuthorizedClient("admin");
        using var first = await (await admin.GetAsync("/api/app-suggestions?page=1&perPage=2")).Content
            .ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(first);
        Assert.Equal(2, first!.RootElement.GetProperty("items").EnumerateArray().Count());
        Assert.True(first.RootElement.GetProperty("totalCount").GetInt32() >= 3);
        Assert.Equal(1, first.RootElement.GetProperty("page").GetInt32());

        using var second = await (await admin.GetAsync("/api/app-suggestions?page=2&perPage=2")).Content
            .ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(second);
        Assert.Equal(2, second!.RootElement.GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task NonAdmin_CannotMarkRead()
    {
        var lawyer = _factory.AuthorizedClient("lawyer1");
        var created = await lawyer.PostAsJsonAsync("/api/app-suggestions", new { message = "y" });
        var dto = await created.Content.ReadFromJsonAsync<AppSuggestionDto>();
        Assert.NotNull(dto);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await lawyer.PatchAsync($"/api/app-suggestions/{dto.Id}/read", null)).StatusCode);
    }
}
