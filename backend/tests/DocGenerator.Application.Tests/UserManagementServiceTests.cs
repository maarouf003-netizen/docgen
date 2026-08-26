using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

public class UserManagementServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IUserManagementService _service;
    private readonly FakeAuditLogger _audit = new();
    private readonly PasswordHasher _hasher = new();

    public UserManagementServiceTests()
    {
        _db = TestDb.Create();
        _db.Branches.AddRange(
            new Branch { Name = "دمشق", Code = "DAM" },
            new Branch { Name = "حلب", Code = "ALP" });
        _db.SaveChanges();

        var users = new UserRepository(_db);
        var branches = new Repository<Branch>(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        _service = new UserManagementService(users, branches, uow, _hasher, tx, _audit);
    }

    public void Dispose() => _db.Dispose();

    private int DamascusId => _db.Branches.Single(b => b.Code == "DAM").Id;
    private int AleppoId => _db.Branches.Single(b => b.Code == "ALP").Id;

    private async Task<User> AddUserAsync(string username, string fullName, UserRole role, int? branchId, bool isActive = true)
    {
        var user = new User
        {
            Username = username,
            FullName = fullName,
            Role = role,
            BranchId = branchId,
            IsActive = isActive,
            PasswordHash = _hasher.Hash("123456"),        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task CreateLawyer_AddsActiveLawyerToBranch()
    {
        var lawyer = await _service.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("lawyer_x", "محامي جديد", "123456"), "head1");

        Assert.Equal("lawyer_x", lawyer.Username);
        Assert.Equal("محامي جديد", lawyer.FullName);
        Assert.True(lawyer.IsActive);
        Assert.Equal(DamascusId, lawyer.BranchId);
        Assert.Contains("create_user", _audit.Actions);
    }

    [Fact]
    public async Task CreateLawyer_DuplicateUsername_Throws()
    {
        await AddUserAsync("duplicate", "محامي موجود", UserRole.Lawyer, DamascusId);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateLawyerAsync(DamascusId, new CreateLawyerRequest("duplicate", "محامي", "123456"), "head1"));
    }

    [Fact]
    public async Task CreateLawyer_WeakPassword_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateLawyerAsync(DamascusId, new CreateLawyerRequest("lawyer_short", "محامي", "123"), "head1"));
    }

    [Fact]
    public async Task SetLawyerActive_OutsideScope_ReturnsFalse()
    {
        var aleppoLawyer = await AddUserAsync("alep", "محامي حلب", UserRole.Lawyer, AleppoId);

        var ok = await _service.SetLawyerActiveAsync(aleppoLawyer.Id, false, DamascusId, "head1");

        Assert.False(ok);
    }

    [Fact]
    public async Task SetLawyerActive_Disable_IncrementsTokenVersion()
    {
        var lawyer = await AddUserAsync("off", "محامي إيقاف", UserRole.Lawyer, DamascusId);

        var ok = await _service.SetLawyerActiveAsync(lawyer.Id, false, null, "head1");

        Assert.True(ok);
        var reloaded = await _db.Users.FindAsync(lawyer.Id);
        Assert.False(reloaded!.IsActive);
        Assert.Equal(1, reloaded.TokenVersion);
        Assert.Contains("update_user", _audit.Actions);
    }

    [Fact]
    public async Task SetLawyerActive_OnNonLawyer_ReturnsFalse()
    {
        var head = await AddUserAsync("head_x", "رئيس قسم", UserRole.Head, DamascusId);

        var ok = await _service.SetLawyerActiveAsync(head.Id, false, null, "head1");

        Assert.False(ok);
    }

    [Fact]
    public async Task ListLawyers_FiltersByBranch()
    {
        await AddUserAsync("d1", "محامي دمشق 1", UserRole.Lawyer, DamascusId);
        await AddUserAsync("a1", "محامي حلب", UserRole.Lawyer, AleppoId);

        var lawyers = await _service.ListLawyersAsync(DamascusId);

        Assert.Single(lawyers);
        Assert.Equal("d1", lawyers[0].Username);
    }

    [Fact]
    public async Task CreateUser_WithManagerRole_NoBranchRequired()
    {
        var user = await _service.CreateUserAsync(
            new CreateUserRequest("mgr_new", "مدير جديد", "manager", null, "123456"), "admin");

        Assert.Equal("manager", user.Role);
        Assert.Null(user.BranchId);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task CreateUser_LawyerWithoutBranch_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateUserAsync(new CreateUserRequest("law", "محامي", "lawyer", null, "123456"), "admin"));
    }

    [Fact]
    public async Task CreateUser_InvalidRole_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateUserAsync(new CreateUserRequest("x", "مستخدم", "مشرف", null, "123456"), "admin"));
    }

    [Fact]
    public async Task UpdateUser_SelfDisable_Throws()
    {
        var admin = await AddUserAsync("admin_x", "مشرف", UserRole.Admin, null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateUserAsync(admin.Id, new UpdateUserRequest("مشرف", "admin", null, false, null), admin.Id, "admin_x"));
    }

    [Fact]
    public async Task UpdateUser_ResetPassword_IncrementsTokenVersion()
    {
        var user = await AddUserAsync("reset", "مستخدم", UserRole.Lawyer, DamascusId);

        var updated = await _service.UpdateUserAsync(
            user.Id, new UpdateUserRequest("مستخدم", "lawyer", DamascusId, true, "654321"), 999, "admin");

        Assert.NotNull(updated);
        var reloaded = await _db.Users.FindAsync(user.Id);
        Assert.Equal(1, reloaded!.TokenVersion);
        Assert.True(_hasher.Verify("654321", reloaded.PasswordHash));
    }

    [Fact]
    public async Task UpdateUser_NotFound_ReturnsNull()
    {
        var result = await _service.UpdateUserAsync(
            999, new UpdateUserRequest("مستخدم", "lawyer", DamascusId, true, null), 1, "admin");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateLawyer_Rename_SyncsUsernameAndFullName()
    {
        var lawyer = await _service.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("محمود علي", "محمود علي", "123456"), "head1");

        var updated = await _service.UpdateLawyerAsync(
            lawyer.Id, new UpdateLawyerRequest("محمود علي حسن"), DamascusId, "head1");

        Assert.NotNull(updated);
        Assert.Equal("محمود علي حسن", updated.FullName);
        Assert.Equal("محمود علي حسن", updated.Username);
        Assert.Equal(DamascusId, updated.BranchId);
        Assert.Equal("دمشق", updated.BranchName);
        Assert.Contains("update_user", _audit.Actions);
    }

    [Fact]
    public async Task UpdateLawyer_ResetPassword_IncrementsTokenVersion()
    {
        var lawyer = await _service.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("lawyer_pw", "محامي كلمة المرور", "123456"), "head1");

        var updated = await _service.UpdateLawyerAsync(
            lawyer.Id, new UpdateLawyerRequest(null, "654321"), DamascusId, "head1");

        Assert.NotNull(updated);
        var reloaded = await _db.Users.FindAsync(lawyer.Id);
        Assert.Equal(1, reloaded!.TokenVersion);
        Assert.True(_hasher.Verify("654321", reloaded.PasswordHash));
        Assert.Equal("lawyer_pw", reloaded.Username);
    }

    [Fact]
    public async Task UpdateLawyer_OutsideScope_ReturnsNull()
    {
        var aleppoLawyer = await AddUserAsync("alep_edit", "محامي حلب", UserRole.Lawyer, AleppoId);

        var result = await _service.UpdateLawyerAsync(
            aleppoLawyer.Id, new UpdateLawyerRequest("اسم جديد"), DamascusId, "head1");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateLawyer_OnNonLawyer_ReturnsNull()
    {
        var headUser = await AddUserAsync("head_edit", "رئيس قسم", UserRole.Head, DamascusId);

        var result = await _service.UpdateLawyerAsync(
            headUser.Id, new UpdateLawyerRequest("اسم جديد"), null, "admin");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateLawyer_NotFound_ReturnsNull()
    {
        var result = await _service.UpdateLawyerAsync(
            999, new UpdateLawyerRequest("اسم جديد"), null, "head1");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateLawyer_DuplicateNameSameBranch_Throws()
    {
        await _service.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("مروان سعيد", "مروان سعيد", "123456"), "head1");
        var lawyer = await _service.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("قاسم علي", "قاسم علي", "123456"), "head1");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateLawyerAsync(lawyer.Id, new UpdateLawyerRequest("مروان سعيد"), DamascusId, "head1"));

        Assert.Contains("نفس الفرع", ex.Message);
    }

    [Fact]
    public async Task UpdateLawyer_WeakPassword_Throws()
    {
        var lawyer = await _service.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("lawyer_wp", "محامي", "123456"), "head1");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateLawyerAsync(lawyer.Id, new UpdateLawyerRequest(null, "123"), DamascusId, "head1"));
    }

    [Fact]
    public async Task UpdateLawyer_NoChanges_Throws()
    {
        var lawyer = await _service.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("lawyer_nc", "محامي", "123456"), "head1");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateLawyerAsync(lawyer.Id, new UpdateLawyerRequest(null, null), DamascusId, "head1"));

        Assert.Contains("لا يوجد تغيير", ex.Message);
    }

    [Fact]
    public async Task UpdateLawyer_EquivalentSpelling_PresentationChange_Allowed()
    {
        var lawyer = await _service.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("محمد احمد علي", "محمد احمد علي", "123456"), "head1");

        // النسخة ذات الهمزة تُطبَّع إلى اسم الدخول نفسه فلا تكون تكراراً — يُحدَّث العرض فقط.
        var updated = await _service.UpdateLawyerAsync(
            lawyer.Id, new UpdateLawyerRequest("محمد أحمد علي"), DamascusId, "head1");

        Assert.NotNull(updated);
        Assert.Equal("محمد احمد علي", updated.Username);
        Assert.Equal("محمد أحمد علي", updated.FullName);
    }

    [Fact]
    public async Task CreateLawyer_SameNameDifferentBranch_Allowed()
    {
        await AddUserAsync("محمد احمد علي", "محمد أحمد علي", UserRole.Lawyer, DamascusId);

        var lawyer = await _service.CreateLawyerAsync(AleppoId,
            new CreateLawyerRequest("محمد أحمد علي", "محمد أحمد علي", "123456"), "admin");

        Assert.Equal("محمد احمد علي", lawyer.Username);
        Assert.Equal(AleppoId, lawyer.BranchId);
    }

    [Fact]
    public async Task CreateLawyer_ArabicNameWithSpaces_AcceptedAndNormalized()
    {
        var lawyer = await _service.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("محمد أحمد علي", "محمد أحمد علي", "123456"), "head1");

        // تُخزَّن النسخة المطبّعة (أ/إ/آ → ا) لتكون معياراً موحداً للدخول والتفرد.
        Assert.Equal("محمد احمد علي", lawyer.Username);
        Assert.Equal("محمد أحمد علي", lawyer.FullName);
    }

    [Fact]
    public async Task CreateLawyer_EquivalentArabicSpelling_SameBranch_Throws()
    {
        await _service.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("محمد أحمد علي", "محمد أحمد علي", "123456"), "head1");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateLawyerAsync(DamascusId,
                new CreateLawyerRequest("محمد احمد علي", "محمد أحمد علي", "123456"), "head1"));

        Assert.Contains("نفس الفرع", ex.Message);
    }

    [Fact]
    public async Task CreateUser_SameNameSameBranch_Throws()
    {
        await _service.CreateUserAsync(
            new CreateUserRequest("خالد حسن", "خالد حسن", "head", DamascusId, "123456"), "admin");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateUserAsync(
                new CreateUserRequest("خالد حسن", "خالد حسن", "lawyer", DamascusId, "123456"), "admin"));
    }

    [Fact]
    public async Task UpdateUser_Rename_SyncsUsernameAndBlocksDuplicates()
    {
        var user = await AddUserAsync("قاسم علي", "قاسم علي", UserRole.Lawyer, DamascusId);
        await _service.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("مروان سعيد", "مروان سعيد", "123456"), "head1");

        // تغيير الاسم يحدّث اسم الدخول ليبقى مساوياً للاسم الثلاثي.
        var renamed = await _service.UpdateUserAsync(
            user.Id, new UpdateUserRequest("قاسم علي محمد", "lawyer", DamascusId, true, null), 999, "admin");
        Assert.NotNull(renamed);
        Assert.Equal("قاسم علي محمد", renamed.Username);

        // اسم مطابق لمستخدم آخر في نفس الفرع مرفوض.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateUserAsync(
                user.Id, new UpdateUserRequest("مروان سعيد", "lawyer", DamascusId, true, null), 999, "admin"));
    }

    [Fact]
    public async Task UpdateUser_LeavingEntityManagerRole_ClearsPortalBindings()
    {
        // مندوب مربوط بهوية: تحويله لمحامٍ عبر الشاشة القديمة يجب أن يفكّ نطاق
        // البوابة كليًا فلا تبقى ارتباطات خاملة تتراكم بلا دور يستخدمها.
        var delegateUser = await AddUserAsync("delegate.bind", "مندوب مربوط", UserRole.EntityManager, branchId: null);
        var group = new PublicEntityGroup { CanonicalName = "وزارة التعليم", EntityType = PublicEntityTypeCatalog.Ministry };
        group.Entries.Add(new PublicEntity { Governorate = "دمشق", BranchName = "الفرع الرئيسي", Status = EntityStatusCatalog.Final, CreatedById = delegateUser.Id });
        _db.PublicEntityGroups.Add(group);
        await _db.SaveChangesAsync();
        delegateUser.PortalGroupId = group.Id;
        await _db.SaveChangesAsync();

        var updated = await _service.UpdateUserAsync(
            delegateUser.Id, new UpdateUserRequest(delegateUser.FullName, "manager", null, true, null), 999, "admin");

        Assert.NotNull(updated);
        Assert.Equal("manager", updated.Role);
        var stored = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == delegateUser.Id);
        Assert.Null(stored.PortalGroupId);
        Assert.Null(stored.PortalEntryId);
    }
}
