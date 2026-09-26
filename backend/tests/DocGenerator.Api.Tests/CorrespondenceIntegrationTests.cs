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
    private int _lawyer2Id;
    private int _headId;
    private int _delegateId;
    private int _documentId;

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
        var lawyer2 = mk("corrlawyer2", "المحامي الثاني", UserRole.Lawyer, branch.Id);
        var head = mk("corrhead", "الرئيس", UserRole.Head, branch.Id);
        var del = mk("corrdelegate", "المندوب", UserRole.EntityManager, null);
        db.Users.AddRange(lawyer, lawyer2, head, del);
        await db.SaveChangesAsync();

        del.PortalEntryId = entryId;
        await db.SaveChangesAsync();

        var document = new Document
        {
            BranchId = branch.Id,
            CreatedById = lawyer.Id,
            IsDraft = false,
            BorrowerName = "سعيد",
            BorrowerFather = "خالد",
            BorrowerFamily = "الزعيم",
            FileNumber = "12/2026",
            FileType = "تنفيذي",
            FileYear = "2026",
            Court = "دائرة تنفيذ دمشق",
            AmountNumeric = 0,
            ExecStatus = string.Empty,
        };
        document.ExecutionApplicants.Add(new ExecutionApplicant
        {
            Name = "وزارة التعليم",
            ApplicantNature = PartyNatureCatalog.Legal,
            RegistryId = entryId,
        });
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        _branchId = branch.Id;
        _lawyerId = lawyer.Id;
        _lawyer2Id = lawyer2.Id;
        _headId = head.Id;
        _delegateId = del.Id;
        _documentId = document.Id;
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

    /// <summary>
    /// بيان العقد المشترك (`frontend/src/test/contracts/correspondence-contracts.json`):
    /// مصدر الحقيقة الوحيد لأسماء حقول السلك. يُقرأ من الشجرة لا من مجلد البناء،
    /// فيعمل تحت `dotnet test` من أي دليل عمل.
    /// </summary>
    private static JsonElement ReadSharedContract(string section)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName, "frontend", "src", "test", "contracts",
                "correspondence-contracts.json");
            if (File.Exists(candidate))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(candidate));
                return doc.RootElement.GetProperty(section).Clone();
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException("بيان العقد المشترك غير موجود في الشجرة");
    }

    private static List<string> SortedKeys(JsonElement obj)
        => obj.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToList();

    [Fact]
    public async Task WireContract_DetailKeysMatchSharedManifest()
    {
        // أي حقل جديد/محذوف/مُعاد تسميته في `CorrespondenceDto` يُفشل هنا حتى
        // تُحدَّث الواجهة والبيان معًا — لا تباين C#↔TS بصمت.
        var create = await Lawyer().PostAsync("/api/correspondence", Json(new
        {
            documentId = (int?)null,
            targetUserId = _delegateId,
            importance = "normal",
            bodyHtml = "<p>عقد السلك</p>",
        }));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var body = await ReadJsonAsync(create);
        var expected = ReadSharedContract("detail")
            .EnumerateArray().Select(e => e.GetString()!).OrderBy(n => n).ToList();
        Assert.Equal(expected, SortedKeys(body));
    }

    [Fact]
    public async Task WireContract_ListItemKeysMatchSharedManifest()
    {
        var create = await Lawyer().PostAsync("/api/correspondence", Json(new
        {
            documentId = (int?)null,
            targetUserId = _delegateId,
            importance = "normal",
            bodyHtml = "<p>عقد سلك القائمة</p>",
        }));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var id = (await ReadJsonAsync(create)).GetProperty("id").GetInt32();

        var list = await ReadJsonAsync(
            await Lawyer().GetAsync("/api/correspondence?page=1&perPage=20"));
        var row = list.GetProperty("items").EnumerateArray()
            .First(e => e.GetProperty("id").GetInt32() == id);
        var expected = ReadSharedContract("list")
            .EnumerateArray().Select(e => e.GetString()!).OrderBy(n => n).ToList();
        Assert.Equal(expected, SortedKeys(row));
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
        Assert.False(body.GetProperty("canMarkSeen").GetBoolean());
        Assert.False(body.GetProperty("canReply").GetBoolean());
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

    [Fact]
    public async Task Delegate_OnFileLinked_TargetsUninvolvedLawyer_ReturnsBadRequest()
    {
        var response = await Delegate().PostAsync("/api/portal/correspondence", Json(new
        {
            documentId = _documentId,
            targetUserId = _lawyer2Id,
            importance = "normal",
            bodyHtml = "<p>للمحامي الثاني</p>",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Lawyer_OnFileLinked_TargetsHead_ReturnsBadRequest()
    {
        var response = await Lawyer().PostAsync("/api/correspondence", Json(new
        {
            documentId = _documentId,
            targetUserId = _headId,
            importance = "normal",
            bodyHtml = "<p>لرئيس القسم</p>",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delegate_OnFileLinked_TargetsOwnerLawyer_ReturnsCreated()
    {
        var response = await Delegate().PostAsync("/api/portal/correspondence", Json(new
        {
            documentId = _documentId,
            targetUserId = _lawyerId,
            importance = "normal",
            bodyHtml = "<p>استفسار عن الملف</p>",
        }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(_documentId, body.GetProperty("documentId").GetInt32());
        Assert.Equal(_lawyerId, body.GetProperty("targetUserId").GetInt32());
    }

    [Fact]
    public async Task MarkSeen_ForCreatorOrHead_ReturnsForbidden_AndTargetSucceeds()
    {
        // عقد `mark-seen` على السلك: المستلم فقط 403 لغيره، مع بقاء الحالة
        // «بانتظار» — الحجب على مستوى نقطة النهاية لا على زرّ في الواجهة.
        var create = await Lawyer().PostAsync("/api/correspondence", Json(new
        {
            documentId = (int?)null,
            targetUserId = _delegateId,
            importance = "urgent",
            bodyHtml = "<p>عاجل</p>",
        }));
        var id = (await ReadJsonAsync(create)).GetProperty("id").GetInt32();

        // حق التوثيق والرد على السلك قرار خادم لا زرّ: المنشئ والرئيس بلا حق، والمستلم وحده بحق.
        var creatorDetail = await ReadJsonAsync(
            await Lawyer().GetAsync($"/api/correspondence/{id}"));
        Assert.False(creatorDetail.GetProperty("canMarkSeen").GetBoolean());
        Assert.False(creatorDetail.GetProperty("canReply").GetBoolean());
        var headDetail = await ReadJsonAsync(
            await Head().GetAsync($"/api/correspondence/{id}"));
        Assert.False(headDetail.GetProperty("canMarkSeen").GetBoolean());
        Assert.False(headDetail.GetProperty("canReply").GetBoolean());

        // المنشئ (المحامي) لا يوثّق نيابةً عن المستلم.
        var byCreator = await Lawyer().PostAsync($"/api/correspondence/{id}/mark-seen", Json(new { }));
        Assert.Equal(HttpStatusCode.Forbidden, byCreator.StatusCode);

        // رئيس قسم المحافظة (قارئ مصرَّح) كذلك — يقرأ ولا يؤشّر.
        var byHead = await Head().PostAsync($"/api/correspondence/{id}/mark-seen", Json(new { }));
        Assert.Equal(HttpStatusCode.Forbidden, byHead.StatusCode);

        // الحالة لم تتغيّر بِرفض غير المستلم.
        var detailBefore = await ReadJsonAsync(
            await Delegate().GetAsync($"/api/portal/correspondence/{id}"));
        Assert.Equal("pending", detailBefore.GetProperty("viewStatus").GetString());
        Assert.True(detailBefore.GetProperty("canMarkSeen").GetBoolean());
        Assert.True(detailBefore.GetProperty("canReply").GetBoolean());
        Assert.Equal(0, detailBefore.GetProperty("receipts").GetArrayLength());

        // المستلم يوثّق بنجاح.
        var byTarget = await Delegate().PostAsync($"/api/portal/correspondence/{id}/mark-seen", Json(new { }));
        Assert.Equal(HttpStatusCode.OK, byTarget.StatusCode);

        var detailAfter = await ReadJsonAsync(
            await Delegate().GetAsync($"/api/portal/correspondence/{id}"));
        Assert.Equal("seen", detailAfter.GetProperty("viewStatus").GetString());
        Assert.True(detailAfter.GetProperty("canMarkSeen").GetBoolean());
        Assert.Equal(1, detailAfter.GetProperty("receipts").GetArrayLength());
    }

    [Fact]
    public async Task TargetsEndpoint_WithDocumentId_ReturnsScopeDelegatesOnly()
    {
        var response = await Lawyer().GetAsync(
            $"/api/correspondence/targets?q=مندوب&documentId={_documentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        var fullNames = body.EnumerateArray()
            .Select(t => t.GetProperty("fullName").GetString())
            .ToList();
        Assert.Single(fullNames);
        Assert.Equal("المندوب", fullNames[0]);
    }
}
