using System.Net;
using System.Net.Http.Json;
using static DocGenerator.Api.Tests.TestHelpers.ApiFixtureExtensions;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// إبطال الصلاحية القديمة (S1): تغيير الدور أو الفرع يجب أن يُسقط التوكنات الصادرة
/// سابقًا عبر رفع <c>TokenVersion</c> — وإلا بقيت صلاحيات الدور/الفرع القديم صالحة
/// حتى انتهاء التوكن (انظر <c>docs/SECURITY_PLAN.md</c> §3).
/// </summary>
[Collection(ApiTestCollection.Name)]
public class StaleRoleRevocationTests
{
    private readonly ApiFactory _factory;

    public StaleRoleRevocationTests(ApiFactory factory) => _factory = factory;

    private int BranchId(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return db.Branches.Single(b => b.Code == code).Id;
    }

    /// <summary>
    /// فرع معزول برئيسه الوحيد (وحدانية الرئيس المفعّل لكل فرع — قرار §2.26).
    /// </summary>
    [PostgresFact]
    public async Task RoleDemotion_InvalidatesPreviouslyIssuedTokens()
    {
        var username = $"demote_{Guid.NewGuid():N}"[..16];
        var target = await _factory.CreateUserAsync(username, UserRole.Admin, password: "123456");

        var login = await _factory.LoginAsync(username, "123456");
        Assert.Equal((int)HttpStatusCode.OK, login!.StatusCode);

        var staleClient = _factory.CreateClient();
        staleClient.SetAuthCookie(login.Token!);
        Assert.Equal(HttpStatusCode.OK, (await staleClient.GetAsync("/api/auth/me")).StatusCode);

        // المشرف المزروع يخفّض الدور إلى محامٍ في فرع دمشق.
        var admin = _factory.AuthorizedClient("admin");
        var demote = await admin.PutAsJsonAsync($"/api/users/{target.Id}", new
        {
            fullName = (string?)null,
            role = "lawyer",
            branchId = (int?)BranchId("DAM"),
            isActive = true,
            password = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, demote.StatusCode);

        // التوكن القديم (بدور admin) يجب أن يسقط فورًا.
        Assert.Equal(HttpStatusCode.Unauthorized, (await staleClient.GetAsync("/api/auth/me")).StatusCode);

        // الدخول الجديد يعمل بالدور الجديد.
        var fresh = await _factory.LoginAsync(username, "123456");
        Assert.Equal((int)HttpStatusCode.OK, fresh!.StatusCode);
        var freshClient = _factory.CreateClient();
        freshClient.SetAuthCookie(fresh.Token!);
        Assert.Equal(HttpStatusCode.OK, (await freshClient.GetAsync("/api/auth/me")).StatusCode);
    }

    [PostgresFact]
    public async Task BranchTransfer_InvalidatesPreviouslyIssuedTokens()
    {
        var username = $"move_{Guid.NewGuid():N}"[..16];
        var target = await _factory.CreateUserAsync(username, UserRole.Lawyer, branchId: BranchId("DAM"), password: "123456");

        var login = await _factory.LoginAsync(username, "123456");
        Assert.Equal((int)HttpStatusCode.OK, login!.StatusCode);

        var staleClient = _factory.CreateClient();
        staleClient.SetAuthCookie(login.Token!);
        Assert.Equal(HttpStatusCode.OK, (await staleClient.GetAsync("/api/auth/me")).StatusCode);

        // نقل المحامي من دمشق إلى حلب مع بقاء الدور.
        var admin = _factory.AuthorizedClient("admin");
        var move = await admin.PutAsJsonAsync($"/api/users/{target.Id}", new
        {
            fullName = (string?)null,
            role = "lawyer",
            branchId = (int?)BranchId("ALP"),
            isActive = true,
            password = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, move.StatusCode);

        // التوكن القديم (بفرع DAM) يجب أن يسقط فورًا.
        Assert.Equal(HttpStatusCode.Unauthorized, (await staleClient.GetAsync("/api/auth/me")).StatusCode);

        var fresh = await _factory.LoginAsync(username, "123456");
        Assert.Equal((int)HttpStatusCode.OK, fresh!.StatusCode);
    }

    /// <summary>
    /// نقل رئيس القسم بين الفروع يُبطل توكن الفرع القديم (S1): الفرع جزء
    /// من هوية التوكن فيُعامَل كالدور — فلا يرى بيانات فرعه السابق بصمت.
    /// </summary>
    [PostgresFact]
    public async Task HeadBranchTransfer_InvalidatesPreviouslyIssuedTokens()
    {
        // نفس آلية S1 لكن بدور الرئيس محل التحقيق: نقل الرئيس بين الفروع
        // يُسقط توكن الفرع القديم فورًا فلا يرى بياناته بصمت. الرئيس يُنشأ في
        // فرع معزول (وحدانية الرئيس — قرار §2.26: دمشق فيها head1 المزروع).
        var username = $"hmove_{Guid.NewGuid():N}"[..16];
        var srcBranchId = await _factory.CreateBranchAsync("فرع المصدر", "حمص");
        var target = await _factory.CreateUserAsync(username, UserRole.Head, branchId: srcBranchId, password: "123456");

        var login = await _factory.LoginAsync(username, "123456");
        Assert.Equal((int)HttpStatusCode.OK, login!.StatusCode);

        var staleClient = _factory.CreateClient();
        staleClient.SetAuthCookie(login.Token!);
        Assert.Equal(HttpStatusCode.OK, (await staleClient.GetAsync("/api/auth/me")).StatusCode);
        // نقطة بيانات حقيقية (لا `/me` وحدها): رئيس الفرع يرى لوحته.
        Assert.Equal(HttpStatusCode.OK, (await staleClient.GetAsync("/api/dashboard")).StatusCode);

        var admin = _factory.AuthorizedClient("admin");
        var move = await admin.PutAsJsonAsync($"/api/users/{target.Id}", new
        {
            fullName = (string?)null,
            role = "head",
            branchId = (int?)BranchId("ALP"),
            isActive = true,
            password = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, move.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await staleClient.GetAsync("/api/auth/me")).StatusCode);
        // والنقطة البيانية تسقط معها — لا قراءة صامتة لبيانات الفرع السابق.
        Assert.Equal(HttpStatusCode.Unauthorized, (await staleClient.GetAsync("/api/dashboard")).StatusCode);

        var fresh = await _factory.LoginAsync(username, "123456");
        Assert.Equal((int)HttpStatusCode.OK, fresh!.StatusCode);
    }

    /// <summary>
    /// تغيير اسم الدخول يُبطل التوكنات (R2): الاسم جزء من هوية التوكن
    /// (<c>Name</c>/<c>UniqueName</c>) فيُعامَل كالدور/الفرع.
    /// </summary>
    [PostgresFact]
    public async Task UsernameRename_InvalidatesPreviouslyIssuedTokens()
    {
        var username = $"rnm_{Guid.NewGuid():N}"[..16];
        var target = await _factory.CreateUserAsync(username, UserRole.Lawyer, branchId: BranchId("DAM"), password: "123456");

        var login = await _factory.LoginAsync(username, "123456");
        Assert.Equal((int)HttpStatusCode.OK, login!.StatusCode);
        var staleClient = _factory.CreateClient();
        staleClient.SetAuthCookie(login.Token!);

        var newName = $"renamed_{Guid.NewGuid():N}"[..16];
        var admin = _factory.AuthorizedClient("admin");
        var rename = await admin.PutAsJsonAsync($"/api/users/{target.Id}", new
        {
            fullName = newName,
            role = "lawyer",
            branchId = (int?)BranchId("DAM"),
            isActive = true,
            password = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await staleClient.GetAsync("/api/auth/me")).StatusCode);

        var fresh = await _factory.LoginAsync(newName, "123456");
        Assert.Equal((int)HttpStatusCode.OK, fresh!.StatusCode);
    }

    [PostgresFact]
    public async Task LawyerRenameViaBranchRoute_InvalidatesPreviouslyIssuedTokens()
    {
        var username = $"rnl_{Guid.NewGuid():N}"[..16];
        var target = await _factory.CreateUserAsync(username, UserRole.Lawyer, branchId: BranchId("DAM"), password: "123456");

        var login = await _factory.LoginAsync(username, "123456");
        Assert.Equal((int)HttpStatusCode.OK, login!.StatusCode);
        var staleClient = _factory.CreateClient();
        staleClient.SetAuthCookie(login.Token!);

        var newName = $"renamed_{Guid.NewGuid():N}"[..16];
        var admin = _factory.AuthorizedClient("admin");
        var rename = await admin.PutAsJsonAsync($"/api/users/lawyers/{target.Id}", new
        {
            fullName = newName,
            password = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await staleClient.GetAsync("/api/auth/me")).StatusCode);
    }

    /// <summary>
    /// نقل رئيس الشعبة بين شعبين يُبطل توكن الشعبة القديم (W2/F17): الشعبة جزء من
    /// هوية التوكن (`section_id`) كالفرع، فلا يعمل رئيس بنطاق شعبة سابقة بصمت.
    /// </summary>
    [PostgresFact]
    public async Task SectionMove_InvalidatesPreviouslyIssuedTokens()
    {
        var first = await _factory.CreateSectionAsync($"شعبة أولى {Guid.NewGuid():N}"[..24]);
        var second = await _factory.CreateSectionAsync($"شعبة ثانية {Guid.NewGuid():N}"[..24]);
        var username = $"smove_{Guid.NewGuid():N}"[..16];
        var admin = _factory.AuthorizedClient("admin");

        var created = await admin.PostAsJsonAsync("/api/users", new
        {
            username,
            fullName = username,
            role = "subhead",
            branchId = (int?)BranchId("DAM"),
            password = "123456",
            sectionId = first,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>())!
            .RootElement.GetProperty("id").GetInt32();

        var login = await _factory.LoginAsync(username, "123456");
        Assert.Equal((int)HttpStatusCode.OK, login!.StatusCode);
        var staleClient = _factory.CreateClient();
        staleClient.SetAuthCookie(login.Token!);
        Assert.Equal(HttpStatusCode.OK, (await staleClient.GetAsync("/api/auth/me")).StatusCode);

        var move = await admin.PutAsJsonAsync($"/api/users/{id}", new
        {
            fullName = (string?)null,
            role = "subhead",
            branchId = (int?)BranchId("DAM"),
            isActive = true,
            // سلسلة فارغة = لا تغيير لكلمة المرور (`IsNullOrWhiteSpace` في المعالج).
            password = "",
            sectionId = second,
        });
        Assert.Equal(HttpStatusCode.OK, move.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await staleClient.GetAsync("/api/auth/me")).StatusCode);
    }
}
