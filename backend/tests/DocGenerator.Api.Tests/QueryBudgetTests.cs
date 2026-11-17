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

    [Fact]
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

    [Fact]
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

    [Fact]
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

        var response = await factory.ClientWithToken(token).GetAsync("/api/documents/export");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            counter.Count <= ExportBudget,
            $"تجاوز تصدير المستندات ميزانية الاستعلامات: {counter.Count} > {ExportBudget}");
    }

    /// <summary>سجل التدقيق (15 صفًا): العدد لا ينمو مع الصفوف.</summary>
    [Fact]
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
    [Fact]
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
    [Fact]
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
    [Fact]
    public async Task PortalFiles_PageOfFifteen_StaysWithinQueryBudget()
    {
        using var factory = new ApiFactory();
        var lawyerToken = (await factory.LoginAsync("lawyer1", "123456"))!.Token!;

        string delegateUsername;
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

            for (var i = 1; i <= 15; i++)
            {
                var documentId = await factory.CreateDocumentAsync(
                    lawyerToken, borrowerName: $"ملف بوابة {i}");
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
}
