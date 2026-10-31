using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using static DocGenerator.Api.Tests.TestHelpers.ApiFixtureExtensions;

using DocGenerator.Api.Tests.TestHelpers;
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// تكامل عقود PR-4 (الخطة §9.1–§9.3): قوائم مرقّمة بملخصات لا تحمل النص الكامل،
/// وتفاصيل مفردة وحدها تحمله، وقائمة مستلمين منفصلة، وصلاحيات الأدوار كما هي
/// معرّفة في الخدمة والمتحكم (بلا تسريب وجود عبر 404).
/// </summary>
[Collection(ApiTestCollection.Name)]
public class AlertsIntegrationTests
{
    private readonly ApiFactory _factory;

    public AlertsIntegrationTests(ApiFactory factory) => _factory = factory;

    private static async Task<int> CreateDocumentForLawyerAsync(ApiFactory factory, string username)
    {
        var login = await factory.LoginAsync(username, "123456");
        return await factory.CreateDocumentAsync(login!.Token!, borrowerName: "مقترض");
    }

    private static async Task<PagedResult<T>> ReadPageAsync<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<T>>();
        Assert.NotNull(page);
        return page!;
    }

    private static async Task<PagedResult<HeadAlertSummaryDto>> GetAlertsAsync(
        HttpClient client, string query = "")
        => await ReadPageAsync<HeadAlertSummaryDto>(await client.GetAsync("/api/alerts" + query));

    private static async Task<HeadAlertSummaryDto> CreateAlertAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/alerts", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<HeadAlertSummaryDto>();
        Assert.NotNull(summary);
        return summary!;
    }

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("message").GetString()!;
    }

    private async Task DeactivateUserAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        user.IsActive = false;
        await db.SaveChangesAsync();
    }

    private async Task<int> SeedAlertDirectlyAsync(
        int branchId, int createdById, int recipientUserId, string message)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var alert = new HeadAlert
        {
            BranchId = branchId,
            CreatedById = createdById,
            TargetType = HeadAlertTargetType.Lawyer,
            Kind = HeadAlertKindCatalog.ManualToLawyer,
            TargetLawyerId = recipientUserId,
            Message = message,
            CreatedAt = DateTime.UtcNow,
            LastContentChangedAt = DateTime.UtcNow,
            Recipients = new List<HeadAlertRecipient> { new() { UserId = recipientUserId } },
        };
        db.HeadAlerts.Add(alert);
        await db.SaveChangesAsync();
        return alert.Id;
    }

    private async Task<int> SeedDelegationAsync(int sourceDocumentId, int createdById)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var delegation = new DocumentDelegation
        {
            SourceDocumentId = sourceDocumentId,
            CreatedById = createdById,
            Status = DelegationStatusCatalog.PendingHead,
            DelegatedCourt = "دائرة تنفيذ",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.DocumentDelegations.Add(delegation);
        await db.SaveChangesAsync();
        return delegation.Id;
    }

    [Fact]
    public async Task CreateAlert_NonHeadRoles_Forbidden()
    {
        foreach (var username in new[] { "manager", "admin", "lawyer1" })
        {
            var client = _factory.AuthorizedClient(username);
            var response = await client.PostAsJsonAsync("/api/alerts", new
            {
                targetType = "branch",
                documentId = (int?)null,
                targetLawyerId = (int?)null,
                message = "تعميم",
            });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task CreateAlert_HeadTarget_Rejected()
    {
        var head = _factory.AuthorizedClient("head1");
        var response = await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "head",
            documentId = (int?)null,
            targetLawyerId = (int?)null,
            message = "تنبيه موجّه لرئيس",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAlert_Returns201SummaryBody_AndLocationToFullDetail()
    {
        var docId = await CreateDocumentForLawyerAsync(_factory, "lawyer1");
        var head = _factory.AuthorizedClient("head1");

        var response = await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "document",
            documentId = docId,
            targetLawyerId = (int?)null,
            message = "راجع ملف القرض هذا",
        });

        // D3: 201 بجسم ملخص الصف، ورابط التفاصيل في Location.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<HeadAlertSummaryDto>();
        Assert.NotNull(created);
        var fullMessage = HeadAlertMessages.ManualMessage(UserRole.Head, "راجع ملف القرض هذا");
        Assert.Equal(fullMessage, created!.MessagePreview);
        Assert.Equal(HeadAlertTextLimits.CountCodePoints(fullMessage), created.MessageLength);
        Assert.False(created.PreviewTruncated);
        Assert.Equal("document", created.TargetType);
        Assert.Equal(docId, created.DocumentId);
        Assert.Equal(1, created.RecipientCount);
        Assert.Equal(1, created.UnreadCount);
        Assert.Null(created.IsRead); // المنشئ ليس مستلمًا
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/alerts/{created.Id}", response.Headers.Location!.ToString());

        // التفاصيل وحدها تحمل النص الكامل.
        var detailResponse = await head.GetAsync($"/api/alerts/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<HeadAlertDto>();
        Assert.Equal(fullMessage, detail!.Message);
        Assert.True(detail.CanViewRecipients);

        // D3: الملخص العائد في 201 يطابق التفاصيل في العرض المشتق من العلاقات —
        // لا أسماء فارغة ولا «مستند محذوف» لكيان أُنشئ للتو.
        Assert.Equal(detail.DocumentTitle, created.DocumentTitle);
        Assert.False(string.IsNullOrWhiteSpace(created.DocumentTitle));
        Assert.NotEqual("مستند محذوف", created.DocumentTitle);
        Assert.Equal(detail.CreatedByName, created.CreatedByName);
        Assert.False(string.IsNullOrWhiteSpace(created.CreatedByName));
    }

    [Fact]
    public async Task CreateAlert_BlankExistingDocument_UsesIdFallback_In201DetailAndSentList()
    {
        var docId = await CreateDocumentForLawyerAsync(_factory, "lawyer1");

        // محاكاة صف قديم سبق اشتقاق DocumentType (الخدمة تضبطه دائمًا اليوم):
        // الحقول الأربعة NULL والملف قائم ⇒ «مستند {id}» لا «مستند محذوف».
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            await db.Documents
                .Where(d => d.Id == docId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.DocumentType, (string?)null)
                    .SetProperty(d => d.BorrowerName, (string?)null)
                    .SetProperty(d => d.BorrowerFather, (string?)null)
                    .SetProperty(d => d.BorrowerFamily, (string?)null));
        }

        var head = _factory.AuthorizedClient("head1");
        var created = await CreateAlertAsync(head, new
        {
            targetType = "document",
            documentId = docId,
            targetLawyerId = (int?)null,
            message = "راجع هذا الملف",
        });

        var expected = $"مستند {docId}";
        Assert.Equal(expected, created.DocumentTitle);

        var detailResponse = await head.GetAsync($"/api/alerts/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<HeadAlertDto>();
        Assert.Equal(expected, detail!.DocumentTitle);

        var sent = await ReadPageAsync<HeadAlertSummaryDto>(await head.GetAsync("/api/alerts/sent"));
        Assert.Equal(expected, Assert.Single(sent.Items, a => a.Id == created.Id).DocumentTitle);
    }

    [Fact]
    public async Task Head_CreatesDocumentAlert_LawyerListIsPagedSummaryWithoutFullMessage()
    {
        var docId = await CreateDocumentForLawyerAsync(_factory, "lawyer1");
        var head = _factory.AuthorizedClient("head1");

        var created = await CreateAlertAsync(head, new
        {
            targetType = "document",
            documentId = docId,
            targetLawyerId = (int?)null,
            message = "راجع ملف القرض هذا",
        });

        var lawyer = _factory.AuthorizedClient("lawyer1");
        var page = await GetAlertsAsync(lawyer, "?page=1&pageSize=100");

        // شكل الصفحة الصريح (PagedResult) — لا المصفوفة القديمة.
        Assert.Equal(1, page.Page);
        Assert.Equal(100, page.PerPage);
        Assert.True(page.TotalCount >= 1);
        Assert.Equal((int)Math.Ceiling(page.TotalCount / 100.0), page.TotalPages);

        var item = Assert.Single(page.Items, a => a.Id == created.Id);
        var fullMessage = HeadAlertMessages.ManualMessage(UserRole.Head, "راجع ملف القرض هذا");
        Assert.Equal(fullMessage, item.MessagePreview);
        Assert.False(item.IsRead);
        Assert.Null(item.RecipientCount); // المحامي لا يرى عدّادات المستلمين

        // الفحص الخام: لا مفتاح message في الملخص، والمعاينة موجودة.
        var raw = await (await lawyer.GetAsync("/api/alerts?page=1&pageSize=100")).Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(raw);
        Assert.True(json.RootElement.TryGetProperty("items", out var items));
        Assert.True(json.RootElement.TryGetProperty("page", out _));
        Assert.True(json.RootElement.TryGetProperty("perPage", out _));
        Assert.True(json.RootElement.TryGetProperty("totalCount", out _));
        Assert.True(json.RootElement.TryGetProperty("totalPages", out _));
        var element = items.EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == created.Id);
        Assert.False(element.TryGetProperty("message", out _));
        Assert.True(element.TryGetProperty("messagePreview", out var preview));
        Assert.Equal(fullMessage, preview.GetString());

        // التفاصيل: النص الكامل هنا.
        var detail = await (await lawyer.GetAsync($"/api/alerts/{created.Id}"))
            .Content.ReadFromJsonAsync<HeadAlertDto>();
        Assert.Equal(fullMessage, detail!.Message);
        Assert.False(detail.CanViewRecipients);
        Assert.False(detail.IsRead);
    }

    [Fact]
    public async Task Head_CreatesDocumentAlert_OutsideBranch_BadRequest()
    {
        var aleppoId = await _factory.GetBranchIdAsync("ALP");
        var alpLawyer = await _factory.CreateUserAsync(UniqueName("lawyer_alp"), UserRole.Lawyer, aleppoId);
        var login = await _factory.LoginAsync(alpLawyer.Username, "123456");
        var docId = await _factory.CreateDocumentAsync(login!.Token!, borrowerName: "مقترض حلب");

        var head = _factory.AuthorizedClient("head1");
        var response = await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "document",
            documentId = docId,
            targetLawyerId = (int?)null,
            message = "ملف من فرع آخر",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var outsideBranchMessage = await ReadMessageAsync(response);
        Assert.Equal("الملف غير متاح", outsideBranchMessage);

        // P3-5: الملف غير الموجود يعطي النص نفسه — لا تسريب وجود ملف فرع آخر.
        var missing = await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "document",
            documentId = int.MaxValue,
            targetLawyerId = (int?)null,
            message = "ملف غير موجود",
        });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(outsideBranchMessage, await ReadMessageAsync(missing));
    }

    [Fact]
    public async Task Head_CreatesLawyerAlert_InactiveTarget_BadRequest()
    {
        // ف‑3: سلوك واحد لكل اختبار — محامٍ هدف معطّل ضمن الفرع نفسه.
        var damId = await _factory.GetBranchIdAsync("DAM");
        var head = _factory.AuthorizedClient("head1");
        var inactive = await _factory.CreateUserAsync(
            UniqueName("lawyer_off"), UserRole.Lawyer, damId, isActive: false);

        var response = await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "lawyer",
            documentId = (int?)null,
            targetLawyerId = inactive.Id,
            message = "إلى محامٍ معطّل",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("المحامي غير موجود ضمن فرعك", await ReadMessageAsync(response));
    }

    [Fact]
    public async Task Head_CreatesDocumentAlert_InactiveCreator_BadRequest()
    {
        // ف‑3: سلوك واحد لكل اختبار — منشئ الملف صار معطّلًا بعد إنشاء الملف.
        var damId = await _factory.GetBranchIdAsync("DAM");
        var head = _factory.AuthorizedClient("head1");
        var creator = await _factory.CreateUserAsync(UniqueName("lawyer_cr"), UserRole.Lawyer, damId);
        var creatorLogin = await _factory.LoginAsync(creator.Username, "123456");
        var docId = await _factory.CreateDocumentAsync(creatorLogin!.Token!, borrowerName: "مقترض معطّل");
        await DeactivateUserAsync(creator.Id);

        var response = await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "document",
            documentId = docId,
            targetLawyerId = (int?)null,
            message = "إلى منشئ معطّل",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("لا يوجد محامٍ مختص نشط لهذا الملف", await ReadMessageAsync(response));
    }

    [Fact]
    public async Task Head_CreatesLawyerAlert_OnlyTargetReceives()
    {
        var damId = await _factory.GetBranchIdAsync("DAM");
        var target = await _factory.CreateUserAsync(UniqueName("lawyer_extra"), UserRole.Lawyer, damId);
        var head = _factory.AuthorizedClient("head1");

        var created = await CreateAlertAsync(head, new
        {
            targetType = "lawyer",
            documentId = (int?)null,
            targetLawyerId = target.Id,
            message = "رسالة خاصة لك",
        });

        var targetPage = await GetAlertsAsync(_factory.AuthorizedClient(target.Username), "?pageSize=100");
        var targetItem = Assert.Single(targetPage.Items, a => a.Id == created.Id);
        Assert.Equal(target.FullName, created.TargetLawyerName);
        Assert.Equal(created.TargetLawyerName, targetItem.TargetLawyerName);

        var otherPage = await GetAlertsAsync(_factory.AuthorizedClient("lawyer1"), "?pageSize=100");
        Assert.DoesNotContain(otherPage.Items, a => a.Id == created.Id);
    }

    [Fact]
    public async Task Head_CreatesBranchBroadcast_ReachesAllBranchLawyers_WithBaseCounters()
    {
        var branchId = await _factory.CreateBranchAsync("بث");
        var headName = (await _factory.CreateUserAsync(UniqueName("head_bx"), UserRole.Head, branchId)).Username;
        var first = await _factory.CreateUserAsync(UniqueName("lawyer_bx"), UserRole.Lawyer, branchId);
        var second = await _factory.CreateUserAsync(UniqueName("lawyer_bx"), UserRole.Lawyer, branchId);
        var head = _factory.AuthorizedClient(headName);

        var created = await CreateAlertAsync(head, new
        {
            targetType = "branch",
            documentId = (int?)null,
            targetLawyerId = (int?)null,
            message = "اجتماع الفرع يوم الأحد",
        });
        Assert.Equal(2, created.RecipientCount);
        Assert.Equal(2, created.UnreadCount);

        foreach (var lawyer in new[] { first, second })
        {
            var page = await GetAlertsAsync(_factory.AuthorizedClient(lawyer.Username));
            var item = Assert.Single(page.Items);
            Assert.Equal(created.Id, item.Id);
            Assert.False(item.IsRead);
            Assert.Null(item.RecipientCount);
            Assert.Null(item.UnreadCount);
        }
    }

    [Fact]
    public async Task Head_ListsOnlyOwnRecipientAlerts_WithSummaryReadState()
    {
        var branchId = await _factory.CreateBranchAsync("إحصاء");
        var headName = (await _factory.CreateUserAsync(UniqueName("head_st"), UserRole.Head, branchId)).Username;
        var first = await _factory.CreateUserAsync(UniqueName("lawyer_st"), UserRole.Lawyer, branchId);
        await _factory.CreateUserAsync(UniqueName("lawyer_st"), UserRole.Lawyer, branchId);

        var docLogin = await _factory.LoginAsync(first.Username, "123456");
        var docId = await _factory.CreateDocumentAsync(docLogin!.Token!, borrowerName: "مقترض");
        var head = _factory.AuthorizedClient(headName);

        await CreateAlertAsync(head, new
        {
            targetType = "document",
            documentId = docId,
            targetLawyerId = (int?)null,
            message = "تنبيه أول",
        });
        await CreateAlertAsync(head, new
        {
            targetType = "branch",
            documentId = (int?)null,
            targetLawyerId = (int?)null,
            message = "تعميم",
        });
        var headId = await _factory.UserIdAsync(headName);
        var ownId = await HeadAlertTestSeed.SeedSingleHeadAsync(
            _factory, branchId, headId, headId, "لرئيس القسم");

        // قراءة بالمستلم (§8): الرئيس يرى تنبيهه فقط — لا تنبيهات المحامين ولا التعميم.
        var page = await GetAlertsAsync(head, "?pageSize=100");

        var single = Assert.Single(page.Items);
        Assert.Equal(ownId, single.Id);
        Assert.Equal("لرئيس القسم", single.MessagePreview);
        Assert.False(single.IsRead);
        Assert.Equal(1, single.RecipientCount);
        Assert.Equal(1, single.UnreadCount);
    }

    [Fact]
    public async Task Lawyer_MarksAlertRead_UnreadCountDrops_AndSummaryReflectsIt()
    {
        var docId = await CreateDocumentForLawyerAsync(_factory, "lawyer1");
        var head = _factory.AuthorizedClient("head1");
        var created = await CreateAlertAsync(head, new
        {
            targetType = "document",
            documentId = docId,
            targetLawyerId = (int?)null,
            message = "تنبيه للمحامي",
        });

        var lawyer = _factory.AuthorizedClient("lawyer1");
        var before = await (await lawyer.GetAsync("/api/alerts/unread-count")).Content.ReadFromJsonAsync<UnreadCountDto>();
        Assert.NotNull(before);

        var mark = await lawyer.PatchAsJsonAsync($"/api/alerts/{created.Id}/read", new { });
        Assert.Equal(HttpStatusCode.NoContent, mark.StatusCode);

        var after = await (await lawyer.GetAsync("/api/alerts/unread-count")).Content.ReadFromJsonAsync<UnreadCountDto>();
        Assert.Equal(before!.Count - 1, after!.Count);

        var page = await GetAlertsAsync(lawyer, "?pageSize=100");
        Assert.True(Assert.Single(page.Items, a => a.Id == created.Id).IsRead);
    }

    [Fact]
    public async Task Head_MarksOwnAlertRead_Succeeds_OthersNotFound()
    {
        var branchId = await _factory.CreateBranchAsync("قراءة رئيس");
        var headName = (await _factory.CreateUserAsync(UniqueName("head_rd"), UserRole.Head, branchId)).Username;
        var lawyer = await _factory.CreateUserAsync(UniqueName("lawyer_rd"), UserRole.Lawyer, branchId);
        var head = _factory.AuthorizedClient(headName);

        var ownId = await HeadAlertTestSeed.SeedSingleHeadAsync(
            _factory, branchId, await _factory.UserIdAsync(headName), await _factory.UserIdAsync(headName),
            "تنبيه الرئيس الخاص");
        var others = await CreateAlertAsync(head, new
        {
            targetType = "lawyer",
            documentId = (int?)null,
            targetLawyerId = lawyer.Id,
            message = "تنبيه المحامي الخاص",
        });

        // المستلم (ولو رئيسًا) يعلّم تنبيهه مقروءًا؛ وغير المستلم 404.
        Assert.Equal(HttpStatusCode.NoContent,
            (await head.PatchAsJsonAsync($"/api/alerts/{ownId}/read", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await head.PatchAsJsonAsync($"/api/alerts/{others.Id}/read", new { })).StatusCode);

        var count = await (await head.GetAsync("/api/alerts/unread-count"))
            .Content.ReadFromJsonAsync<UnreadCountDto>();
        Assert.Equal(0, count!.Count);
    }

    [Fact]
    public async Task NonHeadNonLawyer_MarkRead_Forbidden()
    {
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.PatchAsJsonAsync("/api/alerts/1/read", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnreadCount_ManagerAdmin_Forbidden_HeadAllowed()
    {
        foreach (var username in new[] { "manager", "admin" })
        {
            var client = _factory.AuthorizedClient(username);
            var response = await client.GetAsync("/api/alerts/unread-count");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        var head = _factory.AuthorizedClient("head1");
        Assert.Equal(HttpStatusCode.OK, (await head.GetAsync("/api/alerts/unread-count")).StatusCode);
    }

    [Fact]
    public async Task Get_Alert_LawyerRecipient_ReturnsFullDetail()
    {
        var docId = await CreateDocumentForLawyerAsync(_factory, "lawyer1");
        var head = _factory.AuthorizedClient("head1");
        var created = await CreateAlertAsync(head, new
        {
            targetType = "document",
            documentId = docId,
            targetLawyerId = (int?)null,
            message = "تنبيه للجلب",
        });

        var lawyer = _factory.AuthorizedClient("lawyer1");
        var response = await lawyer.GetAsync($"/api/alerts/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var alert = await response.Content.ReadFromJsonAsync<HeadAlertDto>();
        Assert.Equal(created.Id, alert!.Id);
        Assert.Equal("document", alert.TargetType);
        Assert.Equal(HeadAlertMessages.ManualMessage(UserRole.Head, "تنبيه للجلب"), alert.Message);
    }

    [Fact]
    public async Task Get_Alert_RecipientOutsideBranch_IsAllowed_ByAbsolutePriority()
    {
        // قرار D2: المستلم بأولوية مطلقة بلا شرط فرع (تنبيهات الإنابة الخارجية).
        var outsideBranch = await _factory.CreateBranchAsync("فرع خارجي");
        var lawyerId = await _factory.UserIdAsync("lawyer1");
        var headOutsideId = (await _factory.CreateUserAsync(
            UniqueName("head_xb"), UserRole.Head, outsideBranch)).Id;
        var alertId = await SeedAlertDirectlyAsync(outsideBranch, headOutsideId, lawyerId, "تنبيه من فرع آخر");

        var response = await _factory.AuthorizedClient("lawyer1").GetAsync($"/api/alerts/{alertId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var alert = await response.Content.ReadFromJsonAsync<HeadAlertDto>();
        Assert.Equal("تنبيه من فرع آخر", alert!.Message);
        Assert.False(alert.CanViewRecipients);
        Assert.False(alert.IsRead);
    }

    [Fact]
    public async Task Get_Alert_LawyerNonRecipient_NotFound()
    {
        var branchId = await _factory.CreateBranchAsync("جلب");
        var headName = (await _factory.CreateUserAsync(UniqueName("head_get"), UserRole.Head, branchId)).Username;
        var target = await _factory.CreateUserAsync(UniqueName("lawyer_get"), UserRole.Lawyer, branchId);
        var other = await _factory.CreateUserAsync(UniqueName("lawyer_get2"), UserRole.Lawyer, branchId);
        var head = _factory.AuthorizedClient(headName);

        var created = await CreateAlertAsync(head, new
        {
            targetType = "lawyer",
            documentId = (int?)null,
            targetLawyerId = target.Id,
            message = "خاص بالجلب",
        });

        var otherClient = _factory.AuthorizedClient(other.Username);
        var response = await otherClient.GetAsync($"/api/alerts/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_Alert_HeadRecipient_ReturnsIt_OthersNotFound()
    {
        var branchId = await _factory.CreateBranchAsync("جلب رئيس");
        var headName = (await _factory.CreateUserAsync(UniqueName("head_g2"), UserRole.Head, branchId)).Username;
        var lawyer = await _factory.CreateUserAsync(UniqueName("lawyer_g3"), UserRole.Lawyer, branchId);
        var head = _factory.AuthorizedClient(headName);

        var ownId = await HeadAlertTestSeed.SeedSingleHeadAsync(
            _factory, branchId, await _factory.UserIdAsync(headName), await _factory.UserIdAsync(headName),
            "جلب رئيس");
        // منشئه محامٍ ومستلمه محامٍ: رئيس الفرع ليس طرفًا فيه → 404.
        var othersId = await SeedAlertDirectlyAsync(branchId, lawyer.Id, lawyer.Id, "خاص بالمحامي");

        Assert.Equal(HttpStatusCode.OK, (await head.GetAsync($"/api/alerts/{ownId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await head.GetAsync($"/api/alerts/{othersId}")).StatusCode);
    }

    [Fact]
    public async Task Get_Alert_HeadOfAnotherBranch_NotFound()
    {
        var branchA = await _factory.CreateBranchAsync("ألف");
        var headA = (await _factory.CreateUserAsync(UniqueName("head_a"), UserRole.Head, branchA)).Username;
        var lawyerA = await _factory.CreateUserAsync(UniqueName("lawyer_a"), UserRole.Lawyer, branchA);
        var clientA = _factory.AuthorizedClient(headA);
        var created = await CreateAlertAsync(clientA, new
        {
            targetType = "lawyer",
            documentId = (int?)null,
            targetLawyerId = lawyerA.Id,
            message = "تنبيه ألف",
        });

        var branchB = await _factory.CreateBranchAsync("باء");
        var headB = (await _factory.CreateUserAsync(UniqueName("head_b"), UserRole.Head, branchB)).Username;
        var clientB = _factory.AuthorizedClient(headB);

        var response = await clientB.GetAsync($"/api/alerts/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_Alert_Manager_ReturnsIt_Missing_ReturnsNotFound()
    {
        // قرار المالك 2026-10-16: قراءة إشرافية للمدير/المشرف (المفرد والقائمة فقط).
        var docId = await CreateDocumentForLawyerAsync(_factory, "lawyer1");
        var head = _factory.AuthorizedClient("head1");
        var created = await CreateAlertAsync(head, new
        {
            targetType = "document",
            documentId = docId,
            targetLawyerId = (int?)null,
            message = "تنبيه للإشراف",
        });

        foreach (var username in new[] { "manager", "admin" })
        {
            var client = _factory.AuthorizedClient(username);
            var response = await client.GetAsync($"/api/alerts/{created.Id}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var alert = await response.Content.ReadFromJsonAsync<HeadAlertDto>();
            Assert.Equal(HeadAlertMessages.ManualMessage(UserRole.Head, "تنبيه للإشراف"), alert!.Message);
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.GetAsync("/api/alerts/999999")).StatusCode);
        }
    }

    [Fact]
    public async Task Recipients_CreatorAndOversightSeeList_RecipientAndOutsiderGetNotFound()
    {
        var branchId = await _factory.GetBranchIdAsync("DAM");
        var target = await _factory.CreateUserAsync(UniqueName("lawyer_rc"), UserRole.Lawyer, branchId);
        var head = _factory.AuthorizedClient("head1");
        var created = await CreateAlertAsync(head, new
        {
            targetType = "lawyer",
            documentId = (int?)null,
            targetLawyerId = target.Id,
            message = "تنبيه لقائمة المستلمين",
        });

        var creatorResponse = await head.GetAsync($"/api/alerts/{created.Id}/recipients");
        Assert.Equal(HttpStatusCode.OK, creatorResponse.StatusCode);
        var recipients = await creatorResponse.Content.ReadFromJsonAsync<List<HeadAlertRecipientDto>>();
        var row = Assert.Single(recipients!);
        Assert.Equal(target.Id, row.UserId);
        Assert.Equal(target.FullName, row.FullName);
        Assert.Equal("lawyer", row.Role);
        Assert.False(row.IsRead);
        Assert.Null(row.ReadAt);

        var managerResponse = await _factory.AuthorizedClient("manager")
            .GetAsync($"/api/alerts/{created.Id}/recipients");
        Assert.Equal(HttpStatusCode.OK, managerResponse.StatusCode);

        // المستلم لا يرى قائمة المستلمين، وغير المنشئ/الإشراف يُترجم إلى 404.
        var recipientResponse = await _factory.AuthorizedClient(target.Username)
            .GetAsync($"/api/alerts/{created.Id}/recipients");
        Assert.Equal(HttpStatusCode.NotFound, recipientResponse.StatusCode);
        var outsiderResponse = await _factory.AuthorizedClient("lawyer1")
            .GetAsync($"/api/alerts/{created.Id}/recipients");
        Assert.Equal(HttpStatusCode.NotFound, outsiderResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await head.GetAsync("/api/alerts/999999/recipients")).StatusCode);
    }

    [Fact]
    public async Task Manager_ListsAlerts_SeesCrossBranchAlert_LawyerDoesNot()
    {
        var branchId = await _factory.CreateBranchAsync("حمص-إشراف");
        var alpLawyer = await _factory.CreateUserAsync(UniqueName("lawyer_ov"), UserRole.Lawyer, branchId);
        var headName = (await _factory.CreateUserAsync(UniqueName("head_ov"), UserRole.Head, branchId)).Username;
        var headAlp = _factory.AuthorizedClient(headName);

        var created = await CreateAlertAsync(headAlp, new
        {
            targetType = "lawyer",
            documentId = (int?)null,
            targetLawyerId = alpLawyer.Id,
            message = "تنبيه إشرافي خاص",
        });

        // المدير يرى عبر الفروع؛ محامي فرع آخر لا يرى.
        var manager = _factory.AuthorizedClient("manager");
        var list = await GetAlertsAsync(manager, "?pageSize=100");
        var item = Assert.Single(list.Items, a => a.Id == created.Id);
        // الإسقاط العرضي: العدّادات محسوبة قاعديًا دون تحميل المستلمين،
        // وحالة القراءة فارغة (مستلم-محددة أصلًا).
        Assert.Equal(1, item.RecipientCount);
        Assert.Equal(1, item.UnreadCount);
        Assert.Null(item.IsRead);

        var lawyerDam = _factory.AuthorizedClient("lawyer1");
        var scoped = await GetAlertsAsync(lawyerDam, "?pageSize=100");
        Assert.DoesNotContain(scoped.Items, a => a.Id == created.Id);
    }

    [Fact]
    public async Task Lawyer_ListsAlerts_DoesNotSeeOthersBranchesAlerts()
    {
        var branchId = await _factory.CreateBranchAsync("حلب2");
        var headName = (await _factory.CreateUserAsync(UniqueName("head_al2"), UserRole.Head, branchId)).Username;
        var alpLawyer = await _factory.CreateUserAsync(UniqueName("lawyer_al2"), UserRole.Lawyer, branchId);
        var headAlp = _factory.AuthorizedClient(headName);

        var created = await CreateAlertAsync(headAlp, new
        {
            targetType = "lawyer",
            documentId = (int?)null,
            targetLawyerId = alpLawyer.Id,
            message = "تنبيه خاص بحلب",
        });

        var lawyerDam = _factory.AuthorizedClient("lawyer1");
        var list = await GetAlertsAsync(lawyerDam, "?pageSize=100");
        Assert.DoesNotContain(list.Items, a => a.Id == created.Id);
    }

    [Fact]
    public async Task Sent_HeadSeesOwnIssued_PagedShape_ManagerAndAdminForbidden()
    {
        var damId = await _factory.GetBranchIdAsync("DAM");
        var target = await _factory.CreateUserAsync(UniqueName("lawyer_sent"), UserRole.Lawyer, damId);
        var head = _factory.AuthorizedClient("head1");

        var created = await CreateAlertAsync(head, new
        {
            targetType = "lawyer",
            documentId = (int?)null,
            targetLawyerId = target.Id,
            message = "تعميم صادر",
        });

        var sentResponse = await head.GetAsync("/api/alerts/sent");
        Assert.Equal(HttpStatusCode.OK, sentResponse.StatusCode);
        var sent = await ReadPageAsync<HeadAlertSummaryDto>(sentResponse);
        Assert.Equal(1, sent.Page);
        Assert.Equal(AlertPageRequest.DefaultPageSize, sent.PerPage);
        var item = Assert.Single(sent.Items, a => a.Id == created.Id);
        Assert.Equal(HeadAlertMessages.ManualMessage(UserRole.Head, "تعميم صادر"), item.MessagePreview);
        Assert.Equal(1, item.RecipientCount);
        Assert.Equal(1, item.UnreadCount);
        Assert.Null(item.IsRead);

        // قائمة المستلَمات لا تشمله (قراءة المستلم لا تضم المُرسِل).
        var received = await GetAlertsAsync(head, "?pageSize=100");
        Assert.DoesNotContain(received.Items, a => a.Id == created.Id);

        foreach (var username in new[] { "lawyer1", "manager", "admin" })
        {
            var response = await _factory.AuthorizedClient(username).GetAsync("/api/alerts/sent");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            // التفويض يسبق التحقق من الشكل: ترقيم غير صالح لا يحوّل 403 إلى 400
            // (مراجعة المرحلة 3 `ح‑13`).
            Assert.Equal(HttpStatusCode.Forbidden,
                (await _factory.AuthorizedClient(username)
                    .GetAsync("/api/alerts/sent?page=0")).StatusCode);
        }
    }

    [Fact]
    public async Task List_ManagerOrAdminUnreadOnly_BadRequest()
    {
        const string expected = "خيار «غير المقروء» غير متاح للمدير/المشرف";

        foreach (var username in new[] { "manager", "admin" })
        {
            // الترقيم غير الصالح لا يسبق رسالة «غير المقروء» (تفويض ← قاعدة ← شكل).
            var response = await _factory.AuthorizedClient(username)
                .GetAsync("/api/alerts?unreadOnly=true&page=0");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            // النص عربي نظيف: لا تُلحق رسالة `ArgumentException` اسم المعامل الإنجليزي
            // (مراجعة المرحلة 3 `ح‑12`).
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(expected, json.RootElement.GetProperty("message").GetString());
        }
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    public async Task List_InvalidPaging_BadRequest(string query)
    {
        var manager = _factory.AuthorizedClient("manager");
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.GetAsync("/api/alerts" + query)).StatusCode);

        var head = _factory.AuthorizedClient("head1");
        Assert.Equal(HttpStatusCode.BadRequest, (await head.GetAsync("/api/alerts/sent" + query)).StatusCode);
    }

    [Fact]
    public async Task Paging_OverflowRejected_ExtremePageWithSize1Allowed()
    {
        // R-3/P3-6: الفيضان (page=int.MaxValue × pageSize=100) كان يلتفّ في `int`
        // (-200) فيرفضه حارس المستودع عرضًا؛ صار رفضًا صريحًا من العقد (400)
        // برسالة عربية نظيفة — لا «Parameter name» إنجليزي في جسم الاستجابة.
        const string overflowMessage =
            "رقم الصفحة كبير جدًا — الإزاحة الناتجة تتجاوز الحد الأقصى المسموح";
        var overflowQuery = $"?page={int.MaxValue}&pageSize={AlertPageRequest.MaxPageSize}";

        var manager = _factory.AuthorizedClient("manager");
        var overflow = await manager.GetAsync("/api/alerts" + overflowQuery);
        Assert.Equal(HttpStatusCode.BadRequest, overflow.StatusCode);
        var overflowBody = await ReadMessageAsync(overflow);
        Assert.Equal(overflowMessage, overflowBody);
        Assert.DoesNotContain("Parameter", overflowBody);

        var head = _factory.AuthorizedClient("head1");
        var sentOverflow = await head.GetAsync("/api/alerts/sent" + overflowQuery);
        Assert.Equal(HttpStatusCode.BadRequest, sentOverflow.StatusCode);
        Assert.Equal(overflowMessage, await ReadMessageAsync(sentOverflow));

        // كِبَر رقم الصفحة وحده ليس فيضانًا: Skip = 2,147,483,646 ≤ int.MaxValue
        // ⇒ 200 بعناصر فارغة (لا تغيير لسلوك صفحة بعيدة قائمة).
        var allowed = await manager.GetAsync($"/api/alerts?page={int.MaxValue}&pageSize=1");
        var page = await ReadPageAsync<HeadAlertSummaryDto>(allowed);
        Assert.Empty(page.Items);
        Assert.Equal(int.MaxValue, page.Page);
        Assert.Equal(1, page.PerPage);
    }

    [Fact]
    public async Task List_ArchivedFilter_AcceptsKnownValues_AndRejectsUnknown()
    {
        var created = await CreateAlertAsync(_factory.AuthorizedClient("head1"), new
        {
            targetType = "lawyer",
            documentId = (int?)null,
            targetLawyerId = await _factory.UserIdAsync("lawyer1"),
            message = "تنبيه أرشفة",
        });

        var all = await GetAlertsAsync(_factory.AuthorizedClient("manager"), "?archived=all&pageSize=100");
        Assert.Contains(all.Items, a => a.Id == created.Id);

        var onlyArchived = await GetAlertsAsync(_factory.AuthorizedClient("manager"), "?archived=only&pageSize=100");
        Assert.DoesNotContain(onlyArchived.Items, a => a.Id == created.Id);

        var invalid = await _factory.AuthorizedClient("manager").GetAsync("/api/alerts?archived=bad");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task ByDelegation_PartyLawyerGetsPagedShape_NonPartyAndNonLawyerForbidden()
    {
        var login = await _factory.LoginAsync("lawyer1", "123456");
        var docId = await _factory.CreateDocumentAsync(login!.Token!, borrowerName: "منيب");
        var lawyerId = await _factory.UserIdAsync("lawyer1");
        var delegationId = await SeedDelegationAsync(docId, lawyerId);

        // محامي الطرف: صفحة واحدة افتراضيًا (page=1/perPage=100 للشريط — D1).
        var party = _factory.AuthorizedClient("lawyer1");
        var response = await party.GetAsync($"/api/alerts/by-delegation/{delegationId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await ReadPageAsync<HeadAlertSummaryDto>(response);
        Assert.Equal(1, page.Page);
        Assert.Equal(AlertPageRequest.MaxPageSize, page.PerPage);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);

        // ترقيم صارم للطرف نفسه.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await party.GetAsync($"/api/alerts/by-delegation/{delegationId}?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await party.GetAsync($"/api/alerts/by-delegation/{delegationId}?pageSize=101")).StatusCode);

        // غير الطرف والمدير/المشرف ورئيس القسم: 403 (السلوك القائم).
        var outsider = await _factory.CreateUserAsync(
            UniqueName("lawyer_out"), UserRole.Lawyer, await _factory.GetBranchIdAsync("DAM"));
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _factory.AuthorizedClient(outsider.Username)
                .GetAsync($"/api/alerts/by-delegation/{delegationId}")).StatusCode);
        foreach (var username in new[] { "manager", "admin", "head1" })
        {
            Assert.Equal(HttpStatusCode.Forbidden,
                (await _factory.AuthorizedClient(username)
                    .GetAsync($"/api/alerts/by-delegation/{delegationId}")).StatusCode);

            // الدور غير المخوّل يبقى 403 حتى مع ترقيم غير صالح: الدور ← الشكل ← الطرفية.
            Assert.Equal(HttpStatusCode.Forbidden,
                (await _factory.AuthorizedClient(username)
                    .GetAsync($"/api/alerts/by-delegation/{delegationId}?page=0")).StatusCode);
        }

        // محامٍ غير طرف + ترقيم غير صالح ⇒ 400: التحقق من الشكل يسبق الطرفية عمدًا
        // (عقد `InvalidPaging_IsRejected`، الخطة §5 — لا تسريب نتيجة القائمة).
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _factory.AuthorizedClient(outsider.Username)
                .GetAsync($"/api/alerts/by-delegation/{delegationId}?page=0")).StatusCode);
    }

    [Fact]
    public async Task BranchlessHead_CreateIs400_AndReadsAre403()
    {
        // D11 (المرحلة 3): حارس الفرع انتقل من المتحكم إلى الخدمة، فأصبحت الحالات:
        // الكتابة بحساب رئيس بلا فرع 400 (عقد `POST /api/alerts` القائم)، وأسطح
        // القراءة/العدّ 403 — قبل التوحيد كان العدّاد يعيد 200 بلا أي نطاق.
        //
        // الحالة غير قابلة للتجسيد عبر EF (قيد `CK_Users_BranchRequiredForBranchRoles`)،
        // فتُسكّ جلسة شاذة صريحة من نسخة كيان حقيقي بلا فرع — نفس نمط
        // `ClientForUser` في حراس النقاط الأخرى، والتحقق من `TokenVersion` يبقى قائمًا.
        //
        // حدّ التغطية (مراجعة المرحلة 3 `ح‑17`): هذا يُثبت حارس **التوكن** في الخدمة؛
        // فرع «دفاع القاعدة» (`user.BranchId != branchId`) لا يُبلَغ هنا لأن
        // `RequireBranchForWrite` يرمي قبله — ويُغطّى باختبار تعارض الدور في الوحدة.
        User branchlessHead;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            var head = await db.Users.AsNoTracking().SingleAsync(u => u.Username == "head1");
            branchlessHead = new User
            {
                Id = head.Id,
                Username = head.Username,
                FullName = head.FullName,
                Role = UserRole.Head,
                BranchId = null,
                SectionId = head.SectionId,
                TokenVersion = head.TokenVersion,
                IsActive = true,
            };
        }

        var client = _factory.ClientForUser(branchlessHead);

        var create = await client.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "branch",
            documentId = (int?)null,
            targetLawyerId = (int?)null,
            message = "تنبيه من رئيس بلا فرع",
        });
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);

        foreach (var path in new[]
                 {
                     "/api/alerts",
                     "/api/alerts/sent",
                     "/api/alerts/unread-count",
                     "/api/alerts/unread-count?scope=user",
                     "/api/alerts/unread-count?scope=received",
                 })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        }

        // التفويض يسبق التحقق من الشكل: حتى الترقيم غير الصالح يبقى 403 لرئيس بلا فرع
        // (مراجعة المرحلة 3 `ح‑13` — كانت الخدمة تتحقق من الترقيم أولًا).
        foreach (var path in new[] { "/api/alerts?page=0", "/api/alerts/sent?pageSize=101" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        }
    }

    private sealed record UnreadCountDto(int Count);
}
