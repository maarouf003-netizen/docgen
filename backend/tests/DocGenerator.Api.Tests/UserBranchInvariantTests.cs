using System.Net;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// قيد القاعدة <c>CK_Users_BranchRequiredForBranchRoles</c>: الصف الشاذ (محامٍ/رئيس
/// بلا فرع) مرفوض على مستوى التخزين نفسه — يشمل الكتابة المباشرة والاستعادات،
/// لا خدمة الإدارة وحدها. يعمل على قاعدة SQLite مهاجَّرة فعليًا (المصنع يطبق
/// الهجرات عند الإقلاع)، والأدوار بلا فرع بالتصميم تبقى صالحة (ضباط شرعية).
/// </summary>
public sealed class UserBranchInvariantTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(UserRole.Lawyer)]
    [InlineData(UserRole.Head)]
    [InlineData(UserRole.SubHead)]
    public async Task BranchRole_WithoutBranch_InsertThrows(UserRole role)
    {
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            _factory.CreateUserAsync($"nobranch_{role}_{Guid.NewGuid():N}"[..24], role, branchId: null));

        Assert.Contains("CK_Users_BranchRequiredForBranchRoles", ex.ToString());
    }

    [Theory]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.EntityManager)]
    public async Task BranchlessByDesignRole_WithoutBranch_InsertsFine(UserRole role)
    {
        // الضابط الشرعي: الأدوار بلا فرع بالتصميم لا يمسّها القيد.
        var user = await _factory.CreateUserAsync($"free_{role}_{Guid.NewGuid():N}"[..24], role, branchId: null);

        Assert.Null(user.BranchId);
    }

    [Theory]
    [InlineData(UserRole.Lawyer)]
    [InlineData(UserRole.Head)]
    public async Task BranchRole_WithBranch_InsertsFine(UserRole role)
    {
        // الضابط الإيجابي: الصف الشرعي بفرع يمرّ طبيعيًا — ورئيس القسم في فرعٍ
        // جديد (وحدانية الرئيس المفعّل لكل فرع — قرار §2.26: دمشق فيها head1).
        int branchId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGenerator.Infrastructure.Persistence.DocGeneratorDbContext>();
            if (role == UserRole.Head)
            {
                var fresh = new DocGenerator.Domain.Entities.Branch { Name = "فرع اختبار", Code = $"TST_{Guid.NewGuid():N}"[..12].ToUpperInvariant() };
                db.Branches.Add(fresh);
                await db.SaveChangesAsync();
                branchId = fresh.Id;
            }
            else
            {
                branchId = db.Branches.Single(b => b.Code == "DAM").Id;
            }
        }

        var user = await _factory.CreateUserAsync($"ok_{role}_{Guid.NewGuid():N}"[..24], role, branchId: branchId);

        Assert.Equal(branchId, user.BranchId);
    }

    [Fact]
    public async Task SecondActiveHeadSameBranch_ViolatesPartialUniqueIndex()
    {
        // ظهر قاعدة البيانات للوحدانية (قرار §2.26): رئيسا قسم مفعّلان في الفرع
        // نفسه مرفوضان على مستوى التخزين — دمشق فيها head1 المزروع.
        int damascusId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGenerator.Infrastructure.Persistence.DocGeneratorDbContext>();
            damascusId = db.Branches.Single(b => b.Code == "DAM").Id;
        }

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() =>
            _factory.CreateUserAsync($"dup_head_{Guid.NewGuid():N}"[..24], UserRole.Head, branchId: damascusId));

        // `SQLite` يسمّي العمود لا الفهرس في رسالته.
        Assert.Contains("UNIQUE constraint failed: Users.BranchId", ex.ToString());
    }

    [Fact]
    public async Task SectionlessSubHead_HeadEndpoints_Forbidden()
    {
        // الحالة الانتقالية (1أ–1ج) مغلقة: حساب رئيس شعبة بفرع وبلا شعبة يدخل
        // (بوابة الفرع) لكن كل نقاط الرئاسة ترفضه `403` — السمات لم تُحوَّل بعد
        // عمدًا (قرار §5.1 التنفيذي: التحويل مع الفلترة مرحليًا).
        int damascusId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGenerator.Infrastructure.Persistence.DocGeneratorDbContext>();
            damascusId = db.Branches.Single(b => b.Code == "DAM").Id;
        }
        var sub = await _factory.CreateUserAsync($"sub_noscope_{Guid.NewGuid():N}"[..24], UserRole.SubHead, branchId: damascusId);

        var login = await _factory.LoginAsync(sub.Username, "123456");
        Assert.Equal(200, login!.StatusCode);
        Assert.NotNull(login.Token);

        var client = _factory.CreateClient();
        client.SetAuthCookie(login.Token!);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/execution-circuits/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/appeals")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/delegations/pending")).StatusCode);
    }
}
