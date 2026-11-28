using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Api.Tests.TestHelpers;
using DocGenerator.Api.Tests.TestServices;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using static DocGenerator.Api.Tests.TestHelpers.ApiFixtureExtensions;

namespace DocGenerator.Api.Tests;

/// <summary>
/// P3-08 — ميزانية استعلامات لكل طلب (حارس N+1).
/// <para>
/// السقوف مثبّتة يدويًا بعد قياس على بذرة الاختبار، وبهامش صغير: الهدف كشف أي انحدار
/// يجعل عدد الاستعلامات ينمو مع عدد الصفوف (N+1)، لا تجميد العدد بدقة بايت.
/// القياس المرجعي (2026-11-16): قائمة 15 صفًا = 18 استعلامًا، وتفاصيل صف واحد = 18
/// استعلامًا — أي أن العدد **لا ينمو** مع الصفوف. أي N+1 في القائمة سيضيف 15 استعلامًا
/// على الأقل فيتجاوز السقف فورًا.
/// </para>
/// <para>
/// كل اختبار يستخدم <see cref="ApiFactory"/> خاصًا به لأن العدّاد Singleton داخل حاوية
/// المضيف، فلا تتقاطع الاختبارات المتوازية.
/// </para>
/// </summary>
public class QueryBudgetTests
{
    /// <summary>سقف قائمة 15 صفًا: القياس 18 + هامش 4.</summary>
    private const int ListBudgetForFifteenRows = 22;

    /// <summary>سقف تفاصيل صف واحد: القياس 18 + هامش 2.</summary>
    private const int DetailBudget = 20;

    /// <summary>سقف تصدير Excel لخمسة عشر صفًا: القياس 10 + هامش 4.</summary>
    private const int ExportBudget = 14;

    /// <summary>سقف سجل التدقيق (15 صفًا): القياس 8 + هامش 4.</summary>
    private const int AuditLogBudgetForFifteenRows = 12;

    /// <summary>سقف سجل تغييرات مستند واحد: القياس 4 + هامش 2.</summary>
    private const int ChangeLogBudget = 6;

    /// <summary>سقف إحصاءات 15 دائرة: القياس 12 + هامش 4.</summary>
    private const int CircuitStatsBudgetForFifteenCircuits = 16;

    /// <summary>سقف قائمة البوابة (15 صفًا): القياس 14 + هامش 4.</summary>
    private const int PortalListBudgetForFifteenRows = 18;

    /// <summary>سقف قائمة الملفات المُسقَطة (15 صفًا) — P3-08d: القياس ~12 + هامش.</summary>
    private const int ProjectedListBudgetForFifteenRows = 20;

    /// <summary>سقف صفحة المؤشر (15 صفًا) — نفس مسار الإسقاط بلا إزاحة.</summary>
    private const int ProjectedCursorBudgetForFifteenRows = 20;

    /// <summary>سقف القوائم الأرشيفية المُسقَطة (15 صفًا) — بلا إثراء استئناف.</summary>
    private const int ArchiveListBudgetForFifteenRows = 18;

    /// <summary>سقف تذكيرات الشطب (15 صفًا) — استعلام اختيار + تحميل دفعي.</summary>
    private const int RemindersListBudgetForFifteenRows = 18;

    /// <summary>سقف تصدير البوابة المُسقَط (15 صفًا) — P3-04d.</summary>
    private const int PortalExportBudgetForFifteenRows = 20;

    // ── W5-F8: سقوف مسارات W5 (الإحصاءات/التنبيهات/خيارات الفلترة/تفاصيل البوابة/البحث) ──
    // القياس المرجعي غير متاح محليًا (لا PostgreSQL)؛ السقوف = تقدير من مسار الاستعلام
    // الثابت (تحليل ثابت) + هامش صغير، والتحقق الفعلي في بوابة `backend-postgres`.

    /// <summary>إحصاءات المدير: استعلام تجميعي واحد على 15 ملفًا + هامش.</summary>
    private const int ManagerStatsBudgetForFifteenDocuments = 14;

    /// <summary>قائمة التنبيهات (15 صفًا): صفحة واحدة بلا استعلام لكل صف.</summary>
    private const int AlertsListBudgetForFifteenRows = 14;

    /// <summary>قائمة «مرسلة» للرئيس (15 صفًا).</summary>
    private const int AlertsSentBudgetForFifteenRows = 14;

    /// <summary>خيارات الفلترة: عدة استعلامات DISTINCT ثابتة (لا تنمو بعدد الصفوف).</summary>
    private const int FilterOptionsBudget = 20;

    /// <summary>تفاصيل ملف البوابة: شجرة قراءة واحدة + تحميلات دفعية.</summary>
    private const int PortalFileDetailBudget = 26;

    /// <summary>إحصاءات البوابة: استعلامات تجميعية ثابتة.</summary>
    private const int PortalStatsBudget = 14;

    /// <summary>قائمة البحث النصي (15 صفًا): مسار الإسقاط + مطابقة البحث.</summary>
    private const int SearchListBudgetForFifteenRows = 26;

    [PostgresFact]
    public async Task DocumentsList_PageOfFifteen_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var login = await factory.LoginAsync("lawyer1", "123456");
        var token = login!.Token!;

        for (var i = 1; i <= 15; i++)
        {
            await factory.CreateDocumentAsync(token, borrowerName: $"مستلف اختبار {i}");
        }

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await factory.ClientWithToken(token).GetAsync("/api/documents?perPage=15");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            counter.Count <= ListBudgetForFifteenRows,
            $"تجاوزت قائمة المستندات ميزانية الاستعلامات: {counter.Count} > {ListBudgetForFifteenRows}");
    }

    [PostgresFact]
    public async Task DocumentDetail_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var login = await factory.LoginAsync("lawyer1", "123456");
        var token = login!.Token!;
        var documentId = await factory.CreateDocumentAsync(token);

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await factory.ClientWithToken(token).GetAsync($"/api/documents/{documentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            counter.Count <= DetailBudget,
            $"تجاوزت تفاصيل المستند ميزانية الاستعلامات: {counter.Count} > {DetailBudget}");
    }

    [PostgresFact]
    public async Task DocumentsExport_WithFifteenRows_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var login = await factory.LoginAsync("lawyer1", "123456");
        var token = login!.Token!;

        for (var i = 1; i <= 15; i++)
        {
            await factory.CreateDocumentAsync(token, borrowerName: $"مستلف تصدير {i}");
        }

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await factory.ClientWithToken(token)
            .GetAsync("/api/documents/export?status=" + Uri.EscapeDataString("تحت رفع"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            counter.Count <= ExportBudget,
            $"تجاوز تصدير المستندات ميزانية الاستعلامات: {counter.Count} > {ExportBudget}");
    }

    /// <summary>سجل التدقيق (15 صفًا): العدد لا ينمو مع الصفوف.</summary>
    [PostgresFact]
    public async Task AuditLog_PageOfFifteen_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var lawyerToken = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;

        for (var i = 1; i <= 15; i++)
        {
            await factory.CreateDocumentAsync(lawyerToken, borrowerName: $"سجل تدقيق {i}");
        }

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await factory.AuthorizedClient("admin").GetAsync("/api/audit-logs?perPage=15");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            // صفحة بلا صفوف لا تكشف N+1؛ نثبت أن القياس على 15 صفًا فعلية.
            Assert.True(
                json.RootElement.GetProperty("items").GetArrayLength() > 0,
                "صفحة سجل التدقيق فارغة — القياس بلا صفوف.");
        }

        Assert.True(
            counter.Count <= AuditLogBudgetForFifteenRows,
            $"تجاوز سجل التدقيق ميزانية الاستعلامات: {counter.Count} > {AuditLogBudgetForFifteenRows}");
    }

    /// <summary>سجل تغييرات مستند (مجموعات + حقول): بلا استعلام لكل مجموعة/حقل.</summary>
    [PostgresFact]
    public async Task DocumentChanges_StayWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var lawyerToken = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var documentId = await factory.CreateDocumentAsync(lawyerToken);

        // تعديل عدة حقول لإنتاج مجموعة تغييرات حقلية (وإلا كانت الصفحة فارغة)،
        // ولضمان أن أي استعلام لكل حقل يتجاوز السقف لا يمر بهوامش صغيرة.
        var update = await factory.ClientWithToken(lawyerToken).PutAsJsonAsync(
            $"/api/documents/{documentId}",
            new
            {
                borrowerName = "مقترض محدَّث",
                applicant = "مدعٍ محدَّث",
                court = "محكمة محدَّثة",
                contractType = "تعهد",
                amountNumeric = 750,
            });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await factory.ClientWithToken(lawyerToken)
            .GetAsync($"/api/documents/{documentId}/changes?perPage=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            Assert.True(
                json.RootElement.GetProperty("items").GetArrayLength() > 0,
                "سجل تغييرات المستند فارغ — القياس بلا صفوف.");
        }

        Assert.True(
            counter.Count <= ChangeLogBudget,
            $"تجاوز سجل تغييرات المستند ميزانية الاستعلامات: {counter.Count} > {ChangeLogBudget}");
    }

    /// <summary>إحصاءات الدوائر (15 دائرة): استعلامات تجميعية ثابتة لا دائرية.</summary>
    [PostgresFact]
    public async Task CircuitStats_WithFifteenCircuits_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var head = factory.AuthorizedClient("head1");

        for (var i = 1; i <= 15; i++)
        {
            var created = await head.PostAsJsonAsync(
                "/api/execution-circuits", new { name = UniqueName($"دائرة {i}") });
            created.EnsureSuccessStatusCode();
        }

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await factory.AuthorizedClient("manager").GetAsync("/api/execution-circuits/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            Assert.True(
                json.RootElement.GetArrayLength() >= 15,
                $"إحصاءات الدوائر أعادت {json.RootElement.GetArrayLength()} صفًا والمطلوب ≥15.");
        }

        Assert.True(
            counter.Count <= CircuitStatsBudgetForFifteenCircuits,
            $"تجاوزت إحصاءات الدوائر ميزانية الاستعلامات: {counter.Count} > {CircuitStatsBudgetForFifteenCircuits}");
    }

    /// <summary>قائمة البوابة (15 ملفًا): النطاق يُحسب مرة، والصفوف لا تضيف استعلامات.</summary>
    [PostgresFact]
    public async Task PortalFiles_PageOfFifteen_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var lawyerToken = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var (delegateUsername, _) = await SeedPortalScopeAsync(factory, lawyerToken, 15);

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await factory.AuthorizedClient(delegateUsername)
            .GetAsync("/api/portal/files?perPage=15");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            Assert.True(
                json.RootElement.GetProperty("items").GetArrayLength() >= 15,
                $"قائمة البوابة أعادت {json.RootElement.GetProperty("items").GetArrayLength()} صفًا والمطلوب ≥15.");
        }

        Assert.True(
            counter.Count <= PortalListBudgetForFifteenRows,
            $"تجاوزت قائمة البوابة ميزانية الاستعلامات: {counter.Count} > {PortalListBudgetForFifteenRows}");
    }

    /// <summary>تهيئة نطاق مندوب (جهة نهائية + مستخدم مندوب + 15 ملفًا مرتبطًا) لاختبارات ميزانية البوابة.</summary>
    private static async Task<(string DelegateUsername, List<int> DocumentIds)> SeedPortalScopeAsync(
        ApiFactory factory, string lawyerToken, int count)
    {
        string delegateUsername;
        var documentIds = new List<int>(count);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var creator = db.Users.Single(u => u.Username == "lawyer1");

            var group = new PublicEntityGroup
            {
                CanonicalName = UniqueName("جهة الميزانية"),
                EntityType = PublicEntityTypeCatalog.Ministry,
            };
            group.Entries.Add(new PublicEntity
            {
                Governorate = "دمشق",
                BranchName = "مديرية الميزانية",
                Status = EntityStatusCatalog.Final,
                CreatedById = creator.Id,
            });
            db.PublicEntityGroups.Add(group);
            await db.SaveChangesAsync();
            var entryId = group.Entries.First().Id;

            var delegateUser = await factory.CreateUserAsync(
                UniqueName("del_budget"), UserRole.EntityManager);
            delegateUsername = delegateUser.Username;
            db.Users.Single(u => u.Id == delegateUser.Id).PortalEntryId = entryId;
            await db.SaveChangesAsync();

            for (var i = 1; i <= count; i++)
            {
                var documentId = await factory.CreateDocumentAsync(
                    lawyerToken, borrowerName: $"ملف بوابة {i}");
                documentIds.Add(documentId);
                db.ApplicantPublicEntities.Add(new ApplicantPublicEntity
                {
                    DocumentId = documentId,
                    RegistryId = entryId,
                    Name = "مديرية الميزانية",
                    Branch = "مديرية الميزانية",
                    Governorate = "دمشق",
                });
            }

            await db.SaveChangesAsync();
        }

        return (delegateUsername, documentIds);
    }

    /// <summary>تصدير البوابة المُسقَط (P3-04d): 15 صفًا بنفس ميزانية القائمة بلا شجرة تحميل كاملة.</summary>
    [PostgresFact]
    public async Task PortalExport_WithFifteenRows_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var lawyerToken = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var (delegateUsername, _) = await SeedPortalScopeAsync(factory, lawyerToken, 15);

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await factory.AuthorizedClient(delegateUsername)
            .GetAsync("/api/portal/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            counter.Count <= PortalExportBudgetForFifteenRows,
            $"تجاوز تصدير البوابة ميزانية الاستعلامات: {counter.Count} > {PortalExportBudgetForFifteenRows}");
    }

    // ── P3-08d: ميزانيات مسارات القراءة المُسقَطة/المؤشر (P3-04/P3-05) ──

    private static async Task<List<int>> CreateFifteenDocumentsAsync(
        ApiFactory factory, string token, string prefix, bool registered = true)
    {
        var ids = new List<int>(15);
        for (var i = 1; i <= 15; i++)
            ids.Add(await factory.CreateDocumentAsync(token, $"{prefix} {i}", registered: registered));
        return ids;
    }

    [PostgresFact]
    public async Task DocumentsProjectedList_PageOfFifteen_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var token = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;
        await CreateFifteenDocumentsAsync(factory, token, "قائمة مُسقَطة");

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await factory.ClientWithToken(token).GetAsync("/api/documents/list?perPage=15");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            counter.Count <= ProjectedListBudgetForFifteenRows,
            $"تجاوزت القائمة المُسقَطة ميزانية الاستعلامات: {counter.Count} > {ProjectedListBudgetForFifteenRows}");
    }

    [PostgresFact]
    public async Task DocumentsProjectedList_CursorPage_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var token = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;
        await CreateFifteenDocumentsAsync(factory, token, "صفحة مؤشر");
        var client = factory.ClientWithToken(token);

        var first = await client.GetAsync("/api/documents/list?perPage=10");
        using var firstBody = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var cursor = firstBody.RootElement.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(cursor));

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await client.GetAsync(
            "/api/documents/list?perPage=10&cursor=" + Uri.EscapeDataString(cursor!));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            counter.Count <= ProjectedCursorBudgetForFifteenRows,
            $"تجاوزت صفحة المؤشر ميزانية الاستعلامات: {counter.Count} > {ProjectedCursorBudgetForFifteenRows}");
    }

    [PostgresFact]
    public async Task StruckOffList_PageOfFifteen_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var token = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = factory.ClientWithToken(token);
        foreach (var id in await CreateFifteenDocumentsAsync(factory, token, "مشطوبة"))
        {
            var strike = await client.PostAsJsonAsync($"/api/documents/{id}/status",
                new { status = "مشطوب", fields = new { struckOffDate = "1/2/2024" } });
            Assert.Equal(HttpStatusCode.OK, strike.StatusCode);
        }

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();
        var response = await client.GetAsync("/api/documents/struck-off/list?perPage=15");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            counter.Count <= ArchiveListBudgetForFifteenRows,
            $"تجاوزت قائمة المشطوبة ميزانية الاستعلامات: {counter.Count} > {ArchiveListBudgetForFifteenRows}");
    }

    [PostgresFact]
    public async Task ExecutedList_PageOfFifteen_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var token = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = factory.ClientWithToken(token);
        foreach (var id in await CreateFifteenDocumentsAsync(factory, token, "منفذة"))
        {
            var run = await client.PostAsJsonAsync($"/api/documents/{id}/status",
                new { status = "منفذ بالتسوية", fields = new { baraetNumber = "1", baraetDate = "1/3/2026" } });
            Assert.Equal(HttpStatusCode.OK, run.StatusCode);
        }

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();
        var response = await client.GetAsync("/api/documents/executed/list?perPage=15");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            counter.Count <= ArchiveListBudgetForFifteenRows,
            $"تجاوزت قائمة المنفذة ميزانية الاستعلامات: {counter.Count} > {ArchiveListBudgetForFifteenRows}");
    }

    [PostgresFact]
    public async Task ReferredToStartList_PageOfFifteen_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var token = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = factory.ClientWithToken(token);
        foreach (var id in await CreateFifteenDocumentsAsync(factory, token, "محالة"))
        {
            var referred = await client.PostAsJsonAsync($"/api/documents/{id}/status",
                new
                {
                    status = "محال الى البداية",
                    fields = new
                    {
                        noFundsDemandNumber = "5",
                        noFundsDemandDate = "1/1/2024",
                        startReferralNumber = "6",
                        startReferralDate = "2/1/2024",
                    },
                });
            Assert.Equal(HttpStatusCode.OK, referred.StatusCode);
        }

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();
        var response = await client.GetAsync("/api/documents/referred-to-start/list?perPage=15");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            counter.Count <= ArchiveListBudgetForFifteenRows,
            $"تجاوزت قائمة المحالة ميزانية الاستعلامات: {counter.Count} > {ArchiveListBudgetForFifteenRows}");
    }

    [PostgresFact]
    public async Task DeletedList_PageOfFifteen_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var token = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = factory.ClientWithToken(token);
        foreach (var id in await CreateFifteenDocumentsAsync(factory, token, "محذوفة"))
        {
            var delete = await client.DeleteAsync($"/api/documents/{id}");
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        }

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();
        var response = await client.GetAsync("/api/documents/deleted/list?perPage=15");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            counter.Count <= ArchiveListBudgetForFifteenRows,
            $"تجاوزت قائمة المحذوفة ميزانية الاستعلامات: {counter.Count} > {ArchiveListBudgetForFifteenRows}");
    }

    [PostgresFact]
    public async Task StrikeOffRemindersList_PageOfFifteen_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var token = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = factory.ClientWithToken(token);
        var oldDate = ApiTestClock.Today.AddMonths(-7)
            .ToString("d/M/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var id in await CreateFifteenDocumentsAsync(factory, token, "تذكير"))
        {
            var action = await client.PostAsJsonAsync($"/api/documents/{id}/actions", new
            {
                type = "action",
                text = "إجراء قديم",
                actionDate = oldDate,
                reminderDuration = (string?)null,
                reminderColor = (string?)null,
            });
            Assert.Equal(HttpStatusCode.OK, action.StatusCode);
        }

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();
        var response = await client.GetAsync("/api/documents/strike-off-reminders/list?perPage=15");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            counter.Count <= RemindersListBudgetForFifteenRows,
            $"تجاوزت تذكيرات الشطب ميزانية الاستعلامات: {counter.Count} > {RemindersListBudgetForFifteenRows}");
    }

    // ── W5-F8: سقوف مسارات W5 (المستخدم/المدير/البوابة) — تُتحقق فعليًا في backend-postgres ──

    /// <summary>يبذر تنبيهات مباشرة لاستقبال محامٍ واحد (بلا خدمة الإحلال) لقياس القراءة.</summary>
    private static async Task SeedAlertsForBudgetAsync(
        ApiFactory factory, int branchId, int recipientUserId, int createdById, int count)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var anchor = new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < count; i++)
        {
            db.HeadAlerts.Add(new HeadAlert
            {
                BranchId = branchId,
                CreatedById = createdById,
                TargetType = HeadAlertTargetType.Lawyer,
                Kind = HeadAlertKindCatalog.ManualToLawyer,
                TargetLawyerId = recipientUserId,
                Message = $"تنبيه ميزانية {i}",
                CreatedAt = anchor.AddMinutes(i),
                LastContentChangedAt = anchor.AddMinutes(i),
                Recipients = new List<HeadAlertRecipient> { new() { UserId = recipientUserId } },
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>إحصاءات المدير على 15 ملفًا: تجميع واحد لا استعلام لكل ملف (F6 يبقى مؤجلًا).</summary>
    [PostgresFact]
    public async Task ManagerStats_WithFifteenDocuments_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var token = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;
        await CreateFifteenDocumentsAsync(factory, token, "إحصاء مدير", registered: true);

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await factory.AuthorizedClient("manager").GetAsync("/api/stats/manager?period=yearly");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            Assert.True(
                json.RootElement.GetProperty("totalFiles").GetInt32() >= 15,
                "إحصاءات المدير لم تُحصِ الملفات الخمسة عشر — القياس بلا صفوف.");
        }

        Assert.True(
            counter.Count <= ManagerStatsBudgetForFifteenDocuments,
            $"تجاوزت إحصاءات المدير ميزانية الاستعلامات: {counter.Count} > {ManagerStatsBudgetForFifteenDocuments}");
    }

    /// <summary>قائمة التنبيهات (15 صفًا): صفحة واحدة بلا استعلام لكل صف.</summary>
    [PostgresFact]
    public async Task AlertsList_PageOfFifteen_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var branchId = await factory.GetBranchIdAsync("DAM");
        var (lawyerId, client) = await factory.NewLawyerClientAsync(branchId);
        var createdById = await factory.UserIdAsync("head1");
        await SeedAlertsForBudgetAsync(factory, branchId, lawyerId, createdById, 15);

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await client.GetAsync("/api/alerts?pageSize=15");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            Assert.True(
                json.RootElement.GetProperty("items").GetArrayLength() >= 15,
                "قائمة التنبيهات أعادت أقل من 15 صفًا — القياس بلا صفوف.");
        }

        Assert.True(
            counter.Count <= AlertsListBudgetForFifteenRows,
            $"تجاوزت قائمة التنبيهات ميزانية الاستعلامات: {counter.Count} > {AlertsListBudgetForFifteenRows}");
    }

    /// <summary>قائمة «مرسلة» للرئيس (15 صفًا).</summary>
    [PostgresFact]
    public async Task AlertsSent_PageOfFifteen_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var branchId = await factory.GetBranchIdAsync("DAM");
        var (lawyerId, _) = await factory.NewLawyerClientAsync(branchId);
        var createdById = await factory.UserIdAsync("head1");
        await SeedAlertsForBudgetAsync(factory, branchId, lawyerId, createdById, 15);

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await factory.AuthorizedClient("head1").GetAsync("/api/alerts/sent?pageSize=15");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            Assert.True(
                json.RootElement.GetProperty("items").GetArrayLength() >= 15,
                "قائمة «مرسلة» أعادت أقل من 15 صفًا — القياس بلا صفوف.");
        }

        Assert.True(
            counter.Count <= AlertsSentBudgetForFifteenRows,
            $"تجاوزت قائمة «مرسلة» ميزانية الاستعلامات: {counter.Count} > {AlertsSentBudgetForFifteenRows}");
    }

    /// <summary>خيارات الفلترة: استعلامات DISTINCT ثابتة لا تنمو مع عدد الملفات.</summary>
    [PostgresFact]
    public async Task FilterOptions_WithFifteenDocuments_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var token = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;
        await CreateFifteenDocumentsAsync(factory, token, "خيارات فلترة");

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await factory.ClientWithToken(token).GetAsync("/api/documents/filter-options");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            Assert.True(
                json.RootElement.GetProperty("applicants").GetArrayLength() > 0,
                "خيارات الفلترة بلا قيم — القياس بلا صفوف.");
        }

        Assert.True(
            counter.Count <= FilterOptionsBudget,
            $"تجاوزت خيارات الفلترة ميزانية الاستعلامات: {counter.Count} > {FilterOptionsBudget}");
    }

    /// <summary>تفاصيل ملف البوابة: شجرة قراءة واحدة وتحميلات دفعية (لا استعلام لكل علاقة).</summary>
    [PostgresFact]
    public async Task PortalFileDetail_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var lawyerToken = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var (delegateUsername, documentIds) = await SeedPortalScopeAsync(factory, lawyerToken, 15);

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await factory.AuthorizedClient(delegateUsername)
            .GetAsync($"/api/portal/files/{documentIds[0]}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            // تأكيد الهوية: استجابة تفاصيل ملف آخر/فارغة تُفشل القياس لا تمرّ بسقف زائف.
            Assert.Equal(documentIds[0], json.RootElement.GetProperty("id").GetInt32());
        }

        Assert.True(
            counter.Count <= PortalFileDetailBudget,
            $"تجاوز تفاصيل ملف البوابة ميزانية الاستعلامات: {counter.Count} > {PortalFileDetailBudget}");
    }

    /// <summary>إحصاءات البوابة: تجميعيات ثابتة على نطاق الجهة.</summary>
    [PostgresFact]
    public async Task PortalStats_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var lawyerToken = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var (delegateUsername, _) = await SeedPortalScopeAsync(factory, lawyerToken, 15);

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var response = await factory.AuthorizedClient(delegateUsername).GetAsync("/api/portal/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            Assert.True(
                json.RootElement.GetProperty("totalFiles").GetInt32() >= 15,
                "إحصاءات البوابة لم تُحصِ الملفات الخمسة عشر — القياس بلا صفوف.");
        }

        Assert.True(
            counter.Count <= PortalStatsBudget,
            $"تجاوزت إحصاءات البوابة ميزانية الاستعلامات: {counter.Count} > {PortalStatsBudget}");
    }

    /// <summary>مسار البحث النصي في `/documents/list`: لا استعلام لكل نتيجة.</summary>
    [PostgresFact]
    public async Task DocumentsProjectedList_Search_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var token = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;
        await CreateFifteenDocumentsAsync(factory, token, "بحث ميزانية");

        var counter = factory.Services.GetRequiredService<QueryCounter>();
        counter.Reset();

        var query = Uri.EscapeDataString("بحث ميزانية");
        var response = await factory.ClientWithToken(token)
            .GetAsync($"/api/documents/list?q={query}&perPage=15");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            Assert.True(
                json.RootElement.GetProperty("items").GetArrayLength() >= 15,
                "بحث القائمة أعاد أقل من 15 صفًا — القياس بلا صفوف.");
        }

        Assert.True(
            counter.Count <= SearchListBudgetForFifteenRows,
            $"تجاوز البحث في القائمة ميزانية الاستعلامات: {counter.Count} > {SearchListBudgetForFifteenRows}");
    }
}
