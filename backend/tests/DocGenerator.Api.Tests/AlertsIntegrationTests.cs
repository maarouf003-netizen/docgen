using System.Net;
using System.Net.Http.Json;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

[Collection(ApiTestCollection.Name)]
public class AlertsIntegrationTests
{
    private readonly ApiFactory _factory;

    public AlertsIntegrationTests(ApiFactory factory) => _factory = factory;

    private static string NewName(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 16, 40)];

    private Task<int> BranchIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return Task.FromResult(db.Branches.Single(b => b.Code == code).Id);
    }

    private async Task<int> CreateBranchAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var branch = new DocGenerator.Domain.Entities.Branch { Name = name, Code = $"BX_{Guid.NewGuid():N}"[..8].ToUpperInvariant() };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        return branch.Id;
    }

    private static async Task<int> CreateDocumentForLawyerAsync(ApiFactory factory, string username)
    {
        var login = await factory.LoginAsync(username, "123456");
        return await factory.CreateDocumentAsync(login!.Token!, borrowerName: "مقترض");
    }

    private async Task<int> UserIdAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        await Task.CompletedTask;
        return db.Users.Single(u => u.Username == username).Id;
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
    public async Task Head_CreatesDocumentAlert_LawyerReceivesIt()
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
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<HeadAlertDto>();
        Assert.NotNull(created);
        Assert.Equal("document", created.TargetType);
        Assert.Equal(docId, created.DocumentId);

        var lawyer = _factory.AuthorizedClient("lawyer1");
        var list = await (await lawyer.GetAsync("/api/alerts")).Content.ReadFromJsonAsync<List<HeadAlertDto>>();
        Assert.NotNull(list);
        Assert.Contains(list, a => a.Id == created.Id && a.Message == "راجع ملف القرض هذا");
    }

    [Fact]
    public async Task Head_CreatesDocumentAlert_OutsideBranch_BadRequest()
    {
        var aleppoId = await BranchIdAsync("ALP");
        var alpLawyer = await _factory.CreateUserAsync(NewName("lawyer_alp"), UserRole.Lawyer, aleppoId);
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
    }

    [Fact]
    public async Task Head_CreatesLawyerAlert_OnlyTargetReceives()
    {
        var damId = await BranchIdAsync("DAM");
        var target = await _factory.CreateUserAsync(NewName("lawyer_extra"), UserRole.Lawyer, damId);
        var head = _factory.AuthorizedClient("head1");

        var response = await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "lawyer",
            documentId = (int?)null,
            targetLawyerId = target.Id,
            message = "رسالة خاصة لك",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var targetClient = _factory.AuthorizedClient(target.Username);
        var targetList = await (await targetClient.GetAsync("/api/alerts")).Content.ReadFromJsonAsync<List<HeadAlertDto>>();
        Assert.Single(targetList!);

        var otherClient = _factory.AuthorizedClient("lawyer1");
        var otherList = await (await otherClient.GetAsync("/api/alerts")).Content.ReadFromJsonAsync<List<HeadAlertDto>>();
        Assert.DoesNotContain(otherList!, a => a.Message == "رسالة خاصة لك");
    }

    [Fact]
    public async Task Head_CreatesBranchBroadcast_ReachesAllBranchLawyers()
    {
        var branchId = await CreateBranchAsync("بث");
        var headName = (await _factory.CreateUserAsync(NewName("head_bx"), UserRole.Head, branchId)).Username;
        var first = await _factory.CreateUserAsync(NewName("lawyer_bx"), UserRole.Lawyer, branchId);
        var second = await _factory.CreateUserAsync(NewName("lawyer_bx"), UserRole.Lawyer, branchId);
        var head = _factory.AuthorizedClient(headName);

        var response = await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "branch",
            documentId = (int?)null,
            targetLawyerId = (int?)null,
            message = "اجتماع الفرع يوم الأحد",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<HeadAlertDto>();
        Assert.Equal(2, created!.RecipientCount);

        foreach (var lawyer in new[] { first, second })
        {
            var client = _factory.AuthorizedClient(lawyer.Username);
            var list = await (await client.GetAsync("/api/alerts")).Content.ReadFromJsonAsync<List<HeadAlertDto>>();
            Assert.Contains(list!, a => a.Message == "اجتماع الفرع يوم الأحد");
        }
    }

    [Fact]
    public async Task Head_ListsOnlyOwnRecipientAlerts_WithReadState()
    {
        var branchId = await CreateBranchAsync("إحصاء");
        var headName = (await _factory.CreateUserAsync(NewName("head_st"), UserRole.Head, branchId)).Username;
        var first = await _factory.CreateUserAsync(NewName("lawyer_st"), UserRole.Lawyer, branchId);
        await _factory.CreateUserAsync(NewName("lawyer_st"), UserRole.Lawyer, branchId);

        var docLogin = await _factory.LoginAsync(first.Username, "123456");
        var docId = await _factory.CreateDocumentAsync(docLogin!.Token!, borrowerName: "مقترض");
        var head = _factory.AuthorizedClient(headName);

        await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "document",
            documentId = docId,
            targetLawyerId = (int?)null,
            message = "تنبيه أول",
        });
        await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "branch",
            documentId = (int?)null,
            targetLawyerId = (int?)null,
            message = "تعميم",
        });
        var own = await (await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "head",
            documentId = (int?)null,
            targetLawyerId = (int?)null,
            message = "لرئيس القسم",
            recipientUserId = await UserIdAsync(headName),
        })).Content.ReadFromJsonAsync<HeadAlertDto>();

        // قراءة بالمستلم (§8): الرئيس يرى تنبيهه فقط — لا تنبيهات المحامين ولا التعميم.
        var list = await (await head.GetAsync("/api/alerts")).Content.ReadFromJsonAsync<List<HeadAlertDto>>();

        Assert.NotNull(list);
        var single = Assert.Single(list!);
        Assert.Equal("لرئيس القسم", single.Message);
        Assert.False(single.IsRead);
        Assert.Equal(1, single.RecipientCount);
        Assert.Equal(1, single.UnreadCount);
        _ = own;
    }

    [Fact]
    public async Task Lawyer_MarksAlertRead_UnreadCountDrops()
    {
        var docId = await CreateDocumentForLawyerAsync(_factory, "lawyer1");
        var head = _factory.AuthorizedClient("head1");
        var created = await (await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "document",
            documentId = docId,
            targetLawyerId = (int?)null,
            message = "تنبيه للمحامي",
        })).Content.ReadFromJsonAsync<HeadAlertDto>();

        var lawyer = _factory.AuthorizedClient("lawyer1");
        var before = await (await lawyer.GetAsync("/api/alerts/unread-count")).Content.ReadFromJsonAsync<UnreadCountDto>();
        Assert.NotNull(before);

        var mark = await lawyer.PatchAsJsonAsync($"/api/alerts/{created!.Id}/read", new { });
        Assert.Equal(HttpStatusCode.NoContent, mark.StatusCode);

        var after = await (await lawyer.GetAsync("/api/alerts/unread-count")).Content.ReadFromJsonAsync<UnreadCountDto>();
        Assert.Equal(before!.Count - 1, after!.Count);

        var list = await (await lawyer.GetAsync("/api/alerts")).Content.ReadFromJsonAsync<List<HeadAlertDto>>();
        Assert.True(list!.Single(a => a.Id == created.Id).IsRead);
    }

    [Fact]
    public async Task Head_MarksOwnAlertRead_Succeeds_OthersNotFound()
    {
        var branchId = await CreateBranchAsync("قراءة رئيس");
        var headName = (await _factory.CreateUserAsync(NewName("head_rd"), UserRole.Head, branchId)).Username;
        var lawyer = await _factory.CreateUserAsync(NewName("lawyer_rd"), UserRole.Lawyer, branchId);
        var head = _factory.AuthorizedClient(headName);

        var own = await (await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "head",
            documentId = (int?)null,
            targetLawyerId = (int?)null,
            message = "تنبيه الرئيس الخاص",
            recipientUserId = await UserIdAsync(headName),
        })).Content.ReadFromJsonAsync<HeadAlertDto>();
        var others = await (await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "lawyer",
            documentId = (int?)null,
            targetLawyerId = lawyer.Id,
            message = "تنبيه المحامي الخاص",
        })).Content.ReadFromJsonAsync<HeadAlertDto>();

        // المستلم (ولو رئيسًا) يعلّم تنبيهه مقروءًا؛ وغير المستلم 404.
        Assert.Equal(HttpStatusCode.NoContent,
            (await head.PatchAsJsonAsync($"/api/alerts/{own!.Id}/read", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await head.PatchAsJsonAsync($"/api/alerts/{others!.Id}/read", new { })).StatusCode);

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
    public async Task Get_Alert_LawyerRecipient_ReturnsIt()
    {
        var docId = await CreateDocumentForLawyerAsync(_factory, "lawyer1");
        var head = _factory.AuthorizedClient("head1");
        var created = await (await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "document",
            documentId = docId,
            targetLawyerId = (int?)null,
            message = "تنبيه للجلب",
        })).Content.ReadFromJsonAsync<HeadAlertDto>();

        var lawyer = _factory.AuthorizedClient("lawyer1");
        var response = await lawyer.GetAsync($"/api/alerts/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var alert = await response.Content.ReadFromJsonAsync<HeadAlertDto>();
        Assert.Equal(created.Id, alert!.Id);
        Assert.Equal("document", alert.TargetType);
    }

    [Fact]
    public async Task Get_Alert_LawyerNonRecipient_NotFound()
    {
        var branchId = await CreateBranchAsync("جلب");
        var headName = (await _factory.CreateUserAsync(NewName("head_get"), UserRole.Head, branchId)).Username;
        var target = await _factory.CreateUserAsync(NewName("lawyer_get"), UserRole.Lawyer, branchId);
        var other = await _factory.CreateUserAsync(NewName("lawyer_get2"), UserRole.Lawyer, branchId);
        var head = _factory.AuthorizedClient(headName);

        var created = await (await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "lawyer",
            documentId = (int?)null,
            targetLawyerId = target.Id,
            message = "خاص بالجلب",
        })).Content.ReadFromJsonAsync<HeadAlertDto>();

        var otherClient = _factory.AuthorizedClient(other.Username);
        var response = await otherClient.GetAsync($"/api/alerts/{created!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_Alert_HeadRecipient_ReturnsIt_OthersNotFound()
    {
        var branchId = await CreateBranchAsync("جلب رئيس");
        var headName = (await _factory.CreateUserAsync(NewName("head_g2"), UserRole.Head, branchId)).Username;
        var lawyer = await _factory.CreateUserAsync(NewName("lawyer_g3"), UserRole.Lawyer, branchId);
        var head = _factory.AuthorizedClient(headName);

        var own = await (await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "head",
            documentId = (int?)null,
            targetLawyerId = (int?)null,
            message = "جلب رئيس",
            recipientUserId = await UserIdAsync(headName),
        })).Content.ReadFromJsonAsync<HeadAlertDto>();
        var others = await (await head.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "lawyer",
            documentId = (int?)null,
            targetLawyerId = lawyer.Id,
            message = "خاص بالمحامي",
        })).Content.ReadFromJsonAsync<HeadAlertDto>();

        Assert.Equal(HttpStatusCode.OK, (await head.GetAsync($"/api/alerts/{own!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await head.GetAsync($"/api/alerts/{others!.Id}")).StatusCode);
    }

    [Fact]
    public async Task Get_Alert_HeadOfAnotherBranch_NotFound()
    {
        var branchA = await CreateBranchAsync("ألف");
        var headA = (await _factory.CreateUserAsync(NewName("head_a"), UserRole.Head, branchA)).Username;
        var lawyerA = await _factory.CreateUserAsync(NewName("lawyer_a"), UserRole.Lawyer, branchA);
        var clientA = _factory.AuthorizedClient(headA);
        var created = await (await clientA.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "lawyer",
            documentId = (int?)null,
            targetLawyerId = lawyerA.Id,
            message = "تنبيه ألف",
        })).Content.ReadFromJsonAsync<HeadAlertDto>();

        var branchB = await CreateBranchAsync("باء");
        var headB = (await _factory.CreateUserAsync(NewName("head_b"), UserRole.Head, branchB)).Username;
        var clientB = _factory.AuthorizedClient(headB);

        var response = await clientB.GetAsync($"/api/alerts/{created!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_Alert_Manager_Forbidden()
    {
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.GetAsync("/api/alerts/1");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Lawyer_ListsAlerts_DoesNotSeeOthersBranchesAlerts()
    {
        var branchId = await CreateBranchAsync("حلب2");
        var headName = (await _factory.CreateUserAsync(NewName("head_al2"), UserRole.Head, branchId)).Username;
        var alpLawyer = await _factory.CreateUserAsync(NewName("lawyer_al2"), UserRole.Lawyer, branchId);
        var headAlp = _factory.AuthorizedClient(headName);

        await headAlp.PostAsJsonAsync("/api/alerts", new
        {
            targetType = "lawyer",
            documentId = (int?)null,
            targetLawyerId = alpLawyer.Id,
            message = "تنبيه خاص بحلب",
        });

        var lawyerDam = _factory.AuthorizedClient("lawyer1");
        var list = await (await lawyerDam.GetAsync("/api/alerts")).Content.ReadFromJsonAsync<List<HeadAlertDto>>();
        Assert.DoesNotContain(list!, a => a.Message == "تنبيه خاص بحلب");
    }

    private sealed record UnreadCountDto(int Count);
}
