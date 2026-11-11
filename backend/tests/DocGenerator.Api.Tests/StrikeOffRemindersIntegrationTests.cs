using System.Net;
using System.Net.Http.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// عقد «تذكيرات الشطب» (الخطة `docs/STRIKE_OFF_REMINDERS_PLAN.md`): تُحسب عند القراءة من آخر
/// إجراء مسجّل (الملاحظات لا تُحتسب) أو تاريخ القيد أو تاريخ الإدخال، بلا كتابة، والإلغاء
/// هو الكتابة الوحيدة (للمالك وحده). النطاق: محامٍ واحد — لا فوقي ولا زميل.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class StrikeOffRemindersIntegrationTests
{
    private readonly ApiFactory _factory;

    public StrikeOffRemindersIntegrationTests(ApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, string Token)> LawyerAsync()
    {
        var login = await _factory.LoginAsync("lawyer1", "123456");
        return (_factory.ClientWithToken(login!.Token!), login.Token!);
    }

    private static async Task<int> AddActionAsync(
        HttpClient client, int documentId, string actionDate, string type = "action")
    {
        var response = await client.PostAsJsonAsync($"/api/documents/{documentId}/actions", new
        {
            type,
            text = "إجراء اختبار",
            actionDate,
            reminderDuration = (string?)null,
            reminderColor = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ExecutionActionDto>();
        return dto!.Id;
    }

    private static async Task<PagedResult<DocumentResponse>> ListAsync(HttpClient client, int perPage = 100)
    {
        var page = await (await client.GetAsync($"/api/documents/strike-off-reminders?page=1&perPage={perPage}"))
            .Content.ReadFromJsonAsync<PagedResult<DocumentResponse>>();
        Assert.NotNull(page);
        return page!;
    }

    [Fact]
    public async Task OldAction_MakesFileDue_AndNewActionRemovesIt()
    {
        var (client, token) = await LawyerAsync();
        var id = await _factory.CreateDocumentAsync(token, "مقاربة الشطب", registered: true);
        await AddActionAsync(client, id, "1/5/2026");

        var due = await ListAsync(client);
        Assert.Contains(due.Items, d => d.Id == id);

        // إجراء حديث يرفع آخر نشاط ⇒ يخرج من القائمة فورًا (بلا كتابة حالة).
        await AddActionAsync(client, id, "1/11/2026");
        var after = await ListAsync(client);
        Assert.DoesNotContain(after.Items, d => d.Id == id);
    }

    [Fact]
    public async Task NotesAreNotCounted_FileWithOldNoteIsNotDue()
    {
        var (client, token) = await LawyerAsync();
        var id = await _factory.CreateDocumentAsync(token, "ملاحظة قديمة", registered: true);
        // ملاحظة بتاريخ قديم: لا تُحتسب ⇒ آخر نشاط = تاريخ القيد (1/8/2026) ⇒ غير مستحق.
        await AddActionAsync(client, id, "1/1/2026", type: "note");

        var due = await ListAsync(client);
        Assert.DoesNotContain(due.Items, d => d.Id == id);
    }

    [Fact]
    public async Task Dismiss_HidesFile_AndCountMatchesList()
    {
        var (client, token) = await LawyerAsync();
        var id = await _factory.CreateDocumentAsync(token, "ملف يُلغى تذكيره", registered: true);
        await AddActionAsync(client, id, "1/5/2026");

        var before = await ListAsync(client);
        Assert.Contains(before.Items, d => d.Id == id);

        var dismiss = await client.PostAsync($"/api/documents/{id}/strike-off-reminder/dismiss", null);
        Assert.Equal(HttpStatusCode.NoContent, dismiss.StatusCode);

        var after = await ListAsync(client);
        Assert.DoesNotContain(after.Items, d => d.Id == id);

        var count = await (await client.GetAsync("/api/documents/strike-off-reminders/count"))
            .Content.ReadFromJsonAsync<CountDto>();
        Assert.NotNull(count);
        Assert.Equal(after.TotalCount, count!.Count);
    }

    private sealed record CountDto(int Count);

    /// <summary>ملف قديم الإدخال بلا قيد ولا إجراءات: مصدر آخر نشاط = تاريخ الإدخال.</summary>
    [Fact]
    public async Task NoActionsAndNoRegistration_FallsBackToCreatedAt()
    {
        var (client, _) = await LawyerAsync();
        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var lawyer = db.Users.Single(u => u.Username == "lawyer1");
            var doc = new Document
            {
                CreatedById = lawyer.Id,
                BranchId = lawyer.BranchId,
                GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
                IsDraft = false,
                ExecStatus = ExecutionStatusCatalog.None,
                FileNumber = $"S{Guid.NewGuid():N}"[..8],
                FileYear = "2025",
                BorrowerName = "قديم",
                BorrowerFather = "بلا",
                BorrowerFamily = "قيد",
                // تاريخ إدخال قديم عمدًا (UTC) — قبل الحد بخمسة أشهر.
                CreatedAt = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc),
            };
            db.Documents.Add(doc);
            await db.SaveChangesAsync();
            id = doc.Id;
        }

        var due = await ListAsync(client);
        var row = Assert.Single(due.Items, d => d.Id == id);
        Assert.Equal("created", row.LastActivitySource);
    }

    [Fact]
    public async Task OldestIdle_First()
    {
        var (client, token) = await LawyerAsync();
        var older = await _factory.CreateDocumentAsync(token, "الأقدم ركودًا", registered: true);
        var newer = await _factory.CreateDocumentAsync(token, "الأحدث ركودًا", registered: true);
        await AddActionAsync(client, newer, "1/6/2026");
        await AddActionAsync(client, older, "1/3/2026");

        var due = await ListAsync(client);
        var olderIndex = due.Items.FindIndex(d => d.Id == older);
        var newerIndex = due.Items.FindIndex(d => d.Id == newer);
        Assert.True(olderIndex >= 0 && newerIndex >= 0);
        Assert.True(olderIndex < newerIndex);
    }

    [Fact]
    public async Task NonLawyerRoles_Forbidden_AndOtherLawyer_CannotDismiss()
    {
        var (client, token) = await LawyerAsync();
        var id = await _factory.CreateDocumentAsync(token, "ملف محمي", registered: true);
        await AddActionAsync(client, id, "1/5/2026");

        foreach (var username in new[] { "head1", "manager" })
        {
            var other = _factory.AuthorizedClient(username);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await other.GetAsync("/api/documents/strike-off-reminders")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await other.GetAsync("/api/documents/strike-off-reminders/count")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await other.PostAsync($"/api/documents/{id}/strike-off-reminder/dismiss", null)).StatusCode);
        }

        // محامٍ آخر لا يملك الملف: لا يراه في قائمته ولا يلغي تذكيره.
        int branchId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            branchId = db.Users.Single(u => u.Username == "lawyer1").BranchId!.Value;
        }
        var rival = await _factory.CreateUserAsync("lawyer-rival", UserRole.Lawyer, branchId: branchId);
        var rivalClient = _factory.ClientForUser(rival);
        var rivalList = await ListAsync(rivalClient);
        Assert.DoesNotContain(rivalList.Items, d => d.Id == id);
        Assert.Equal(HttpStatusCode.NotFound,
            (await rivalClient.PostAsync($"/api/documents/{id}/strike-off-reminder/dismiss", null)).StatusCode);
    }
}
