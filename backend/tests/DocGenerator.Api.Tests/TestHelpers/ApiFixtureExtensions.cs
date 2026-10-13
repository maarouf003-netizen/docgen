using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests.TestHelpers;

/// <summary>
/// مساعدات `Api` المركزية (R1-1): التوحيد الشكلي لمولدات الأسماء الفريدة
/// ومسارات البذر المتكررة (فرع/شعبة/رئيس شعبة/محامٍ) — بلا أي تغيير دلالي:
/// كل مساعد نسخة حرفية للمتغيرات المحلية التي وحّدها (نفس النقاط الطرفية،
/// نفس الأدوار، نفس الاقتطاع `[..24]` المحترم لحد `Username` الأربعين).
/// المستثناة عمدًا (آثار جانبية مقصودة): `HeadBranchIdAsync` (يُثبّت
/// `Governorate` كأثر جانبي) و`BranchIdOfHeadAsync` و`GetBranchIdAsync(code)`
/// المخصصة — تبقى محلية وتُوثَّق في مواضعها.
/// </summary>
public static class ApiFixtureExtensions
{
    /// <summary>اسم فريد قصير (`prefix` + `Guid`) — الاقتطاع الواحد `[..24]`.</summary>
    public static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..24];

    /// <summary>معرف فرع برمزه — بحث صرف بلا آثار جانبية.</summary>
    public static async Task<int> GetBranchIdAsync(this ApiFactory factory, string code)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return (await db.Branches.AsNoTracking().SingleAsync(b => b.Code == code)).Id;
    }

    /// <summary>معرف فرع دمشق (`DAM`) مع ضمان `Governorate` — سلوك `BranchIdAsync()` الشائع.</summary>
    public static async Task<int> GetDamascusIdAsync(this ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var branch = await db.Branches.SingleAsync(b => b.Code == "DAM");
        if (string.IsNullOrWhiteSpace(branch.Governorate))
        {
            branch.Governorate = "دمشق";
            await db.SaveChangesAsync();
        }
        return branch.Id;
    }

    /// <summary>إنشاء شعبة عبر الـ API — العميل مدير افتراضيًا (صلاحية مؤكدة).</summary>
    public static async Task<int> CreateSectionAsync(this ApiFactory factory, string name, int? branchId = null)
    {
        var admin = factory.AuthorizedClient("admin");
        var response = await admin.PostAsJsonAsync("/api/sections", new
        {
            branchId = branchId ?? await factory.GetDamascusIdAsync(),
            name,
        });
        response.EnsureSuccessStatusCode();
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("id").GetInt32();
    }

    /// <summary>إنشاء رئيس شعبة وتسجيل دخوله — يُرجع التوكن.</summary>
    public static async Task<string> CreateSubHeadTokenAsync(this ApiFactory factory, int sectionId, int? branchId = null)
    {
        var admin = factory.AuthorizedClient("admin");
        var username = UniqueName("sub");
        var created = await admin.PostAsJsonAsync("/api/users", new
        {
            username,
            fullName = username,
            role = "subhead",
            branchId = branchId ?? await factory.GetDamascusIdAsync(),
            password = "123456",
            sectionId,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await factory.LoginAsync(username, "123456"))!.Token!;
    }

    /// <summary>إنشاء فرع مباشرة في القاعدة برمز فريد — بلا مرور بالـ API.</summary>
    /// <param name="governorate">المحافظة الاختيارية — تُمرَّر صراحةً عندما يعتمد الاختبار على نطاق محافظة
    /// (كمحافظة مختلفة عمدًا لاختبارات العزل؛ وحدانية الرئيس المفعّل لكل فرع — §2.26).</param>
    public static async Task<int> CreateBranchAsync(this ApiFactory factory, string name, string? governorate = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var branch = new Branch { Name = name, Code = $"TX_{Guid.NewGuid():N}"[..12].ToUpperInvariant(), Governorate = governorate };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        return branch.Id;
    }

    /// <summary>معرف مستخدم باسمه.</summary>
    public static async Task<int> UserIdAsync(this ApiFactory factory, string username)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return (await db.Users.SingleAsync(u => u.Username == username)).Id;
    }

    /// <summary>محامٍ جديد عبر الـ API مع توكن دخوله — يُرجع المعرف والتوكن.</summary>
    public static async Task<(int Id, string Token)> NewLawyerAsync(this ApiFactory factory, int? branchId = null)
    {
        var admin = factory.AuthorizedClient("admin");
        var username = UniqueName("law");
        var created = await admin.PostAsJsonAsync("/api/users/lawyers", new
        {
            username,
            fullName = username,
            password = "123456",
            branchId = branchId ?? await factory.GetDamascusIdAsync(),
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var doc = await created.Content.ReadFromJsonAsync<JsonDocument>();
        var token = (await factory.LoginAsync(username, "123456"))!.Token!;
        return (doc!.RootElement.GetProperty("id").GetInt32(), token);
    }

    /// <summary>محامٍ جديد مع عميل مُصادَق جاهز (حقن التوكن) — يُرجع المعرف والعميل.</summary>
    public static async Task<(int Id, HttpClient Client)> NewLawyerClientAsync(this ApiFactory factory, int? branchId = null)
    {
        var (id, token) = await NewLawyerAsync(factory, branchId);
        return (id, factory.ClientWithToken(token));
    }
}
