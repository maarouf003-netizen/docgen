using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Tests;

/// <summary>
/// بوابة الدخول للحساب الفرعي بلا فرع (رئيس قسم/رئيس شعبة/محامٍ): حالة محرّمة تُرفض بلا
/// جلسة أصلًا. تُختبر هنا بمستودع مزيف لأن قيد القاعدة يجعل تجسيد الحالة عبر
/// EF مستحيلًا — وهذا مقصود: الخدمة تحرس منطق القرار، والقاعدة تحرس التخزين.
/// </summary>
public class AuthServiceBranchGateTests
{
    private sealed class CannedUserRepository : IUserRepository
    {
        private readonly List<User> _users;
        public CannedUserRepository(List<User> users) => _users = users;

        public Task<List<User>> FindByUsernameAllAsync(string username, CancellationToken ct = default)
            => Task.FromResult(_users.Where(u => u.Username == username).ToList());
        public Task<bool> ExistsActiveHeadAsync(UserRole role, int? branchId, int? sectionId, int? excludeUserId, CancellationToken ct = default)
            => Task.FromResult(role is (UserRole.Head or UserRole.SubHead)
                && _users.Any(u => u.IsActive && u.Role == role && u.BranchId == branchId
                    && u.SectionId == sectionId && (excludeUserId == null || u.Id != excludeUserId.Value)));
        public Task<List<User>> ListUsersBySectionAsync(int sectionId, CancellationToken ct = default)
            => Task.FromResult(_users.Where(u => u.SectionId == sectionId).OrderBy(u => u.FullName).ToList());
        public Task<User?> FindActiveHeadAsync(UserRole role, int? branchId, int? sectionId, CancellationToken ct = default)
            => Task.FromResult(role is (UserRole.Head or UserRole.SubHead)
                ? _users.Where(u => u.IsActive && u.Role == role && u.BranchId == branchId && u.SectionId == sectionId).OrderBy(u => u.Id).FirstOrDefault()
                : null);
        public Task<List<User>> ListByCreatorAsync(int creatorId, CancellationToken ct = default)
            => Task.FromResult(_users.Where(u => u.CreatedById == creatorId).OrderBy(u => u.FullName).ToList());

        public Task<User?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(_users.FirstOrDefault(u => u.Id == id));
        public Task<List<User>> ListAsync(CancellationToken ct = default) => Task.FromResult(_users.ToList());
        public Task AddAsync(User entity, CancellationToken ct = default) { _users.Add(entity); return Task.CompletedTask; }
        public void Update(User entity) { }
        public void Remove(User entity) => _users.Remove(entity);
        public Task<List<User>> ListLawyersAsync(int? branchId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<User>> ListAllUsersAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> UsernameExistsAsync(string username, int? branchId, int? excludeUserId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<User>> ListEntityManagersAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<User>> ListEntityManagersByGroupIdsAsync(IReadOnlyCollection<int> groupIds, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<User>> ListEntityManagersByEntryIdAsync(int entryId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<User>> SearchCorrespondenceTargetsAsync(int excludeUserId, string? q, int limit, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<User>> SearchDocumentLawyerTargetsAsync(int documentId, int? ownerUserId, int excludeUserId, string? q, int limit, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<User>> SearchScopeDelegateTargetsAsync(IReadOnlyCollection<int> entryIds, IReadOnlyCollection<int> groupIds, int excludeUserId, string? q, int limit, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> IsDocumentLawyerTargetAsync(int documentId, int? ownerUserId, int userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> IsScopeDelegateTargetAsync(IReadOnlyCollection<int> entryIds, IReadOnlyCollection<int> groupIds, int userId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakeTransactionRunner : ITransactionRunner
    {
        public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
            => action(ct);
        public Task RunAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
            => action(ct);
    }

    private readonly PasswordHasher _hasher = new();
    private readonly FakeAuditLogger _audit = new();
    private readonly FakeTokenService _tokens = new();

    private AuthService CreateService(List<User> users) => new(
        new CannedUserRepository(users),
        new FakeUnitOfWork(),
        _hasher,
        _tokens,
        new FakeTransactionRunner(),
        _audit,
        Options.Create(new LockoutOptions()));

    private User BranchlessUser(int id, string username, UserRole role) => new()
    {
        Id = id,
        Username = username,
        FullName = username,
        Role = role,
        BranchId = null,
        IsActive = true,
        PasswordHash = _hasher.Hash("123456"),
    };

    [Theory]
    [InlineData(UserRole.Head)]
    [InlineData(UserRole.SubHead)]
    [InlineData(UserRole.Lawyer)]
    public async Task Login_BranchlessBranchRole_ReturnsBranchRequiredWithoutSession(UserRole role)
    {
        var service = CreateService(new List<User> { BranchlessUser(1, "شاذ بلا فرع", role) });

        var result = await service.LoginAsync(new LoginRequest("شاذ بلا فرع", "123456"));

        Assert.Equal(LoginStatus.BranchRequired, result.Status);
        Assert.Null(result.Response);
        Assert.Contains("login_branch_required", _audit.Actions);
    }

    [Fact]
    public async Task Login_BranchlessLawyer_DoesNotCountFailureOrLock()
    {
        var user = BranchlessUser(1, "محام شاذ", UserRole.Lawyer);
        var service = CreateService(new List<User> { user });

        // كلمة خاطئة عمدًا: الرفض يسبق التحقق منها فلا عدّ إخفاق ولا قفل
        // (عيب إداري لا تخمين) — والثانية تثبت عدم تراكم العدّاد.
        await service.LoginAsync(new LoginRequest("محام شاذ", "خطأ"));
        var result = await service.LoginAsync(new LoginRequest("محام شاذ", "خطأ"));

        Assert.Equal(LoginStatus.BranchRequired, result.Status);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockoutEndUtc);
    }

    [Fact]
    public async Task Login_LawyerWithBranch_Succeeds()
    {
        // الضابط الإيجابي: البوابة الموسعة لا تمسّ المحامي الشرعي بفرع.
        var user = BranchlessUser(1, "محامي دمشق", UserRole.Lawyer);
        user.BranchId = 7;
        var service = CreateService(new List<User> { user });

        var result = await service.LoginAsync(new LoginRequest("محامي دمشق", "123456"));

        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.NotNull(result.Response);
    }

    [Theory]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.EntityManager)]
    public async Task Login_BranchlessNonBranchRole_Succeeds(UserRole role)
    {
        // الضابط الحدودي: الأدوار بلا فرع بالتصميم لا تشملها البوابة.
        var user = BranchlessUser(1, "بلا فرع شرعي", role);
        var service = CreateService(new List<User> { user });

        var result = await service.LoginAsync(new LoginRequest("بلا فرع شرعي", "123456"));

        Assert.Equal(LoginStatus.Success, result.Status);
    }
}
