using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static DocGenerator.Api.Tests.TestHelpers.ApiFixtureExtensions;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// عقود شكل 403 بعد R0-6 (قرار المالك: `{ message }` من المعالج العام بدل
/// `Forbid()` الفارغ): رفض النطاق المتوقع يُرد 403 مع جسم JSON يحمل رسالة
/// غير فارغة — مثبت هنا ضد انزلاق الشكل مستقبلًا.
/// </summary>
public sealed class ForbiddenShapeContractTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private string _lawyer1 = string.Empty;
    private string _lawyer2 = string.Empty;
    private string _delegate = string.Empty;

    public async Task InitializeAsync()
    {
        var lawyer1 = UniqueName("law1");
        var lawyer2 = UniqueName("law2");
        var del = UniqueName("del");
        await _factory.CreateUserAsync(lawyer1, UserRole.Lawyer, await _factory.GetDamascusIdAsync());
        await _factory.CreateUserAsync(lawyer2, UserRole.Lawyer, await _factory.GetDamascusIdAsync());
        await _factory.CreateUserAsync(del, UserRole.EntityManager);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var creator = await db.Users.SingleAsync(u => u.Username == lawyer1);
        var group = new PublicEntityGroup
        {
            CanonicalName = "وزارة العقد",
            EntityType = PublicEntityTypeCatalog.Ministry,
        };
        group.Entries.Add(new PublicEntity
        {
            Governorate = "دمشق",
            BranchName = "الفرع الرئيسي",
            Status = EntityStatusCatalog.Final,
            CreatedById = creator.Id,
        });
        db.PublicEntityGroups.Add(group);
        await db.SaveChangesAsync();
        (await db.Users.SingleAsync(u => u.Username == del)).PortalEntryId =
            group.Entries.First().Id;
        await db.SaveChangesAsync();

        _lawyer1 = lawyer1;
        _lawyer2 = lawyer2;
        _delegate = del;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("message").GetString() ?? string.Empty;
    }

    [Fact]
    public async Task Portal_Files_WithOutOfScopeEntryId_Returns403WithMessage()
    {
        var client = _factory.AuthorizedClient(_delegate);

        var response = await client.GetAsync("/api/portal/files?entryId=2147483647");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await ReadMessageAsync(response)));
    }

    [Fact]
    public async Task ReviewLetters_Create_OnForeignDocument_Returns403WithMessage()
    {
        var token1 = (await _factory.LoginAsync(_lawyer1, "123456"))!.Token!;
        var docId = await _factory.CreateDocumentAsync(token1);
        var lawyer2 = _factory.AuthorizedClient(_lawyer2);

        var response = await lawyer2.PostAsJsonAsync("/api/review-letters", new
        {
            documentId = docId,
            bodyHtml = "<p>محتوى كتاب التجربة</p>",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await ReadMessageAsync(response)));
    }

    /// <summary>
    /// عقد الدفعة A (R0-6 توسع): رئيس دمشق يعاين عملية فرع على قيد حلب أدخله
    /// محامي حلب — يبلغ مسار الخدمة الرامي (`EnsureHeadScopeAsync`) لا الحارس
    /// الاستباقي، فيُرد 403 بجسم `{ message }` غير فارغ من المعالج العام.
    /// </summary>
    [Fact]
    public async Task EntityRegistry_PreviewBranch_OutOfScope_Returns403WithMessage()
    {
        var damId = await _factory.GetDamascusIdAsync();
        var alpId = await _factory.GetBranchIdAsync("ALP");
        var alpLawyer = await _factory.CreateUserAsync(UniqueName("alplaw"), UserRole.Lawyer, alpId);

        int groupId;
        int aleppoChildId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var group = new PublicEntityGroup
            {
                CanonicalName = $"مصرف العقد {Guid.NewGuid():N}".Trim(),
                EntityType = PublicEntityTypeCatalog.Company,
            };
            var damChild = new PublicEntity
            {
                Group = group,
                Governorate = "دمشق",
                BranchName = "فرع دمشق",
                CreatedById = alpLawyer.Id,
            };
            var aleppoChild = new PublicEntity
            {
                Group = group,
                Governorate = "حلب",
                BranchName = "فرع حلب",
                CreatedById = alpLawyer.Id,
            };
            group.Entries.Add(damChild);
            group.Entries.Add(aleppoChild);
            db.PublicEntityGroups.Add(group);
            await db.SaveChangesAsync();
            groupId = group.Id;
            aleppoChildId = aleppoChild.Id;
        }

        // رئيس دمشق المزروع (`head1` فرع DAM) — يمرّ الحارس الاستباقي
        // (`CanManageEntityRegistry`) ويُرفَض خدميًا خارج محافظته.
        var head = _factory.AuthorizedClient("head1");

        var response = await head.PostAsJsonAsync(
            $"/api/entity-registry/groups/{groupId}/branches/preview",
            new
            {
                action = "rename",
                entryId = aleppoChildId,
                newBranchName = "فرع حلب المحدّث",
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await ReadMessageAsync(response)));
        Assert.True(damId != alpId);
    }

    /// <summary>
    /// عقد الدفعة B — منتدى: تعديل رسالة من غير كاتبها يبلغ مسار الخدمة الرامي
    /// (`تعديل الرسالة متاح لكاتبها فقط`) لا الحارس الاستباقي، فيُرد 403 بجسم
    /// `{ message }` غير فارغ.
    /// </summary>
    [Fact]
    public async Task Forum_Update_ByNonAuthor_Returns403WithMessage()
    {
        var author = _factory.AuthorizedClient(_lawyer1);
        var post = await author.PostAsJsonAsync("/api/forum/messages", new { body = "رسالة الكاتب الأصلي" });
        post.EnsureSuccessStatusCode();
        using var doc = await post.Content.ReadFromJsonAsync<JsonDocument>();
        var id = doc!.RootElement.GetProperty("id").GetInt32();

        var other = _factory.AuthorizedClient(_lawyer2);
        var response = await other.PutAsJsonAsync($"/api/forum/messages/{id}", new { body = "تعديل دخيل" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await ReadMessageAsync(response)));
    }

    /// <summary>
    /// عقد الدفعة B — مراسلات: اطلاع محامٍ ثالث (لا منشئ ولا مستلم) على مراسلة
    /// خاصة يبلغ `CanViewAsync` الرامي (`لا تملك صلاحية الاطلاع`)، فيُرد 403 بجسم.
    /// </summary>
    [Fact]
    public async Task Correspondence_Get_ByThirdParty_Returns403WithMessage()
    {
        var lawyer3 = UniqueName("law3");
        await _factory.CreateUserAsync(lawyer3, UserRole.Lawyer, await _factory.GetDamascusIdAsync());

        var creator = _factory.AuthorizedClient(_lawyer1);
        int targetId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            targetId = await db.Users.Where(u => u.Username == _lawyer2).Select(u => u.Id).SingleAsync();
        }

        var created = await creator.PostAsJsonAsync("/api/correspondence", new
        {
            documentId = (int?)null,
            targetUserId = targetId,
            importance = "normal",
            bodyHtml = "<p>مراسلة خاصة للعقد</p>",
        });
        created.EnsureSuccessStatusCode();
        using var createdDoc = await created.Content.ReadFromJsonAsync<JsonDocument>();
        var letterId = createdDoc!.RootElement.GetProperty("id").GetInt32();

        var third = _factory.AuthorizedClient(lawyer3);
        var response = await third.GetAsync($"/api/correspondence/{letterId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await ReadMessageAsync(response)));
    }

    /// <summary>
    /// عقد الدفعة B — إدارة المستخدمين: إنشاء المدير لحساب مشرف يبلغ حد
    /// (`BQ-001د` — `حسابات المشرف يديرها مشرف فقط`)، فيُرد 403 بجسم.
    /// </summary>
    [Fact]
    public async Task UserManagement_ManagerCreateAdmin_Returns403WithMessage()
    {
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.PostAsJsonAsync("/api/users", new
        {
            username = UniqueName("adm"),
            fullName = UniqueName("adm"),
            role = "Admin",
            branchId = (int?)null,
            password = "123456",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await ReadMessageAsync(response)));
    }
}
