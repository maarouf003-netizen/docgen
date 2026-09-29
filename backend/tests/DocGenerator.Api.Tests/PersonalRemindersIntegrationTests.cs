using System.Net;
using System.Net.Http.Json;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

[Collection(ApiTestCollection.Name)]
public class PersonalRemindersIntegrationTests
{
    private readonly ApiFactory _factory;

    public PersonalRemindersIntegrationTests(ApiFactory factory) => _factory = factory;

    private static string NewName(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 16, 40)];

    private Task<int> BranchIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return Task.FromResult(db.Branches.Single(b => b.Code == code).Id);
    }

    private static object ValidBody(string title = "مراجعة الملف") => new
    {
        title,
        notes = "ملاحظة",
        dueDate = "2026-08-08",
        color = "أحمر",
        recurrence = "مرة واحدة",
        recurrenceEnd = (string?)null,
    };

    [Fact]
    public async Task NonLawyerRoles_Forbidden()
    {
        foreach (var username in new[] { "manager", "admin", "head1" })
        {
            var client = _factory.AuthorizedClient(username);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/personal-reminders")).StatusCode);
            var post = await client.PostAsJsonAsync("/api/personal-reminders", ValidBody());
            Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
        }
    }

    [Fact]
    public async Task Lawyer_CreatesListsUpdatesAndDeletes()
    {
        var lawyer = _factory.AuthorizedClient("lawyer1");

        var created = await lawyer.PostAsJsonAsync("/api/personal-reminders", ValidBody());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dto = await created.Content.ReadFromJsonAsync<PersonalReminderDto>();
        Assert.NotNull(dto);
        Assert.Equal("2026-08-08", dto.DueDate);

        var list = await (await lawyer.GetAsync("/api/personal-reminders")).Content
            .ReadFromJsonAsync<List<PersonalReminderDto>>();
        Assert.NotNull(list);
        Assert.Contains(list, r => r.Id == dto.Id);

        var updated = await lawyer.PutAsJsonAsync($"/api/personal-reminders/{dto.Id}", new
        {
            title = "معدل",
            notes = (string?)null,
            dueDate = (string?)null,
            color = (string?)null,
            recurrence = (string?)null,
            recurrenceEnd = (string?)null,
            isArchived = true,
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var archived = await (await lawyer.GetAsync("/api/personal-reminders")).Content
            .ReadFromJsonAsync<List<PersonalReminderDto>>();
        Assert.NotNull(archived);
        Assert.DoesNotContain(archived, r => r.Id == dto.Id);

        var withArchived = await (await lawyer.GetAsync("/api/personal-reminders?includeArchived=true")).Content
            .ReadFromJsonAsync<List<PersonalReminderDto>>();
        Assert.NotNull(withArchived);
        Assert.Contains(withArchived, r => r.Id == dto.Id && r.IsArchived);

        var deleted = await lawyer.DeleteAsync($"/api/personal-reminders/{dto.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await lawyer.GetAsync($"/api/personal-reminders/{dto.Id}")).StatusCode);
    }

    [Fact]
    public async Task Lawyer_GetById_ReturnsOwnSingle()
    {
        var lawyer = _factory.AuthorizedClient("lawyer1");

        var created = await lawyer.PostAsJsonAsync("/api/personal-reminders", ValidBody("تذكير مفرد"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dto = await created.Content.ReadFromJsonAsync<PersonalReminderDto>();
        Assert.NotNull(dto);

        var response = await lawyer.GetAsync($"/api/personal-reminders/{dto.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var single = await response.Content.ReadFromJsonAsync<PersonalReminderDto>();
        Assert.NotNull(single);
        Assert.Equal(dto.Id, single.Id);
        Assert.Equal("تذكير مفرد", single.Title);
    }

    [Fact]
    public async Task Lawyer_CreateInvalid_BadRequest()
    {
        var lawyer = _factory.AuthorizedClient("lawyer1");

        var emptyTitle = await lawyer.PostAsJsonAsync("/api/personal-reminders", ValidBody("  "));
        Assert.Equal(HttpStatusCode.BadRequest, emptyTitle.StatusCode);

        var badDate = await lawyer.PostAsJsonAsync("/api/personal-reminders", new
        {
            title = "x",
            notes = (string?)null,
            dueDate = "تاريخ-فاسد",
            color = (string?)null,
            recurrence = "مرة واحدة",
            recurrenceEnd = (string?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, badDate.StatusCode);
    }

    [Fact]
    public async Task Lawyer_CannotTouchAnotherLawyerReminder()
    {
        var damId = await BranchIdAsync("DAM");
        var other = await _factory.CreateUserAsync(NewName("prem_other"), UserRole.Lawyer, damId);
        var otherClient = _factory.AuthorizedClient(other.Username);
        var lawyer = _factory.AuthorizedClient("lawyer1");

        var created = await lawyer.PostAsJsonAsync("/api/personal-reminders", ValidBody());
        var dto = await created.Content.ReadFromJsonAsync<PersonalReminderDto>();
        Assert.NotNull(dto);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await otherClient.GetAsync($"/api/personal-reminders/{dto.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await otherClient.DeleteAsync($"/api/personal-reminders/{dto.Id}")).StatusCode);
    }

    [Fact]
    public async Task Lawyer_TogglesOccurrenceDone()
    {
        var lawyer = _factory.AuthorizedClient("lawyer1");
        var created = await lawyer.PostAsJsonAsync("/api/personal-reminders", new
        {
            title = "يومي",
            notes = (string?)null,
            dueDate = "2026-08-08",
            color = (string?)null,
            recurrence = "يومي",
            recurrenceEnd = (string?)null,
        });
        var dto = await created.Content.ReadFromJsonAsync<PersonalReminderDto>();
        Assert.NotNull(dto);

        var done = await lawyer.PatchAsJsonAsync(
            $"/api/personal-reminders/{dto.Id}/occurrences",
            new { occurrenceDate = "2026-08-09", done = true });
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        var doneDto = await done.Content.ReadFromJsonAsync<PersonalReminderDto>();
        Assert.NotNull(doneDto);
        Assert.Contains("2026-08-09", doneDto.CompletedOccurrenceKeys);

        var badKey = await lawyer.PatchAsJsonAsync(
            $"/api/personal-reminders/{dto.Id}/occurrences",
            new { occurrenceDate = "تاريخ-فاسد", done = true });
        Assert.Equal(HttpStatusCode.BadRequest, badKey.StatusCode);
    }
}
