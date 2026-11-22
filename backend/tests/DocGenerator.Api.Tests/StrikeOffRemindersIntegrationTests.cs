using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Api.Tests.TestHelpers;
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

    private static string MonthsAgo(int months) =>
        ApiTestClock.Today.AddMonths(-months).ToString("d/M/yyyy", CultureInfo.InvariantCulture);

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

    private static async Task<PagedResult<DocumentListItemDto>> ProjectedListAsync(HttpClient client, int perPage = 100)
    {
        var page = await (await client.GetAsync($"/api/documents/strike-off-reminders/list?page=1&perPage={perPage}"))
            .Content.ReadFromJsonAsync<PagedResult<DocumentListItemDto>>();
        Assert.NotNull(page);
        return page!;
    }

    [PostgresFact]
    public async Task ProjectedList_CursorPaginatesOldestIdleFirst_WithoutDuplicates()
    {
        var (client, token) = await LawyerAsync();
        var oldest = await _factory.CreateDocumentAsync(token, "الأقدم للمؤشر", registered: true);
        var middle = await _factory.CreateDocumentAsync(token, "الأوسط للمؤشر", registered: true);
        var newest = await _factory.CreateDocumentAsync(token, "الأحدث للمؤشر", registered: true);
        await AddActionAsync(client, oldest, MonthsAgo(9));
        await AddActionAsync(client, middle, MonthsAgo(8));
        await AddActionAsync(client, newest, MonthsAgo(7));

        var first = await ProjectedListAsync(client, perPage: 2);
        Assert.Equal(2, first.Items.Count);
        Assert.False(string.IsNullOrWhiteSpace(first.NextCursor));

        var secondPage = await (await client.GetAsync(
                "/api/documents/strike-off-reminders/list?perPage=2&cursor=" +
                Uri.EscapeDataString(first.NextCursor!)))
            .Content.ReadFromJsonAsync<PagedResult<DocumentListItemDto>>();
        Assert.NotNull(secondPage);
        Assert.Single(secondPage!.Items);
        Assert.Null(secondPage.NextCursor);

        var all = first.Items.Select(d => d.Id).Concat(secondPage.Items.Select(d => d.Id)).ToList();
        Assert.Equal(3, all.Distinct().Count());
        Assert.Equal([oldest, middle, newest], all);
    }

    [PostgresFact]
    public async Task ProjectedListEndpoint_MatchesLegacySelection_AndCarriesCardFields()
    {
        var (client, token) = await LawyerAsync();
        var id = await _factory.CreateDocumentAsync(token, "تذكير مُسقط", registered: true);
        await AddActionAsync(client, id, MonthsAgo(7));

        var legacy = await ListAsync(client);
        var projected = await ProjectedListAsync(client);

        Assert.Equal(legacy.TotalCount, projected.TotalCount);
        Assert.Equal(
            legacy.Items.Select(d => d.Id).ToList(),
            projected.Items.Select(d => d.Id).ToList());

        var row = Assert.Single(projected.Items, d => d.Id == id);
        Assert.Equal("action", row.LastActivitySource);
        Assert.NotNull(row.LastActivityDate);
        Assert.Equal("متداول", row.DisplayStatus);
        Assert.False(string.IsNullOrWhiteSpace(row.DisplayFileNumber));
    }

    [PostgresFact]
    public async Task OldAction_MakesFileDue_AndNewActionRemovesIt()
    {
        var (client, token) = await LawyerAsync();
        var id = await _factory.CreateDocumentAsync(token, "مقاربة الشطب", registered: true);
        await AddActionAsync(client, id, MonthsAgo(7));

        var due = await ListAsync(client);
        Assert.Contains(due.Items, d => d.Id == id);

        // إجراء حديث يرفع آخر نشاط ⇒ يخرج من القائمة فورًا (بلا كتابة حالة).
        await AddActionAsync(client, id, MonthsAgo(1));
        var after = await ListAsync(client);
        Assert.DoesNotContain(after.Items, d => d.Id == id);
    }

    [PostgresFact]
    public async Task NotesAreNotCounted_FileWithOldNoteIsNotDue()
    {
        var (client, token) = await LawyerAsync();
        var id = await _factory.CreateDocumentAsync(token, "ملاحظة قديمة", registered: true);
        // ملاحظة بتاريخ قديم: لا تُحتسب ⇒ آخر نشاط = تاريخ القيد (1/8/2026) ⇒ غير مستحق.
        await AddActionAsync(client, id, "1/1/2026", type: "note");

        var due = await ListAsync(client);
        Assert.DoesNotContain(due.Items, d => d.Id == id);
    }

    [PostgresFact]
    public async Task Dismiss_HidesFile_AndCountMatchesList()
    {
        var (client, token) = await LawyerAsync();
        var id = await _factory.CreateDocumentAsync(token, "ملف يُلغى تذكيره", registered: true);
        await AddActionAsync(client, id, MonthsAgo(7));

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
    [PostgresFact]
    public async Task NoActionsAndNoRegistration_FallsBackToCreatedAt()
    {
        var (client, _) = await LawyerAsync();
        int id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var lawyer = db.Users.Single(u => u.Username == "lawyer1");
            // القيد `CK_Documents_FileNumberRequiresCircuit` يفرض دائرة لملف مرقّم، كما
            // في الإنتاج: تُنشأ/تُعاد دائرة اختبار للفرع ثم تُربط بالملف.
            var circuitId = await _factory.EnsureCircuitForBranchAsync(
                lawyer.BranchId!.Value, "دائرة تنفيذ اختبار التذكير");
            var doc = new Document
            {
                CreatedById = lawyer.Id,
                BranchId = lawyer.BranchId,
                ExecutionCircuitId = circuitId,
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

    [PostgresFact]
    public async Task OldestIdle_First()
    {
        var (client, token) = await LawyerAsync();
        var older = await _factory.CreateDocumentAsync(token, "الأقدم ركودًا", registered: true);
        var newer = await _factory.CreateDocumentAsync(token, "الأحدث ركودًا", registered: true);
        await AddActionAsync(client, newer, MonthsAgo(6));
        await AddActionAsync(client, older, MonthsAgo(9));

        var due = await ListAsync(client);
        var olderIndex = due.Items.FindIndex(d => d.Id == older);
        var newerIndex = due.Items.FindIndex(d => d.Id == newer);
        Assert.True(olderIndex >= 0 && newerIndex >= 0);
        Assert.True(olderIndex < newerIndex);
    }

    [PostgresFact]
    public async Task NonLawyerRoles_Forbidden_AndOtherLawyer_CannotDismiss()
    {
        var (client, token) = await LawyerAsync();
        var id = await _factory.CreateDocumentAsync(token, "ملف محمي", registered: true);
        await AddActionAsync(client, id, MonthsAgo(7));

        foreach (var username in new[] { "head1", "manager" })
        {
            var other = _factory.AuthorizedClient(username);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await other.GetAsync("/api/documents/strike-off-reminders")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,
                (await other.GetAsync("/api/documents/strike-off-reminders/list")).StatusCode);
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
