using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// تكامل المراسلات عبر التطبيق الحقيقي: تسطير برقم تلقائي، عزل المندوب عن المسار
/// الرئيسي (الحارس)، كتابته عبر البوابة، الرد/اللاحق للأطراف فقط، وتوثيق المشاهدة
/// مع انطفاء جرس العاجل — ويثبت تركيب حقن الخدمة الجديدة في حاوية الإنتاج.
/// </summary>
public sealed class CorrespondenceIntegrationTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private int _branchId;
    private int _lawyerId;
    private int _headId;
    private int _delegateId;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // الفرع مزروع مسبقًا من DbSeeder (DAM/دمشق) — يُعاد استعماله لا إنشاؤه.
        var branch = await db.Branches.FirstAsync(b => b.Code == "DAM");
        var creator = new User
        {
            Username = "creator", FullName = "منشئ", Role = UserRole.Admin,
            PasswordHash = hasher.Hash("123456"),
        };
        db.Users.Add(creator);
        await db.SaveChangesAsync();

        var group = new PublicEntityGroup
        {
            CanonicalName = "وزارة التعليم",
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
        var entryId = group.Entries.First().Id;

        User mk(string username, string fullName, UserRole role, int? branchId) => new()
        {
            Username = ArabicNameNormalizer.Normalize(username),
            FullName = fullName,
            Role = role,
            BranchId = branchId,
            PasswordHash = hasher.Hash("123456"),
        };
        var lawyer = mk("corrlawyer", "المحامي", UserRole.Lawyer, branch.Id);
        var head = mk("corrhead", "الرئيس", UserRole.Head, branch.Id);
        var del = mk("corrdelegate", "المندوب", UserRole.EntityManager, null);
        db.Users.AddRange(lawyer, head, del);
        await db.SaveChangesAsync();

        del.PortalEntryId = entryId;
        await db.SaveChangesAsync();

        _branchId = branch.Id;
        _lawyerId = lawyer.Id;
        _headId = head.Id;
        _delegateId = del.Id;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private HttpClient Lawyer() => _factory.AuthorizedClient("corrlawyer");
    private HttpClient Head() => _factory.AuthorizedClient("corrhead");
    private HttpClient Delegate() => _factory.AuthorizedClient("corrdelegate");

    private static StringContent Json(object body) => new(
        JsonSerializer.Serialize(body),
        System.Text.Encoding.UTF8,
        "application/json");

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement;
    }

    [Fact]
    public async Task Lawyer_CreatesUrgentGeneralCorrespondence_ReturnsCreatedWithNumber()
    {
        var response = await Lawyer().PostAsync("/api/correspondence", Json(new
        {
            documentId = (int?)null,
            targetUserId = _delegateId,
            importance = "urgent",
            bodyHtml = "<p>زودونا بالبيانات عاجلًا</p>",
        }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        var number = body.GetProperty("correspondenceNumber").GetString() ?? string.Empty;
        Assert.StartsWith("DAM-", number);
        Assert.Equal("urgent", body.GetProperty("importance").GetString());
        Assert.Equal("دمشق", body.GetProperty("governorate").GetString());
        Assert.Equal(_delegateId, body.GetProperty("targetUserId").GetInt32());
    }

    [Fact]
    public async Task EntityManager_OnMainCorrespondencePaths_IsForbiddenByGuard()
    {
        var post = await Delegate().PostAsync("/api/correspondence", Json(new
        {
            documentId = (int?)null,
            targetUserId = _lawyerId,
            importance = "normal",
            bodyHtml = "<p>نص</p>",
        }));
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);

        var get = await Delegate().GetAsync("/api/correspondence");
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
    }

    [Fact]
    public async Task EntityManager_CreatesRepliesAndMarksSeen_ViaPortal()
    {
        // تسطير من البوابة (يثبت تركيب الخدمة في مسار المندوب).
        var create = await Delegate().PostAsync("/api/portal/correspondence", Json(new
        {
            documentId = (int?)null,
            targetUserId = _lawyerId,
            importance = "urgent",
            bodyHtml = "<p>رد الجهة</p>",
        }));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await ReadJsonAsync(create);
        var id = created.GetProperty("id").GetInt32();
        Assert.StartsWith("دمشق-", created.GetProperty("correspondenceNumber").GetString());

        // المحامي المستلم يرد من المسار الرئيسي.
        var reply = await Lawyer().PostAsync($"/api/correspondence/{id}/replies",
            Json(new { bodyHtml = "<p>استلمنا، شكرًا</p>" }));
        Assert.Equal(HttpStatusCode.OK, reply.StatusCode);

        // المندوب المنشئ لا يرد على مراسلته (الرد للمستلم فقط).
        var selfReply = await Delegate().PostAsync($"/api/portal/correspondence/{id}/replies",
            Json(new { bodyHtml = "<p>رد ذاتي</p>" }));
        Assert.Equal(HttpStatusCode.Forbidden, selfReply.StatusCode);

        // المنشئ يضيف لاحقًا؛ المستلم يُمنع من اللاحق.
        var addendum = await Delegate().PostAsync($"/api/portal/correspondence/{id}/addenda",
            Json(new { bodyHtml = "<p>إلحاق</p>" }));
        Assert.Equal(HttpStatusCode.OK, addendum.StatusCode);
        var foreignAddendum = await Lawyer().PostAsync($"/api/correspondence/{id}/addenda",
            Json(new { bodyHtml = "<p>لاحق دخيل</p>" }));
        Assert.Equal(HttpStatusCode.Forbidden, foreignAddendum.StatusCode);

        // جرس العاجل للمستلم (المحامي) ثم ينطفئ بعد التوثيق.
        var bellBefore = await Lawyer().GetAsync("/api/correspondence/urgent-unseen-count");
        var beforeText = await bellBefore.Content.ReadAsStringAsync();
        Assert.Equal(1, JsonDocument.Parse(beforeText).RootElement.GetProperty("count").GetInt32());
        var seen = await Lawyer().PostAsync($"/api/correspondence/{id}/mark-seen", Json(new { }));
        Assert.Equal(HttpStatusCode.OK, seen.StatusCode);
        var bellAfter = await Lawyer().GetAsync("/api/correspondence/urgent-unseen-count");
        var afterText = await bellAfter.Content.ReadAsStringAsync();
        Assert.Equal(0, JsonDocument.Parse(afterText).RootElement.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task NonPartyHead_CannotReply_ButHeadOfGovernorate_CanView()
    {
        var create = await Lawyer().PostAsync("/api/correspondence", Json(new
        {
            documentId = (int?)null,
            targetUserId = _delegateId,
            importance = "normal",
            bodyHtml = "<p>للعلم</p>",
        }));
        var id = (await ReadJsonAsync(create)).GetProperty("id").GetInt32();

        var reply = await Head().PostAsync($"/api/correspondence/{id}/replies",
            Json(new { bodyHtml = "<p>رد الرئيس</p>" }));
        Assert.Equal(HttpStatusCode.Forbidden, reply.StatusCode);

        var view = await Head().GetAsync($"/api/correspondence/{id}");
        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotAccessCorrespondence()
    {
        var response = await _factory.CreateClient().GetAsync("/api/correspondence");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
