using System.Net;
using System.Net.Http.Json;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Api.Tests;

/// <summary>
/// عقد فصل تاريخ التذكير عن تاريخ الإجراء (الخطة: `docs/ACTION_REMINDER_DATE_SEPARATION_PLAN.md`):
/// نقل التذكير يحرّك `ReminderBaseDate` وحده، وتاريخ الإجراء يبقى كما أُدخل حرفيًا، وتعديل الإجراء
/// لا يكتب أي حالة تذكير (التذكير يُدار من مساره المستقل).
/// </summary>
[Collection(ApiTestCollection.Name)]
public class ActionReminderSeparationIntegrationTests
{
    private readonly ApiFactory _factory;

    public ActionReminderSeparationIntegrationTests(ApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, int DocumentId, string Token)> CreateDocAsync()
    {
        var login = await _factory.LoginAsync("lawyer1", "123456");
        var token = login!.Token!;
        var documentId = await _factory.CreateDocumentAsync(token, registered: true);
        return (_factory.ClientWithToken(token), documentId, token);
    }

    private static async Task<ExecutionActionDto> AddActionAsync(
        HttpClient client, int documentId, string actionDate = "10-11-2026", string? duration = "3 أيام")
    {
        var response = await client.PostAsJsonAsync($"/api/documents/{documentId}/actions", new
        {
            type = "action",
            text = "طلب تنفيذي",
            actionDate,
            reminderDuration = duration,
            reminderColor = duration is null ? null : "أحمر",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ExecutionActionDto>();
        Assert.NotNull(dto);
        return dto!;
    }

    [Fact]
    public async Task Reschedule_KeepsActionDateVerbatim_AndMovesReminderDueDate()
    {
        var (client, documentId, _) = await CreateDocAsync();
        var action = await AddActionAsync(client, documentId);

        var rescheduled = await client.PostAsJsonAsync(
            $"/api/documents/{documentId}/actions/{action.Id}/reschedule",
            new { newDueDate = "14-11-2026" });
        Assert.Equal(HttpStatusCode.OK, rescheduled.StatusCode);

        // تاريخ الإجراء كما أُدخل بالحرف — لا قيمةً ولا صيغةً.
        var actions = await (await client.GetAsync($"/api/documents/{documentId}/actions"))
            .Content.ReadFromJsonAsync<List<ExecutionActionDto>>();
        var stored = Assert.Single(actions!, a => a.Id == action.Id);
        Assert.Equal("10-11-2026", stored.ActionDate);

        // التذكير هو المتحرك: 14-11-2026.
        var reminders = await (await client.GetAsync("/api/reminders"))
            .Content.ReadFromJsonAsync<List<ReminderDto>>();
        var reminder = Assert.Single(reminders!, r => r.ActionId == action.Id);
        Assert.Equal(new DateTime(2026, 11, 14), reminder.DueDate.Date);
        Assert.False(reminder.DueDateSuspect);
    }

    [Fact]
    public async Task UpdateAction_DoesNotWriteReminder_AndDedicatedEndpointDoes()
    {
        var (client, documentId, _) = await CreateDocAsync();
        var action = await AddActionAsync(client, documentId);

        // تعديل الإجراء: حقل التذكير في الجسم يُهمَل (العقد لا يحمله) — لا تغيير في التذكير.
        var updated = await client.PutAsJsonAsync($"/api/documents/{documentId}/actions/{action.Id}", new
        {
            type = "action",
            text = "نص مُحدَّث",
            actionDate = "11-11-2026",
            reminderDuration = "شهر",
            reminderColor = "أصفر",
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var afterUpdate = await updated.Content.ReadFromJsonAsync<ExecutionActionDto>();
        Assert.Equal("نص مُحدَّث", afterUpdate!.Text);
        Assert.Equal("11-11-2026", afterUpdate.ActionDate);
        Assert.Equal("3 أيام", afterUpdate.ReminderDuration);
        Assert.Equal("أحمر", afterUpdate.ReminderColor);

        // المسار المستقل يعدّل التذكير بلا مساس بتاريخ الإجراء.
        var remind = await client.PutAsJsonAsync(
            $"/api/documents/{documentId}/actions/{action.Id}/reminder",
            new { reminderDuration = "شهر", reminderColor = "أصفر" });
        Assert.Equal(HttpStatusCode.OK, remind.StatusCode);
        var reminded = await remind.Content.ReadFromJsonAsync<ExecutionActionDto>();
        Assert.Equal("شهر", reminded!.ReminderDuration);
        Assert.Equal("أصفر", reminded.ReminderColor);
        Assert.Equal("11-11-2026", reminded.ActionDate);

        // الإلغاء يُصفّر الحقول الثلاثة ويُخفي التذكير من البطاقة.
        var cleared = await client.DeleteAsync($"/api/documents/{documentId}/actions/{action.Id}/reminder");
        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        var reminders = await (await client.GetAsync("/api/reminders"))
            .Content.ReadFromJsonAsync<List<ReminderDto>>();
        Assert.DoesNotContain(reminders!, r => r.ActionId == action.Id);
    }

    [Fact]
    public async Task ReminderEndpoints_NonLawyer_Forbidden()
    {
        var (client, documentId, _) = await CreateDocAsync();
        var action = await AddActionAsync(client, documentId);

        foreach (var username in new[] { "head1", "manager" })
        {
            var other = _factory.AuthorizedClient(username);
            var put = await other.PutAsJsonAsync(
                $"/api/documents/{documentId}/actions/{action.Id}/reminder",
                new { reminderDuration = "أسبوع", reminderColor = "أحمر" });
            Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);

            var delete = await other.DeleteAsync($"/api/documents/{documentId}/actions/{action.Id}/reminder");
            Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);

            var reschedule = await other.PostAsJsonAsync(
                $"/api/documents/{documentId}/actions/{action.Id}/reschedule",
                new { newDueDate = "20-11-2026" });
            Assert.Equal(HttpStatusCode.Forbidden, reschedule.StatusCode);
        }
    }

    [Fact]
    public async Task SetReminder_UnknownAction_NotFound()
    {
        var (client, documentId, _) = await CreateDocAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/documents/{documentId}/actions/999999/reminder",
            new { reminderDuration = "أسبوع", reminderColor = "أحمر" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
