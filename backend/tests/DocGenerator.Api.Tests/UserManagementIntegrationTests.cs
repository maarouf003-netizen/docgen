using System.Net;
using System.Net.Http.Json;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

[Collection(ApiTestCollection.Name)]
public class UserManagementIntegrationTests
{
    private readonly ApiFactory _factory;

    public UserManagementIntegrationTests(ApiFactory factory) => _factory = factory;

    private Task<int> BranchIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return Task.FromResult(db.Branches.Single(b => b.Code == code).Id);
    }

    private static string NewUsername(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..20];

    /// <summary>
    /// فرع معزول برئيسه المستقبلي الوحيد (وحدانية الرئيس المفعّل لكل فرع —
    /// قرار §2.26: الفروع المزروعة قد تحوي رؤساء من اختبارات أخرى في القاعدة المشتركة).
    /// </summary>
    private async Task<int> CreateBranchAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var branch = new DocGenerator.Domain.Entities.Branch { Name = "فرع اختبار", Code = $"UM_{Guid.NewGuid():N}"[..12].ToUpperInvariant() };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        return branch.Id;
    }

    [Fact]
    public async Task UserManagement_NonAdminRoles_Forbidden()
    {
        // `BQ-001د` (`RF-004`): المدير يدير المستخدمين — بقي head1/lawyer1 مرفوضين.
        foreach (var username in new[] { "head1", "lawyer1" })
        {
            var client = _factory.AuthorizedClient(username);
            var response = await client.GetAsync("/api/users");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task UserManagement_AsManager_Allowed()
    {
        // `BQ-001د` (`RF-004`): المدير يسرد المستخدمين (كان `403`).
        var client = _factory.AuthorizedClient("manager");
        var response = await client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Admin_ListsUsers_ContainsSeeded()
    {
        var admin = _factory.AuthorizedClient("admin");
        var response = await admin.GetAsync("/api/users");
        response.EnsureSuccessStatusCode();
        var users = await response.Content.ReadFromJsonAsync<List<UserListItemDto>>();

        Assert.NotNull(users);
        Assert.Contains(users, u => u.Username == "admin");
        Assert.Contains(users, u => u.Username == "lawyer1");
    }

    [Fact]
    public async Task Admin_CreatesUser_NewUserCanLogin()
    {
        var username = NewUsername("u");
        var branchId = await CreateBranchAsync();
        var admin = _factory.AuthorizedClient("admin");
        var response = await admin.PostAsJsonAsync("/api/users", new
        {
            username,
            fullName = "رئيس قسم جديد",
            role = "head",
            branchId,
            password = "123456",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<UserListItemDto>();
        Assert.NotNull(created);
        Assert.Equal(username, created.Username);
        Assert.Equal("head", created.Role);

        var login = await _factory.LoginAsync(username, "123456");
        Assert.Equal(HttpStatusCode.OK, (HttpStatusCode)login!.StatusCode);
    }

    [Fact]
    public async Task Admin_CreatesSecondHeadSameBranch_BadRequestWithApprovedMessage()
    {
        // وحدانية الرئيس المفعّل (قرار §2.26): الثاني مرفوض `400` بالرسالة
        // المعتمدة — لا `500` من القيد.
        var branchId = await CreateBranchAsync();
        var admin = _factory.AuthorizedClient("admin");
        var first = await admin.PostAsJsonAsync("/api/users", new
        {
            username = NewUsername("h1"),
            fullName = "رئيس أول",
            role = "head",
            branchId,
            password = "123456",
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await admin.PostAsJsonAsync("/api/users", new
        {
            username = NewUsername("h2"),
            fullName = "رئيس ثان",
            role = "head",
            branchId,
            password = "123456",
        });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains("الفرع لا يقبل رئيسي قسم", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_UpdatesUser_DeactivateInvalidatesToken()
    {
        var username = NewUsername("u");
        await _factory.CreateUserAsync(username, UserRole.Lawyer, await BranchIdAsync("DAM"));
        var token = (await _factory.LoginAsync(username, "123456"))!.Token;
        Assert.False(string.IsNullOrWhiteSpace(token));

        var admin = _factory.AuthorizedClient("admin");
        var users = await (await admin.GetAsync("/api/users")).Content.ReadFromJsonAsync<List<UserListItemDto>>();
        var target = users!.Single(u => u.Username == username);

        var update = await admin.PutAsJsonAsync($"/api/users/{target.Id}", new
        {
            fullName = "محامي معدل",
            role = "lawyer",
            branchId = await BranchIdAsync("DAM"),
            isActive = false,
            password = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var login = await _factory.LoginAsync(username, "123456");
        Assert.Equal(HttpStatusCode.Unauthorized, (HttpStatusCode)login!.StatusCode);
    }

    [Fact]
    public async Task Admin_CannotDeactivateHimself()
    {
        var admin = _factory.AuthorizedClient("admin");
        var users = await (await admin.GetAsync("/api/users")).Content.ReadFromJsonAsync<List<UserListItemDto>>();
        var self = users!.Single(u => u.Username == "admin");

        var response = await admin.PutAsJsonAsync($"/api/users/{self.Id}", new
        {
            fullName = "مشرف النظام",
            role = "admin",
            branchId = (int?)null,
            isActive = false,
            password = (string?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CreatesUser_InvalidRole_BadRequest()
    {
        var admin = _factory.AuthorizedClient("admin");
        var response = await admin.PostAsJsonAsync("/api/users", new
        {
            username = NewUsername("b"),
            fullName = "مستخدم",
            role = "king",
            branchId = (int?)null,
            password = "123456",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Head_ListsLawyers_OnlyOwnBranch()
    {
        var head = _factory.AuthorizedClient("head1");
        var response = await head.GetAsync("/api/users/lawyers");
        response.EnsureSuccessStatusCode();
        var lawyers = await response.Content.ReadFromJsonAsync<List<LawyerListItemDto>>();
        var damascusId = await BranchIdAsync("DAM");

        Assert.NotNull(lawyers);
        Assert.DoesNotContain(lawyers, l => l.BranchId != damascusId);
    }

    [Fact]
    public async Task Head_AddsLawyer_IgnoresForeignBranch()
    {
        var head = _factory.AuthorizedClient("head1");
        var response = await head.PostAsJsonAsync("/api/users/lawyers", new
        {
            username = NewUsername("l"),
            fullName = "محامي جديد",
            password = "123456",
            branchId = await BranchIdAsync("ALP"),
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var lawyer = await response.Content.ReadFromJsonAsync<LawyerListItemDto>();
        Assert.Equal(await BranchIdAsync("DAM"), lawyer!.BranchId);
    }

    [Fact]
    public async Task Admin_AddsLawyer_ToSpecifiedBranch()
    {
        var admin = _factory.AuthorizedClient("admin");
        var response = await admin.PostAsJsonAsync("/api/users/lawyers", new
        {
            username = NewUsername("l"),
            fullName = "محامي حلب",
            password = "123456",
            branchId = await BranchIdAsync("ALP"),
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var lawyer = await response.Content.ReadFromJsonAsync<LawyerListItemDto>();
        Assert.Equal(await BranchIdAsync("ALP"), lawyer!.BranchId);
    }

    [Fact]
    public async Task Admin_AddsLawyer_WithoutBranch_BadRequest()
    {
        var admin = _factory.AuthorizedClient("admin");
        var response = await admin.PostAsJsonAsync("/api/users/lawyers", new
        {
            username = NewUsername("l"),
            fullName = "محامي",
            password = "123456",
            branchId = (int?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Head_DeactivatesForeignBranchLawyer_NotFound()
    {
        var admin = _factory.AuthorizedClient("admin");
        var created = await admin.PostAsJsonAsync("/api/users/lawyers", new
        {
            username = NewUsername("l"),
            fullName = "محامي حلب",
            password = "123456",
            branchId = await BranchIdAsync("ALP"),
        });
        var aleppoLawyer = (await created.Content.ReadFromJsonAsync<LawyerListItemDto>())!;

        var head = _factory.AuthorizedClient("head1");
        var response = await head.PatchAsJsonAsync($"/api/users/{aleppoLawyer.Id}/active", new { isActive = false });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Head_DeactivatesOwnLawyer_ThenLoginFails()
    {
        var admin = _factory.AuthorizedClient("admin");
        var created = await admin.PostAsJsonAsync("/api/users/lawyers", new
        {
            username = NewUsername("l"),
            fullName = "محامي دمشق",
            password = "123456",
            branchId = await BranchIdAsync("DAM"),
        });
        var lawyer = (await created.Content.ReadFromJsonAsync<LawyerListItemDto>())!;
        var token = (await _factory.LoginAsync(lawyer.Username, "123456"))!.Token;
        Assert.False(string.IsNullOrWhiteSpace(token));

        var head = _factory.AuthorizedClient("head1");
        var response = await head.PatchAsJsonAsync($"/api/users/{lawyer.Id}/active", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await _factory.LoginAsync(lawyer.Username, "123456");
        Assert.Equal(HttpStatusCode.Unauthorized, (HttpStatusCode)login!.StatusCode);
    }

    [Fact]
    public async Task Admin_CreatesUser_ArabicTripartiteName_LoginWithEquivalentSpelling()
    {
        var name = "أحمد خالد العلي";
        var branchId = await CreateBranchAsync();
        var admin = _factory.AuthorizedClient("admin");
        var response = await admin.PostAsJsonAsync("/api/users", new
        {
            username = name,
            fullName = name,
            role = "head",
            branchId,
            password = "123456",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<UserListItemDto>();

        // تُخزَّن النسخة المطبّعة (أ/إ/آ → ا) كمعيار موحد.
        Assert.Equal("احمد خالد العلي", created!.Username);

        // الدخول بالنسخة ذات الهمزة يعمل لأن التطبيع على الطرفين.
        var login = await _factory.LoginAsync("أحمد خالد العلي", "123456");
        Assert.Equal(HttpStatusCode.OK, (HttpStatusCode)login!.StatusCode);
    }

    [Fact]
    public async Task Admin_CreatesUser_DuplicateTripartiteNameSameBranch_BadRequest()
    {
        var name = "سامر محمود عيد";
        var branchId = await CreateBranchAsync();
        var admin = _factory.AuthorizedClient("admin");
        var first = await admin.PostAsJsonAsync("/api/users", new
        {
            username = name,
            fullName = name,
            role = "head",
            branchId,
            password = "123456",
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await admin.PostAsJsonAsync("/api/users", new
        {
            username = name,
            fullName = name,
            role = "lawyer",
            branchId,
            password = "123456",
        });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var body = await second.Content.ReadAsStringAsync();
        Assert.Contains("نفس الفرع", body);
    }

    [Fact]
    public async Task Admin_CreatesUser_SameTripartiteNameDifferentBranch_Allowed()
    {
        var name = "نزار عادل صالح";
        var branchA = await CreateBranchAsync();
        var branchB = await CreateBranchAsync();
        var admin = _factory.AuthorizedClient("admin");
        var first = await admin.PostAsJsonAsync("/api/users", new
        {
            username = name,
            fullName = name,
            role = "head",
            branchId = branchA,
            password = "123456",
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await admin.PostAsJsonAsync("/api/users", new
        {
            username = name,
            fullName = name,
            role = "head",
            branchId = branchB,
            password = "123456",
        });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
    }

    [Fact]
    public async Task DbConstraint_SameUsernameWithoutBranch_Rejected()
    {
        var username = NewUsername("nb");
        await _factory.CreateUserAsync(username, UserRole.Manager);

        // الفهرس الفريد الجزئي (BranchId IS NULL) يمنع تكرار اسم المدير/المشرف في قاعدة البيانات.
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            _factory.CreateUserAsync(username, UserRole.Manager));
    }

    [Fact]
    public async Task DbConstraint_SameUsername_OneWithBranchOneWithout_Allowed()
    {
        var username = NewUsername("mix");
        await _factory.CreateUserAsync(username, UserRole.Lawyer, await BranchIdAsync("DAM"));

        // الاسم نفسه مسموح مرةً بلا فرع وأخرى بفرع: قيود الفرعين مستقلتان.
        await _factory.CreateUserAsync(username, UserRole.Manager);
    }

    [Fact]
    public async Task Head_UpdatesOwnLawyer_RenameAndResetPassword()
    {
        var username = NewUsername("l");
        var admin = _factory.AuthorizedClient("admin");
        var created = await admin.PostAsJsonAsync("/api/users/lawyers", new
        {
            username,
            fullName = "محامي دمشق",
            password = "123456",
            branchId = await BranchIdAsync("DAM"),
        });
        var lawyer = (await created.Content.ReadFromJsonAsync<LawyerListItemDto>())!;
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var head = _factory.AuthorizedClient("head1");
        var update = await head.PutAsJsonAsync($"/api/users/lawyers/{lawyer.Id}", new
        {
            fullName = "محامي محدث للاختبار",
            password = "654321",
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<LawyerListItemDto>();
        Assert.Equal("محامي محدث للاختبار", updated!.FullName);
        Assert.Equal("محامي محدث للاختبار", updated.Username);

        // الدخول بالاسم القديم مرفوض (تغيّر اسم الدخول) وبكلمة المرور القديمة مرفوض (إبطال الرموز).
        var oldLogin = await _factory.LoginAsync(username, "123456");
        Assert.Equal(HttpStatusCode.Unauthorized, (HttpStatusCode)oldLogin!.StatusCode);

        var newLogin = await _factory.LoginAsync("محامي محدث للاختبار", "654321");
        Assert.Equal(HttpStatusCode.OK, (HttpStatusCode)newLogin!.StatusCode);
    }

    [Fact]
    public async Task Head_UpdatesForeignBranchLawyer_NotFound()
    {
        var admin = _factory.AuthorizedClient("admin");
        var created = await admin.PostAsJsonAsync("/api/users/lawyers", new
        {
            username = NewUsername("l"),
            fullName = "محامي حلب",
            password = "123456",
            branchId = await BranchIdAsync("ALP"),
        });
        var aleppoLawyer = (await created.Content.ReadFromJsonAsync<LawyerListItemDto>())!;

        var head = _factory.AuthorizedClient("head1");
        var response = await head.PutAsJsonAsync($"/api/users/lawyers/{aleppoLawyer.Id}", new
        {
            fullName = "اسم معدل",
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Admin_UpdatesLawyer_AnyBranch()
    {
        var admin = _factory.AuthorizedClient("admin");
        var created = await admin.PostAsJsonAsync("/api/users/lawyers", new
        {
            username = NewUsername("l"),
            fullName = "محامي حلب",
            password = "123456",
            branchId = await BranchIdAsync("ALP"),
        });
        var aleppoLawyer = (await created.Content.ReadFromJsonAsync<LawyerListItemDto>())!;

        var update = await admin.PutAsJsonAsync($"/api/users/lawyers/{aleppoLawyer.Id}", new
        {
            fullName = "محامي حلب معدل",
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<LawyerListItemDto>();
        Assert.Equal("محامي حلب معدل", updated!.Username);
        Assert.Equal(await BranchIdAsync("ALP"), updated.BranchId);
    }

    [Fact]
    public async Task UpdateLawyer_AsLawyer_Forbidden()
    {
        var headClient = _factory.AuthorizedClient("head1");
        var headResponse = await headClient.GetAsync("/api/users/lawyers");
        var damLawyers = await headResponse.Content.ReadFromJsonAsync<List<LawyerListItemDto>>();
        var lawyer = damLawyers!.First();

        var client = _factory.AuthorizedClient("lawyer1");
        var response = await client.PutAsJsonAsync($"/api/users/lawyers/{lawyer.Id}", new { fullName = "معدل" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Head_UpdatesOwnLawyer_NoChanges_BadRequest()
    {
        var admin = _factory.AuthorizedClient("admin");
        var created = await admin.PostAsJsonAsync("/api/users/lawyers", new
        {
            username = NewUsername("l"),
            fullName = "محامي دمشق",
            password = "123456",
            branchId = await BranchIdAsync("DAM"),
        });
        var lawyer = (await created.Content.ReadFromJsonAsync<LawyerListItemDto>())!;

        var head = _factory.AuthorizedClient("head1");
        var response = await head.PutAsJsonAsync($"/api/users/lawyers/{lawyer.Id}", new
        {
            fullName = (string?)null,
            password = (string?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("لا يوجد تغيير", body);
    }

    [Fact]
    public async Task Head_UpdatesOwnLawyer_DuplicateName_BadRequest()
    {
        var admin = _factory.AuthorizedClient("admin");
        var first = await admin.PostAsJsonAsync("/api/users/lawyers", new
        {
            username = "مروان سعيد",
            fullName = "مروان سعيد",
            password = "123456",
            branchId = await BranchIdAsync("DAM"),
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await admin.PostAsJsonAsync("/api/users/lawyers", new
        {
            username = "قاسم علي",
            fullName = "قاسم علي",
            password = "123456",
            branchId = await BranchIdAsync("DAM"),
        });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var target = (await second.Content.ReadFromJsonAsync<LawyerListItemDto>())!;

        var head = _factory.AuthorizedClient("head1");
        var response = await head.PutAsJsonAsync($"/api/users/lawyers/{target.Id}", new { fullName = "مروان سعيد" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("نفس الفرع", body);
    }

    private async Task<int> CreateSectionAsync(string name, int branchId)
    {
        var admin = _factory.AuthorizedClient("admin");
        var response = await admin.PostAsJsonAsync("/api/sections", new { branchId, name });
        response.EnsureSuccessStatusCode();
        using var doc = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
        return doc!.RootElement.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Admin_CreatesSubHead_WithSection_CreatedAndCanLogin()
    {
        var branchId = await BranchIdAsync("DAM");
        var sectionId = await CreateSectionAsync($"شعبة التكامل {Guid.NewGuid():N}"[..20], branchId);
        var username = NewUsername("s");
        var admin = _factory.AuthorizedClient("admin");
        var response = await admin.PostAsJsonAsync("/api/users", new
        {
            username,
            fullName = "رئيس شعبة التكامل",
            role = "subhead",
            branchId,
            password = "123456",
            sectionId,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<UserListItemDto>();
        Assert.NotNull(created);
        Assert.Equal("subhead", created!.Role);
        Assert.Equal(sectionId, created.SectionId);

        // الدخول يعمل (بوابة الفرع) — ونقاط الدوائر مفتوحة للسياسة منذ المرحلة 3
        // لكن مفلترة بالنطاق (شعبته فقط — هنا فارغة).
        var login = await _factory.LoginAsync(username, "123456");
        Assert.Equal(HttpStatusCode.OK, (HttpStatusCode)login!.StatusCode);
        var client = _factory.CreateClient();
        client.SetAuthCookie(login.Token!);
        var mine = await client.GetAsync("/api/execution-circuits/mine");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        using var mineDoc = await mine.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
        Assert.Equal(0, mineDoc!.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Admin_CreatesSubHead_MissingSection_BadRequest()
    {
        var admin = _factory.AuthorizedClient("admin");
        var response = await admin.PostAsJsonAsync("/api/users", new
        {
            username = NewUsername("s"),
            fullName = "رئيس شعبة بلا",
            role = "subhead",
            branchId = await BranchIdAsync("DAM"),
            password = "123456",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("الشعبة إلزامية", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_CreatesSubHead_OccupiedSection_BadRequest()
    {
        var branchId = await BranchIdAsync("DAM");
        var sectionId = await CreateSectionAsync($"شعبة مشغولة {Guid.NewGuid():N}"[..20], branchId);
        var admin = _factory.AuthorizedClient("admin");
        var first = await admin.PostAsJsonAsync("/api/users", new
        {
            username = NewUsername("s1"),
            fullName = "رئيس أول",
            role = "subhead",
            branchId,
            password = "123456",
            sectionId,
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await admin.PostAsJsonAsync("/api/users", new
        {
            username = NewUsername("s2"),
            fullName = "رئيس ثان",
            role = "subhead",
            branchId,
            password = "123456",
            sectionId,
        });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains("الشعبة مشغولة برئيس مفعّل", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_CreatesSubHead_ForeignSection_BadRequest()
    {
        var sectionId = await CreateSectionAsync($"شعبة حلب {Guid.NewGuid():N}"[..20], await BranchIdAsync("ALP"));
        var admin = _factory.AuthorizedClient("admin");
        var response = await admin.PostAsJsonAsync("/api/users", new
        {
            username = NewUsername("s"),
            fullName = "رئيس شعبة خطأ",
            role = "subhead",
            branchId = await BranchIdAsync("DAM"),
            password = "123456",
            sectionId,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Manager_DeactivatesHeadWithSuccessor_FullFlow()
    {
        var admin = _factory.AuthorizedClient("admin");
        var branchId = await BranchIdAsync("DAM");
        var sectionId = await CreateSectionAsync($"شعبة إحلال {Guid.NewGuid():N}"[..20], branchId);
        var head = await admin.PostAsJsonAsync("/api/users", new
        {
            username = NewUsername("sh"),
            fullName = "رئيس شعبة محل",
            role = "subhead",
            branchId,
            password = "123456",
            sectionId,
        });
        Assert.Equal(HttpStatusCode.Created, head.StatusCode);
        var headDto = await head.Content.ReadFromJsonAsync<UserListItemDto>();
        var lawyer = await admin.PostAsJsonAsync("/api/users/lawyers", new
        {
            username = NewUsername("sl"),
            fullName = "محامي الخلف",
            password = "123456",
            branchId,
        });
        Assert.Equal(HttpStatusCode.OK, lawyer.StatusCode);
        var lawyerDto = await lawyer.Content.ReadFromJsonAsync<LawyerListItemDto>();

        var manager = _factory.AuthorizedClient("manager");
        var deactivate = await manager.PutAsJsonAsync($"/api/users/{headDto!.Id}", new
        {
            fullName = (string?)null,
            role = (string?)null,
            branchId = (int?)null,
            isActive = false,
            password = (string?)null,
            sectionId = (int?)null,
            successorId = lawyerDto!.Id,
        });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        // الخلف رُقّي بشعبة السلف.
        var fetched = await manager.GetAsync($"/api/users/{lawyerDto.Id}");
        var promoted = await fetched.Content.ReadFromJsonAsync<UserListItemDto>();
        Assert.Equal("subhead", promoted!.Role);
        Assert.Equal(sectionId, promoted.SectionId);

        // سجل التعاقب مرئي للإدارة بصفين على الأقل (تعطيل + إحلال).
        var log = await manager.GetAsync("/api/head-succession");
        Assert.Equal(HttpStatusCode.OK, log.StatusCode);
        var logBody = await log.Content.ReadAsStringAsync();
        Assert.Contains("succeeded", logBody);

        // إعادة تفعيل السلف مرفوضة (الخلف شاغل).
        var reactivate = await manager.PutAsJsonAsync($"/api/users/{headDto.Id}", new
        {
            fullName = (string?)null,
            role = (string?)null,
            branchId = (int?)null,
            isActive = true,
            password = (string?)null,
            sectionId = (int?)null,
            successorId = (int?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, reactivate.StatusCode);
    }

    [Fact]
    public async Task Manager_DeactivatesHeadWithoutSuccessor_BadRequest()
    {
        var admin = _factory.AuthorizedClient("admin");
        var branchId = await BranchIdAsync("DAM");
        var sectionId = await CreateSectionAsync($"شعبة بلا خلف {Guid.NewGuid():N}"[..20], branchId);
        var head = await admin.PostAsJsonAsync("/api/users", new
        {
            username = NewUsername("sh"),
            fullName = "رئيس بلا خلف",
            role = "subhead",
            branchId,
            password = "123456",
            sectionId,
        });
        var headDto = await head.Content.ReadFromJsonAsync<UserListItemDto>();

        var manager = _factory.AuthorizedClient("manager");
        var deactivate = await manager.PutAsJsonAsync($"/api/users/{headDto!.Id}", new
        {
            fullName = (string?)null,
            role = (string?)null,
            branchId = (int?)null,
            isActive = false,
            password = (string?)null,
            sectionId = (int?)null,
            successorId = (int?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, deactivate.StatusCode);
        Assert.Contains("خلفًا إجباريًا", await deactivate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Head_UpdateUser_Forbidden()
    {
        var head = _factory.AuthorizedClient("head1");
        var response = await head.PutAsJsonAsync("/api/users/1", new { fullName = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("manager")]
    [InlineData("admin")]
    public async Task HeadSuccession_ManagementRoles_Ok(string username)
    {
        var client = _factory.AuthorizedClient(username);
        var response = await client.GetAsync("/api/head-succession");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("head1")]
    [InlineData("lawyer1")]
    public async Task HeadSuccession_NonManagementRoles_Forbidden(string username)
    {
        // `head1` مرفوض بالسمة (ليس مديرًا/مشرفًا) — نطاق التدقيق الدائري (§2.23)
        // يُفرض في متحكم التدقيق نفسه عند بنائه (المرحلة 8).
        var client = _factory.AuthorizedClient(username);
        var response = await client.GetAsync("/api/head-succession");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
