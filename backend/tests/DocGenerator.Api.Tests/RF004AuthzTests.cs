using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبارات RF-004 (`ARC-002` + `BQ-001…007`): تضييق إعلاني + تثبيت المصفوفة.
/// تفشل سطور التغيير قبل الإصلاح (`403`/غياب القيد) وتخضر بعده؛ التثبيت أخضر دائمًا.
/// عزل الأسماء: مستخدمون وكيانات بأسماء فريدة (عرف `ApiTestCollection`).
/// </summary>
[Collection(ApiTestCollection.Name)]
public class RF004AuthzTests
{
    private readonly ApiFactory _factory;

    public RF004AuthzTests(ApiFactory factory) => _factory = factory;

    private static string Unique(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..20];

    [Fact]
    public async Task ManagerDeleted_SeesDeleted()
    {
        // `BQ-001`: المدير يرى المحذوفات (كان `403`).
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var id = await _factory.CreateDocumentAsync(token, borrowerName: Unique("محذوف"));
        var lawyer = _factory.WithToken(token);
        Assert.Equal(HttpStatusCode.NoContent, (await lawyer.DeleteAsync($"/api/documents/{id}")).StatusCode);

        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.GetAsync("/api/documents/deleted");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var page = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Contains(page!.RootElement.GetProperty("items").EnumerateArray(),
            e => e.GetProperty("id").GetInt32() == id);
    }

    [Fact]
    public async Task ManagerSuggestions_SeesAll()
    {
        // `BQ-001`: المدير يرى كل الاقتراحات (كان يرى ملكه فقط).
        var lawyer = _factory.AuthorizedClient("lawyer1");
        var message = Unique("اقتراح");
        var created = await lawyer.PostAsJsonAsync("/api/app-suggestions", new { message });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.GetAsync("/api/app-suggestions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ManagerSuggestions_MarkRead()
    {
        // `BQ-001`: المدير يعلّم الاقتراح مقروءًا (كان `403`).
        var lawyer = _factory.AuthorizedClient("lawyer1");
        var created = await lawyer.PostAsJsonAsync("/api/app-suggestions", new { message = Unique("قراءة") });
        using var body = await created.Content.ReadFromJsonAsync<JsonDocument>();
        var id = body!.RootElement.GetProperty("id").GetInt32();

        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.PatchAsync($"/api/app-suggestions/{id}/read",
            new StringContent(string.Empty));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ManagerCreateLawyer_Allowed()
    {
        // `BQ-001د`: المدير يدير المستخدمين عدا دور المشرف (كان `403`).
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.PostAsJsonAsync("/api/users", new
        {
            username = Unique("محام"),
            fullName = Unique("محام"),
            role = "Lawyer",
            branchId = await BranchIdAsync("DAM"),
            password = "123456",
        });
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created,
            $"unexpected {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task ManagerCreateAdmin_Forbidden()
    {
        // حد المشرف: إنشاء مشرف مشرف فقط (قبل/بعد `403` — تثبيت الحد).
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.PostAsJsonAsync("/api/users", new
        {
            username = Unique("مشرف"),
            fullName = Unique("مشرف"),
            role = "Admin",
            branchId = (int?)null,
            password = "123456",
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ManagerUpdateAdmin_Forbidden()
    {
        // حد المشرف: تعديل حساب مشرف مشرف فقط (قبل/بعد `403` — تثبيت الحد).
        int adminId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            adminId = (await db.Users.FirstAsync(u => u.Role == UserRole.Admin)).Id;
        }
        var manager = _factory.AuthorizedClient("manager");
        var response = await manager.PutAsJsonAsync($"/api/users/{adminId}", new
        {
            fullName = "مشرف معدل",
            role = (string?)null,
            branchId = (int?)null,
            isActive = true,
            password = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HeadDelegateCrossGovernorate_Forbidden()
    {
        // `BQ-002`: رئيس دمشق لا يدير مندوبي حلب (كان يُنشأ بلا قيد).
        var entryId = await SeedEntryAsync("حلب", "فرع حلب");
        var head = _factory.AuthorizedClient("head1");
        var response = await head.PostAsJsonAsync("/api/entity-portal/delegates", new
        {
            username = Unique("مندوب"),
            fullName = Unique("مندوب"),
            password = "123456",
            portalGroupId = (int?)null,
            portalEntryId = entryId,
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HeadDelegateOwnGovernorate_Allowed()
    {
        // `BQ-002`: رئيس دمشق يدير مندوبي دمشق (قبل/بعد نجاح — تثبيت عدم الكسر).
        var entryId = await SeedEntryAsync("دمشق", "الفرع الرئيسي");
        var head = _factory.AuthorizedClient("head1");
        var response = await head.PostAsJsonAsync("/api/entity-portal/delegates", new
        {
            username = Unique("مندوب"),
            fullName = Unique("مندوب"),
            password = "123456",
            portalGroupId = (int?)null,
            portalEntryId = entryId,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void NoBareAuthorizeOnApiControllers()
    {
        // حارس الفشل المغلق: كل أكشن يُصرَّح بأدوار صريحة (من التابع أو الصنف)،
        // عدا مجهولية البنية المعلنة أدناه — أي `[Authorize]` عارٍ جديد يُفشل هذا الاختبار.
        var anonymousAllow = new HashSet<(string Controller, string Action)>
        {
            ("AuthController", "Login"),
            ("AuthController", "Logout"),
            ("MetaController", "CurrentYear"),
        };
        var bare = new List<string>();
        var controllers = typeof(Program).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract
                && t.IsSubclassOf(typeof(ControllerBase))
                && t.Name.EndsWith("Controller", StringComparison.Ordinal));
        foreach (var controller in controllers)
        {
            var classRoles = string.Join(",", controller
                .GetCustomAttributes<AuthorizeAttribute>(inherit: false)
                .Select(a => a.Roles)
                .Where(r => !string.IsNullOrWhiteSpace(r)));
            var classAnon = controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: false) is not null;
            var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName
                    && m.GetCustomAttribute<NonActionAttribute>() is null
                    && m.GetCustomAttributes().OfType<HttpMethodAttribute>().Any());
            foreach (var action in actions)
            {
                var methodAnon = action.GetCustomAttribute<AllowAnonymousAttribute>(inherit: false) is not null;
                if (methodAnon || classAnon)
                {
                    if (!anonymousAllow.Contains((controller.Name, action.Name)))
                        bare.Add($"{controller.Name}.{action.Name} (مجهول غير معلن)");
                    continue;
                }
                var methodRoles = string.Join(",", action
                    .GetCustomAttributes<AuthorizeAttribute>(inherit: false)
                    .Select(a => a.Roles)
                    .Where(r => !string.IsNullOrWhiteSpace(r)));
                var effective = string.IsNullOrWhiteSpace(methodRoles) ? classRoles : methodRoles;
                if (string.IsNullOrWhiteSpace(effective))
                    bare.Add($"{controller.Name}.{action.Name}");
            }
        }
        Assert.True(bare.Count == 0, "نقاط عارية:\n" + string.Join("\n", bare));
    }

    [Fact]
    public async Task LawyerCreate_Unaffected()
    {
        // تثبيت: إنشاء المحامي يعمل قبل/بعد (لا تضييق زائد).
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var response = await client.PostAsJsonAsync("/api/documents", new
        {
            documentType = "بيان دعوى",
            borrowerName = Unique("عادي"),
            applicant = "المدعي",
            court = "دمشق",
            contractType = "تعهد",
            amountNumeric = 100,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task EntityManagerDocuments_Blocked()
    {
        // تثبيت: المندوب محجوب عن نقاط المستندات قبل/بعد.
        User manager;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            manager = new User
            {
                Username = Unique("مندوب"),
                FullName = Unique("مندوب"),
                Role = UserRole.EntityManager,
                PasswordHash = "x",
                IsActive = true,
            };
            db.Users.Add(manager);
            await db.SaveChangesAsync();
        }
        var client = _factory.ClientForUser(manager);
        var response = await client.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<int> BranchIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return (await db.Branches.SingleAsync(b => b.Code == code)).Id;
    }

    private async Task<int> SeedEntryAsync(string governorate, string branchName)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var adminId = (await db.Users.FirstAsync(u => u.Role == UserRole.Admin)).Id;
        var group = new PublicEntityGroup
        {
            CanonicalName = Unique("جهة"),
            EntityType = PublicEntityTypeCatalog.Ministry,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.PublicEntityGroups.Add(group);
        await db.SaveChangesAsync();
        var entry = new PublicEntity
        {
            GroupId = group.Id,
            Governorate = governorate,
            BranchName = branchName,
            Status = EntityStatusCatalog.Final,
            CreatedById = adminId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.PublicEntities.Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }
}
