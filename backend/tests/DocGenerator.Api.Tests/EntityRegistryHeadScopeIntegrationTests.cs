using System.Net;
using System.Net.Http.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// مصفوفة نطاق رئيس القسم على سجل الجهات (د3/د4 + خطة إدارة الفروع §4):
/// — الرئيس يقرأ سجل التغييرات والقيود ضمن محافظته فقط (جبر خادمي حتى لو مرّر پارامتر مغايرًا).
/// — عمليات المجموعات (rename/unify/abolish على مستوى الهوية) لا تزال HasFullAccess.
/// — مراجعة/سحب اقتراحات الأم بموجب حراس الأدوار (review=الإدارة، withdraw=المالك الرئيس).
/// </summary>
[Collection(ApiTestCollection.Name)]
public class EntityRegistryHeadScopeIntegrationTests
{
    private readonly ApiFactory _factory;

    public EntityRegistryHeadScopeIntegrationTests(ApiFactory factory) => _factory = factory;

    #region تهيئة بيانات معزولة

    private async Task<int> BranchIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return db.Branches.Single(b => b.Code == code).Id;
    }

    /// <summary>
    /// يُنشئ هوية أم بقيد أم (دمشق) + قيد فرع دمشق + قيد فرع حلب، جميعها أدخلها محامٍ من فرع حلب،
    /// ويعيّن محافظة فرع رئيس القسم (DAM) إلى «دمشق» — فيُقاس النطاق خادميًا وليس عبر منشئ في الفرع.
    /// </summary>
    private async Task<(int GroupId, string CanonicalName, int DamChildId, string DamBranch, int AleppoChildId, string AleppoBranch)>
        SeedGroupWithCrossGovernorateEntriesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();

        var aleppoBranchId = await BranchIdAsync("ALP");
        var user = new User
        {
            Username = ArabicNameNormalizer.Normalize($"lawyer_scope_{Guid.NewGuid():N}"),
            FullName = "محامي حلب",
            Role = UserRole.Lawyer,
            BranchId = aleppoBranchId,
            IsActive = true,
            PasswordHash = "x",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var damBranch = db.Branches.Single(b => b.Code == "DAM");
        var savedDamGov = damBranch.Governorate;
        damBranch.Governorate = "دمشق";

        var group = new PublicEntityGroup { CanonicalName = $"مصرف النطاق {Guid.NewGuid():N}".Trim(), EntityType = PublicEntityTypeCatalog.Company };
        var parent = new PublicEntity
        {
            Group = group,
            Governorate = "دمشق",
            BranchName = "الجهة الأم",
            IsParentEntity = true,
            CreatedById = user.Id,
            CreatedBy = user,
        };
        var damChild = new PublicEntity
        {
            Group = group,
            Governorate = "دمشق",
            BranchName = "فرع دمشق",
            CreatedById = user.Id,
            CreatedBy = user,
        };
        var aleppoChild = new PublicEntity
        {
            Group = group,
            Governorate = "حلب",
            BranchName = "فرع حلب",
            CreatedById = user.Id,
            CreatedBy = user,
        };
        group.Entries.Add(parent);
        group.Entries.Add(damChild);
        group.Entries.Add(aleppoChild);
        db.PublicEntityGroups.Add(group);
        await db.SaveChangesAsync();

        return (group.Id, group.CanonicalName, damChild.Id, damChild.BranchName, aleppoChild.Id, aleppoChild.BranchName);
    }

    #endregion

    [Fact]
    public async Task ChangeEvents_HeadAndManagement_Allowed_LawyerForbidden()
    {
        var head = _factory.AuthorizedClient("head1");
        var manager = _factory.AuthorizedClient("manager");
        var admin = _factory.AuthorizedClient("admin");
        var lawyer = _factory.AuthorizedClient("lawyer1");

        Assert.Equal(HttpStatusCode.OK, (await head.GetAsync("/api/entity-registry/change-events")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/entity-registry/change-events")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/entity-registry/change-events")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await lawyer.GetAsync("/api/entity-registry/change-events")).StatusCode);
    }

    [Fact]
    public async Task Head_ScopesBranchOperationsToOwnGovernorate()
    {
        var seeded = await SeedGroupWithCrossGovernorateEntriesAsync();
        var head = _factory.AuthorizedClient("head1");

        // قيود المجموعة: الأم تظهر دائمًا + فرع دمشق فقط — فلا يرى قيد حلب.
        var entriesResponse = await head.GetAsync($"/api/entity-registry/groups/{seeded.GroupId}/entries");
        Assert.Equal(HttpStatusCode.OK, entriesResponse.StatusCode);
        var entries = await entriesResponse.Content.ReadFromJsonAsync<List<PublicEntityEntryDto>>();
        Assert.NotNull(entries);
        Assert.Contains(entries, e => e.BranchName == seeded.DamBranch);
        Assert.DoesNotContain(entries, e => e.BranchName == seeded.AleppoBranch);
        Assert.Contains(entries, e => e.IsParentEntity);

        // معاينة تعديل على فرع دمشق (ضمن النطاق): نجاح.
        var inScope = await head.PostAsJsonAsync($"/api/entity-registry/groups/{seeded.GroupId}/branches/preview", new
        {
            action = "rename",
            entryId = seeded.DamChildId,
            newBranchName = "فرع دمشق المحدّث",
        });
        Assert.Equal(HttpStatusCode.OK, inScope.StatusCode);
        var preview = await inScope.Content.ReadFromJsonAsync<BranchActionPreviewResponse>();
        Assert.NotNull(preview);
        Assert.Equal("rename", preview.Action);

        // معاينة على فرع حلب (خارج النطاق): 403 خادميًا.
        var outOfScope = await head.PostAsJsonAsync($"/api/entity-registry/groups/{seeded.GroupId}/branches/preview", new
        {
            action = "rename",
            entryId = seeded.AleppoChildId,
            newBranchName = "فرع حلب المحدّث",
        });
        Assert.Equal(HttpStatusCode.Forbidden, outOfScope.StatusCode);

        // حتى لو مرّر الرئيس governorate مغايرًا في جلب القيود يُجبر نطاق محافظته.
        var forced = await head.GetAsync($"/api/entity-registry/groups/{seeded.GroupId}/entries?governorate=حلب");
        var forcedEntries = await forced.Content.ReadFromJsonAsync<List<PublicEntityEntryDto>>();
        Assert.NotNull(forcedEntries);
        Assert.DoesNotContain(forcedEntries, e => e.BranchName == seeded.AleppoBranch);
    }

    [Fact]
    public async Task GroupLevelOperations_RemainHasFullAccess()
    {
        var seeded = await SeedGroupWithCrossGovernorateEntriesAsync();
        var head = _factory.AuthorizedClient("head1");
        var manager = _factory.AuthorizedClient("manager");

        // الرئيس ممنوع من عمليات مستوى الهوية الأم حتى لو كانت المحافظة ضمن نطاقه.
        var rename = await head.PostAsJsonAsync($"/api/entity-registry/groups/{seeded.GroupId}/rename", new
        {
            groupId = seeded.GroupId,
            newCanonicalName = "تسمية جديدة",
            decreeKind = "قرار",
            decreeNumber = "1",
            decreeDate = "1/8/2026",
        });
        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);

        var unify = await head.PostAsJsonAsync("/api/entity-registry/groups/unify", new
        {
            targetGroupId = seeded.GroupId,
            absorbedGroupIds = new[] { seeded.GroupId },
            decreeKind = "قرار",
            decreeNumber = "2",
            decreeDate = "1/8/2026",
        });
        Assert.Equal(HttpStatusCode.Forbidden, unify.StatusCode);

        // المدير يجتاز الحارس (يصل للتحقق من البيانات — 400 لا 403).
        var managerRename = await manager.PostAsJsonAsync($"/api/entity-registry/groups/{seeded.GroupId}/rename", new
        {
            groupId = seeded.GroupId,
            newCanonicalName = "تسمية جديدة",
            decreeKind = "قرار",
            decreeNumber = "1",
            decreeDate = "1/8/2026",
        });
        Assert.NotEqual(HttpStatusCode.Forbidden, managerRename.StatusCode);
    }

    [Fact]
    public async Task ParentSuggestions_GuardMatrix()
    {
        var head = _factory.AuthorizedClient("head1");
        var manager = _factory.AuthorizedClient("manager");
        var lawyer = _factory.AuthorizedClient("lawyer1");

        // الاقتراح = حارس رئيس القسم فقط.
        var byLawyer = await lawyer.PostAsJsonAsync("/api/entity-registry/entries/1/suggest-parent-edit", new { reason = "سبب" });
        Assert.Equal(HttpStatusCode.Forbidden, byLawyer.StatusCode);
        var byManager = await manager.PostAsJsonAsync("/api/entity-registry/entries/1/suggest-parent-edit", new { reason = "سبب" });
        Assert.Equal(HttpStatusCode.Forbidden, byManager.StatusCode);
        var byHead = await head.PostAsJsonAsync("/api/entity-registry/entries/1/suggest-parent-edit", new { reason = "سبب" });
        Assert.NotEqual(HttpStatusCode.Forbidden, byHead.StatusCode); // يجتاز الحارس → المجموعة غير موجودة (400).

        // المراجعة = HasFullAccess.
        var reviewByHead = await head.PostAsJsonAsync("/api/entity-registry/parent-edit-suggestions/1/review", new { status = "approved" });
        Assert.Equal(HttpStatusCode.Forbidden, reviewByHead.StatusCode);
        var reviewByLawyer = await lawyer.PostAsJsonAsync("/api/entity-registry/parent-edit-suggestions/1/review", new { status = "approved" });
        Assert.Equal(HttpStatusCode.Forbidden, reviewByLawyer.StatusCode);
        var reviewByManager = await manager.PostAsJsonAsync("/api/entity-registry/parent-edit-suggestions/1/review", new { status = "approved" });
        Assert.NotEqual(HttpStatusCode.Forbidden, reviewByManager.StatusCode);

        // السحب = المالك الرئيس فقط (حارس الدور: الرؤساء عمومًا ثم الخدمة تتحقق من الملكية).
        var withdrawByManager = await manager.PostAsync("/api/entity-registry/parent-edit-suggestions/1/withdraw", null);
        Assert.Equal(HttpStatusCode.Forbidden, withdrawByManager.StatusCode);
        var withdrawByLawyer = await lawyer.PostAsync("/api/entity-registry/parent-edit-suggestions/1/withdraw", null);
        Assert.Equal(HttpStatusCode.Forbidden, withdrawByLawyer.StatusCode);
    }
}