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
        // الضابط الإيجابي: الصف الشرعي بفرع يمرّ طبيعيًا.
        int damascusId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGenerator.Infrastructure.Persistence.DocGeneratorDbContext>();
            damascusId = db.Branches.Single(b => b.Code == "DAM").Id;
        }

        var user = await _factory.CreateUserAsync($"ok_{role}_{Guid.NewGuid():N}"[..24], role, branchId: damascusId);

        Assert.Equal(damascusId, user.BranchId);
    }
}
