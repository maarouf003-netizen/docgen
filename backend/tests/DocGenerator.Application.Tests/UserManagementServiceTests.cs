using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Users;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

public class UserManagementServiceTests : PostgresDbTest
{
    private readonly UserReadHandlers _reads;
    private readonly ICommandHandler<SetLawyerActiveCommand, bool> _setLawyerActive;
    private readonly ICommandHandler<CreateLawyerCommand, LawyerListItemDto> _createLawyer;
    private readonly ICommandHandler<UpdateLawyerCommand, LawyerListItemDto?> _updateLawyer;
    private readonly ICommandHandler<CreateUserCommand, UserListItemDto> _createUser;
    private readonly ICommandHandler<UpdateUserCommand, UserListItemDto?> _updateUser;
    private readonly FakeAuditLogger _audit = new();
    private readonly PasswordHasher _hasher = new();

    public UserManagementServiceTests()
    {
        _db.Branches.AddRange(
            new Branch { Name = "دمشق", Code = "DAM" },
            new Branch { Name = "حلب", Code = "ALP" });
        _db.SaveChanges();

        var users = new UserRepository(_db);
        var branches = new Repository<Branch>(_db);
        var sections = new Repository<Section>(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        var service = new UserManagementService(
            users, branches, sections,
            new Repository<HeadSuccession>(_db),
            new Repository<Correspondence>(_db),
            new Repository<CorrespondenceMessage>(_db),
            new Repository<ExecutionCircuit>(_db),
            new DocumentRepository(_db),
            new HeadAlertSuccessionService(
                new HeadAlertRepository(_db),
                new HeadAlertOutboxStore(_db),
                uow,
                _audit,
                new DbExceptionClassifier(),
                tx),
            uow);
        _reads = UserReadHandlerFactory.Create(_db);
        var rules = (IUserManagementLawyerWriteRules)service;
        _setLawyerActive = new SetLawyerActiveCommandHandler(
            rules, users, uow, _audit,
            new IPipelineBehavior<SetLawyerActiveCommand, bool>[]
            {
                new TransactionBehavior<SetLawyerActiveCommand, bool>(tx),
            });
        _createLawyer = new CreateLawyerCommandHandler(
            rules, users, branches, uow, _hasher, tx, _audit,
            Array.Empty<IPipelineBehavior<CreateLawyerCommand, LawyerListItemDto>>());
        _updateLawyer = new UpdateLawyerCommandHandler(
            rules, users, uow, _hasher, tx, _audit,
            Array.Empty<IPipelineBehavior<UpdateLawyerCommand, LawyerListItemDto?>>());
        var userRules = (IUserManagementUserWriteRules)service;
        _createUser = new CreateUserCommandHandler(
            userRules, users, new Repository<HeadSuccession>(_db), uow, _hasher, tx, _audit,
            Array.Empty<IPipelineBehavior<CreateUserCommand, UserListItemDto>>());
        _updateUser = new UpdateUserCommandHandler(
            userRules, users, uow, _hasher, tx, _audit,
            Array.Empty<IPipelineBehavior<UpdateUserCommand, UserListItemDto?>>());
    }


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
            PasswordHash = _hasher.Hash("TestPass1234"),
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    [PostgresFact]
    public async Task CreateLawyer_AddsActiveLawyerToBranch()
    {
        var lawyer = await _createLawyer.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("lawyer_x", "محامي جديد", "TestPass1234"), "head1");

        Assert.Equal("lawyer_x", lawyer.Username);
        Assert.Equal("محامي جديد", lawyer.FullName);
        Assert.True(lawyer.IsActive);
        Assert.Equal(DamascusId, lawyer.BranchId);
        Assert.Contains("create_user", _audit.Actions);
    }

    [PostgresFact]
    public async Task CreateLawyer_DuplicateUsername_Throws()
    {
        await AddUserAsync("duplicate", "محامي موجود", UserRole.Lawyer, DamascusId);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _createLawyer.CreateLawyerAsync(DamascusId, new CreateLawyerRequest("duplicate", "محامي", "TestPass1234"), "head1"));
    }

    [PostgresFact]
    public async Task CreateLawyer_WeakPassword_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _createLawyer.CreateLawyerAsync(DamascusId, new CreateLawyerRequest("lawyer_short", "محامي", "123"), "head1"));
    }

    [PostgresFact]
    public async Task SetLawyerActive_OutsideScope_ReturnsFalse()
    {
        var aleppoLawyer = await AddUserAsync("alep", "محامي حلب", UserRole.Lawyer, AleppoId);

        var ok = await _setLawyerActive.HandleAsync(new SetLawyerActiveCommand(aleppoLawyer.Id, false, DamascusId, "head1"));

        Assert.False(ok);
    }

    [PostgresFact]
    public async Task SetLawyerActive_Disable_IncrementsTokenVersion()
    {
        var lawyer = await AddUserAsync("off", "محامي إيقاف", UserRole.Lawyer, DamascusId);

        var ok = await _setLawyerActive.HandleAsync(new SetLawyerActiveCommand(lawyer.Id, false, null, "head1"));

        Assert.True(ok);
        var reloaded = await _db.Users.FindAsync(lawyer.Id);
        Assert.False(reloaded!.IsActive);
        Assert.Equal(1, reloaded.TokenVersion);
        Assert.Contains("update_user", _audit.Actions);
    }

    [PostgresFact]
    public async Task SetLawyerActive_OnNonLawyer_ReturnsFalse()
    {
        var head = await AddUserAsync("head_x", "رئيس قسم", UserRole.Head, DamascusId);

        var ok = await _setLawyerActive.HandleAsync(new SetLawyerActiveCommand(head.Id, false, null, "head1"));

        Assert.False(ok);
    }

    [PostgresFact]
    public async Task ListLawyers_FiltersByBranch()
    {
        await AddUserAsync("d1", "محامي دمشق 1", UserRole.Lawyer, DamascusId);
        await AddUserAsync("a1", "محامي حلب", UserRole.Lawyer, AleppoId);

        var lawyers = await _reads.ListLawyersAsync(DamascusId);

        Assert.Single(lawyers);
        Assert.Equal("d1", lawyers[0].Username);
    }

    [PostgresFact]
    public async Task CreateUser_WithManagerRole_NoBranchRequired()
    {
        var user = await _createUser.CreateUserAsync(
            new CreateUserRequest("mgr_new", "مدير جديد", "manager", null, "TestPass1234"), "admin");

        Assert.Equal("manager", user.Role);
        Assert.Null(user.BranchId);
        Assert.True(user.IsActive);
    }

    [PostgresFact]
    public async Task CreateUser_LawyerWithoutBranch_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _createUser.CreateUserAsync(new CreateUserRequest("law", "محامي", "lawyer", null, "TestPass1234"), "admin"));
    }

    [PostgresFact]
    public async Task CreateUser_HeadWithoutBranch_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _createUser.CreateUserAsync(new CreateUserRequest("head_nob", "رئيس بلا فرع", "head", null, "TestPass1234"), "admin"));
    }

    [PostgresFact]
    public async Task UpdateUser_RoleToLawyerWithoutBranch_Throws()
    {
        // المسار الوحيد القابل للوصول عبر الخدمة: مندوب (بلا فرع بالتصميم) يُحوَّل
        // إلى محامٍ دون فرع — `ResolveBranchAsync` يرفض بدل تثبيت حالة شاذة.
        // (الزرع المباشر لصف شاذ أصبح مستحيلًا بقيد القاعدة.)
        var user = await AddUserAsync("delegate_x", "مندوب", UserRole.EntityManager, null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _updateUser.UpdateUserAsync(user.Id, new UpdateUserRequest("مندوب", "lawyer", null, true, null), 999, "admin"));
    }

    [PostgresFact]
    public async Task CreateUser_InvalidRole_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _createUser.CreateUserAsync(new CreateUserRequest("x", "مستخدم", "مشرف", null, "TestPass1234"), "admin"));
    }

    [PostgresFact]
    public async Task UpdateUser_SelfDisable_Throws()
    {
        var admin = await AddUserAsync("admin_x", "مشرف", UserRole.Admin, null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _updateUser.UpdateUserAsync(admin.Id, new UpdateUserRequest("مشرف", "admin", null, false, null), admin.Id, "admin_x"));
    }

    [PostgresFact]
    public async Task UpdateUser_ResetPassword_IncrementsTokenVersion()
    {
        var user = await AddUserAsync("reset", "مستخدم", UserRole.Lawyer, DamascusId);

        var updated = await _updateUser.UpdateUserAsync(
            user.Id, new UpdateUserRequest("مستخدم", "lawyer", DamascusId, true, "TestPass5678"), 999, "admin");

        Assert.NotNull(updated);
        var reloaded = await _db.Users.FindAsync(user.Id);
        // (R2) الطلب يغيّر اسم الدخول ("reset" ← "مستخدم") وكلمة المرور معًا: +1 لكلٍّ منهما.
        Assert.Equal(2, reloaded!.TokenVersion);
        Assert.True(_hasher.Verify("TestPass5678", reloaded.PasswordHash));
    }

    [PostgresFact]
    public async Task UpdateUser_NotFound_ReturnsNull()
    {
        var result = await _updateUser.UpdateUserAsync(
            999, new UpdateUserRequest("مستخدم", "lawyer", DamascusId, true, null), 1, "admin");

        Assert.Null(result);
    }

    [PostgresFact]
    public async Task UpdateLawyer_Rename_SyncsUsernameAndFullName()
    {
        var lawyer = await _createLawyer.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("محمود علي", "محمود علي", "TestPass1234"), "head1");

        var updated = await _updateLawyer.UpdateLawyerAsync(
            lawyer.Id, new UpdateLawyerRequest("محمود علي حسن"), DamascusId, "head1");

        Assert.NotNull(updated);
        Assert.Equal("محمود علي حسن", updated.FullName);
        Assert.Equal("محمود علي حسن", updated.Username);
        Assert.Equal(DamascusId, updated.BranchId);
        Assert.Equal("دمشق", updated.BranchName);
        Assert.Contains("update_user", _audit.Actions);
    }

    [PostgresFact]
    public async Task UpdateLawyer_ResetPassword_IncrementsTokenVersion()
    {
        var lawyer = await _createLawyer.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("lawyer_pw", "محامي كلمة المرور", "TestPass1234"), "head1");

        var updated = await _updateLawyer.UpdateLawyerAsync(
            lawyer.Id, new UpdateLawyerRequest(null, "TestPass5678"), DamascusId, "head1");

        Assert.NotNull(updated);
        var reloaded = await _db.Users.FindAsync(lawyer.Id);
        Assert.Equal(1, reloaded!.TokenVersion);
        Assert.True(_hasher.Verify("TestPass5678", reloaded.PasswordHash));
        Assert.Equal("lawyer_pw", reloaded.Username);
    }

    [PostgresFact]
    public async Task UpdateLawyer_OutsideScope_ReturnsNull()
    {
        var aleppoLawyer = await AddUserAsync("alep_edit", "محامي حلب", UserRole.Lawyer, AleppoId);

        var result = await _updateLawyer.UpdateLawyerAsync(
            aleppoLawyer.Id, new UpdateLawyerRequest("اسم جديد"), DamascusId, "head1");

        Assert.Null(result);
    }

    [PostgresFact]
    public async Task UpdateLawyer_OnNonLawyer_ReturnsNull()
    {
        var headUser = await AddUserAsync("head_edit", "رئيس قسم", UserRole.Head, DamascusId);

        var result = await _updateLawyer.UpdateLawyerAsync(
            headUser.Id, new UpdateLawyerRequest("اسم جديد"), null, "admin");

        Assert.Null(result);
    }

    [PostgresFact]
    public async Task UpdateLawyer_NotFound_ReturnsNull()
    {
        var result = await _updateLawyer.UpdateLawyerAsync(
            999, new UpdateLawyerRequest("اسم جديد"), null, "head1");

        Assert.Null(result);
    }

    [PostgresFact]
    public async Task UpdateLawyer_DuplicateNameSameBranch_Throws()
    {
        await _createLawyer.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("مروان سعيد", "مروان سعيد", "TestPass1234"), "head1");
        var lawyer = await _createLawyer.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("قاسم علي", "قاسم علي", "TestPass1234"), "head1");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _updateLawyer.UpdateLawyerAsync(lawyer.Id, new UpdateLawyerRequest("مروان سعيد"), DamascusId, "head1"));

        Assert.Contains("نفس الفرع", ex.Message);
    }

    [PostgresFact]
    public async Task UpdateLawyer_WeakPassword_Throws()
    {
        var lawyer = await _createLawyer.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("lawyer_wp", "محامي", "TestPass1234"), "head1");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _updateLawyer.UpdateLawyerAsync(lawyer.Id, new UpdateLawyerRequest(null, "123"), DamascusId, "head1"));
    }

    [PostgresFact]
    public async Task CreateLawyer_PaddedPassword_StoresTrimmedValue()
    {
        var lawyer = await _createLawyer.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("lawyer_trim", "محامي القص", "  TestPass1234  "), "head1");

        var reloaded = await _db.Users.FindAsync(lawyer.Id);
        Assert.True(_hasher.Verify("TestPass1234", reloaded!.PasswordHash));
        Assert.False(_hasher.Verify("  TestPass1234  ", reloaded.PasswordHash));
    }

    [PostgresFact]
    public async Task UpdateLawyer_NoChanges_Throws()
    {
        var lawyer = await _createLawyer.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("lawyer_nc", "محامي", "TestPass1234"), "head1");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _updateLawyer.UpdateLawyerAsync(lawyer.Id, new UpdateLawyerRequest(null, null), DamascusId, "head1"));

        Assert.Contains("لا يوجد تغيير", ex.Message);
    }

    [PostgresFact]
    public async Task UpdateLawyer_EquivalentSpelling_PresentationChange_Allowed()
    {
        var lawyer = await _createLawyer.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("محمد احمد علي", "محمد احمد علي", "TestPass1234"), "head1");

        // النسخة ذات الهمزة تُطبَّع إلى اسم الدخول نفسه فلا تكون تكراراً — يُحدَّث العرض فقط.
        var updated = await _updateLawyer.UpdateLawyerAsync(
            lawyer.Id, new UpdateLawyerRequest("محمد أحمد علي"), DamascusId, "head1");

        Assert.NotNull(updated);
        Assert.Equal("محمد احمد علي", updated.Username);
        Assert.Equal("محمد أحمد علي", updated.FullName);
    }

    [PostgresFact]
    public async Task CreateLawyer_SameNameDifferentBranch_Allowed()
    {
        await AddUserAsync("محمد احمد علي", "محمد أحمد علي", UserRole.Lawyer, DamascusId);

        var lawyer = await _createLawyer.CreateLawyerAsync(AleppoId,
            new CreateLawyerRequest("محمد أحمد علي", "محمد أحمد علي", "TestPass1234"), "admin");

        Assert.Equal("محمد احمد علي", lawyer.Username);
        Assert.Equal(AleppoId, lawyer.BranchId);
    }

    [PostgresFact]
    public async Task CreateLawyer_ArabicNameWithSpaces_AcceptedAndNormalized()
    {
        var lawyer = await _createLawyer.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("محمد أحمد علي", "محمد أحمد علي", "TestPass1234"), "head1");

        // تُخزَّن النسخة المطبّعة (أ/إ/آ → ا) لتكون معياراً موحداً للدخول والتفرد.
        Assert.Equal("محمد احمد علي", lawyer.Username);
        Assert.Equal("محمد أحمد علي", lawyer.FullName);
    }

    [PostgresFact]
    public async Task CreateLawyer_EquivalentArabicSpelling_SameBranch_Throws()
    {
        await _createLawyer.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("محمد أحمد علي", "محمد أحمد علي", "TestPass1234"), "head1");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _createLawyer.CreateLawyerAsync(DamascusId,
                new CreateLawyerRequest("محمد احمد علي", "محمد أحمد علي", "TestPass1234"), "head1"));

        Assert.Contains("نفس الفرع", ex.Message);
    }

    [PostgresFact]
    public async Task CreateUser_SameNameSameBranch_Throws()
    {
        await _createUser.CreateUserAsync(
            new CreateUserRequest("خالد حسن", "خالد حسن", "head", DamascusId, "TestPass1234"), "admin");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _createUser.CreateUserAsync(
                new CreateUserRequest("خالد حسن", "خالد حسن", "lawyer", DamascusId, "TestPass1234"), "admin"));
    }

    [PostgresFact]
    public async Task UpdateUser_Rename_SyncsUsernameAndBlocksDuplicates()
    {
        var user = await AddUserAsync("قاسم علي", "قاسم علي", UserRole.Lawyer, DamascusId);
        await _createLawyer.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("مروان سعيد", "مروان سعيد", "TestPass1234"), "head1");

        // تغيير الاسم يحدّث اسم الدخول ليبقى مساوياً للاسم الثلاثي.
        var renamed = await _updateUser.UpdateUserAsync(
            user.Id, new UpdateUserRequest("قاسم علي محمد", "lawyer", DamascusId, true, null), 999, "admin");
        Assert.NotNull(renamed);
        Assert.Equal("قاسم علي محمد", renamed.Username);

        // اسم مطابق لمستخدم آخر في نفس الفرع مرفوض.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _updateUser.UpdateUserAsync(
                user.Id, new UpdateUserRequest("مروان سعيد", "lawyer", DamascusId, true, null), 999, "admin"));
    }

    [PostgresFact]
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

        var updated = await _updateUser.UpdateUserAsync(
            delegateUser.Id, new UpdateUserRequest(delegateUser.FullName, "manager", null, true, null), 999, "admin");

        Assert.NotNull(updated);
        Assert.Equal("manager", updated.Role);
        var stored = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == delegateUser.Id);
        Assert.Null(stored.PortalGroupId);
        Assert.Null(stored.PortalEntryId);
    }

    [PostgresFact]
    public async Task CreateUser_SecondActiveHeadSameBranch_ThrowsWithApprovedMessage()
    {
        await _createUser.CreateUserAsync(
            new CreateUserRequest("رئيس أول", "رئيس أول", "head", DamascusId, "TestPass1234"), "admin");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _createUser.CreateUserAsync(
                new CreateUserRequest("رئيس ثان", "رئيس ثان", "head", DamascusId, "TestPass1234"), "admin"));
        Assert.Equal("الفرع لا يقبل رئيسي قسم", ex.Message);
    }

    [PostgresFact]
    public async Task CreateUser_HeadDifferentBranch_Allowed()
    {
        await _createUser.CreateUserAsync(
            new CreateUserRequest("رئيس دمشق", "رئيس دمشق", "head", DamascusId, "TestPass1234"), "admin");
        var other = await _createUser.CreateUserAsync(
            new CreateUserRequest("رئيس حلب", "رئيس حلب", "head", AleppoId, "TestPass1234"), "admin");

        Assert.Equal(AleppoId, other.BranchId);
    }

    [PostgresFact]
    public async Task CreateUser_SecondHeadAfterDeactivation_Allowed()
    {
        var first = await _createUser.CreateUserAsync(
            new CreateUserRequest("رئيس سابق", "رئيس سابق", "head", DamascusId, "TestPass1234"), "admin");
        var stored = await _db.Users.SingleAsync(u => u.Id == first.Id);
        stored.IsActive = false;
        await _db.SaveChangesAsync();

        var second = await _createUser.CreateUserAsync(
            new CreateUserRequest("رئيس لاحق", "رئيس لاحق", "head", DamascusId, "TestPass1234"), "admin");
        Assert.NotNull(second);
    }

    [PostgresFact]
    public async Task UpdateUser_PromoteLawyerToHeadWithExistingHead_Throws()
    {
        await _createUser.CreateUserAsync(
            new CreateUserRequest("رئيس قائم", "رئيس قائم", "head", DamascusId, "TestPass1234"), "admin");
        var lawyer = await AddUserAsync("محام مرشح", "محام مرشح", UserRole.Lawyer, DamascusId);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _updateUser.UpdateUserAsync(
                lawyer.Id, new UpdateUserRequest(lawyer.FullName, "head", DamascusId, true, null), 999, "admin"));
    }

    [PostgresFact]
    public async Task UpdateUser_SelfHeadUpdate_Allowed()
    {
        var head = await _createUser.CreateUserAsync(
            new CreateUserRequest("رئيس ذاتي", "رئيس ذاتي", "head", DamascusId, "TestPass1234"), "admin");
        var stored = await _db.Users.SingleAsync(u => u.Id == head.Id);

        var updated = await _updateUser.UpdateUserAsync(
            stored.Id, new UpdateUserRequest("رئيس ذاتي معدل", "head", DamascusId, true, null), 999, "admin");

        Assert.NotNull(updated);
    }

    private async Task<int> AddSectionAsync(string name, int branchId)
    {
        var section = new Section { BranchId = branchId, Name = name, NameNorm = name, IsActive = true };
        _db.Sections.Add(section);
        await _db.SaveChangesAsync();
        return section.Id;
    }

    private async Task<User> AddHeadUserAsync(string username, int branchId)
    {
        var user = new User
        {
            Username = username,
            FullName = username,
            Role = UserRole.Head,
            BranchId = branchId,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<int> AddCircuitAsync(string name, int branchId, int? sectionId, int creatorId)
    {
        var circuit = new ExecutionCircuit
        {
            BranchId = branchId,
            SectionId = sectionId,
            Name = name,
            NameNorm = name,
            IsActive = true,
            CreatedById = creatorId,
        };
        _db.ExecutionCircuits.Add(circuit);
        await _db.SaveChangesAsync();
        return circuit.Id;
    }

    private async Task AddFileAsync(int lawyerId, int branchId, int? circuitId)
    {
        _db.Documents.Add(new Document
        {
            BranchId = branchId,
            CreatedById = lawyerId,
            ExecutionCircuitId = circuitId,
            BorrowerName = "مقترض",
            GeneralEntitySide = "applicant",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    [PostgresFact]
    public async Task ListLawyers_BranchMode_ReturnsFullBranch()
    {
        await AddUserAsync("محام أ", "محام أ", UserRole.Lawyer, DamascusId);
        var list = await _reads.ListLawyersAsync(DamascusId, "branch", 999, null, true, true);

        Assert.Contains(list, l => l.Username == "محام أ");
    }

    [PostgresFact]
    public async Task ListLawyers_BadMode_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _reads.ListLawyersAsync(DamascusId, "everyone", 999, null, true, true));
        Assert.Contains("mine أو branch", ex.Message);
    }

    [PostgresFact]
    public async Task ListLawyers_Mine_UnionRule()
    {
        var head = await AddHeadUserAsync("رئيس القسم", DamascusId);
        var clerk = await AddUserAsync("موظف", "موظف", UserRole.Manager, null);
        var sectionId = await AddSectionAsync("شعبة مصياف", DamascusId);
        var divisionCircuit = await AddCircuitAsync("دائرة القسم", DamascusId, null, head.Id);
        var sectionCircuit = await AddCircuitAsync("دائرة الشعبة", DamascusId, sectionId, head.Id);
        // أ: ملف في القسم. ب: ملف في الشعبة فقط. ج: صفري من إنشاء الرئيس.
        // د: قديم بلا منشئ. هـ: صفري من إنشاء غير الرئيس.
        var a = await AddUserAsync("محام أ", "محام أ", UserRole.Lawyer, DamascusId);
        var b = await AddUserAsync("محام ب", "محام ب", UserRole.Lawyer, DamascusId);
        var c = await AddUserAsync("محام ج", "محام ج", UserRole.Lawyer, DamascusId);
        var d = await AddUserAsync("محام د", "محام د", UserRole.Lawyer, DamascusId);
        var e = await AddUserAsync("محام هـ", "محام هـ", UserRole.Lawyer, DamascusId);
        await AddFileAsync(a.Id, DamascusId, divisionCircuit);
        await AddFileAsync(b.Id, DamascusId, sectionCircuit);
        foreach (var (u, creator) in new[] { (a, clerk.Id), (b, clerk.Id), (c, head.Id), (e, clerk.Id) })
        {
            var stored = await _db.Users.SingleAsync(x => x.Id == u.Id);
            stored.CreatedById = creator;
            await _db.SaveChangesAsync();
        }

        var mine = await _reads.ListLawyersAsync(DamascusId, "mine", head.Id, null, true, true);
        var names = mine.Select(l => l.Username).ToList();

        Assert.Contains("محام أ", names);
        Assert.DoesNotContain("محام ب", names);
        Assert.Contains("محام ج", names);
        Assert.Contains("محام د", names);
        Assert.DoesNotContain("محام هـ", names);
    }

    [PostgresFact]
    public async Task ListLawyers_Mine_SubHeadSeesOwnSectionFiles()
    {
        var head = await AddHeadUserAsync("رئيس القسم", DamascusId);
        var clerk = await AddUserAsync("موظف", "موظف", UserRole.Manager, null);
        var sectionId = await AddSectionAsync("شعبة مصياف", DamascusId);
        var otherId = await AddSectionAsync("شعبة أخرى", DamascusId);
        var ownCircuit = await AddCircuitAsync("دائرة الشعبة", DamascusId, sectionId, head.Id);
        var foreignCircuit = await AddCircuitAsync("دائرة الغير", DamascusId, otherId, head.Id);
        var sub = await AddUserAsync("رئيس شعبة", "رئيس شعبة", UserRole.SubHead, DamascusId);
        var own = await AddUserAsync("محام تابع", "محام تابع", UserRole.Lawyer, DamascusId);
        var foreign = await AddUserAsync("محام غريب", "محام غريب", UserRole.Lawyer, DamascusId);
        // ليسا قديمين: من إنشاء ثالث حتى لا يلتبسا بقاعدة القدامى.
        foreach (var u in new[] { own, foreign })
        {
            (await _db.Users.SingleAsync(x => x.Id == u.Id)).CreatedById = clerk.Id;
            await _db.SaveChangesAsync();
        }
        await AddFileAsync(own.Id, DamascusId, ownCircuit);
        await AddFileAsync(foreign.Id, DamascusId, foreignCircuit);

        var mine = await _reads.ListLawyersAsync(DamascusId, null, sub.Id, sectionId, true, true);
        var names = mine.Select(l => l.Username).ToList();

        Assert.Contains("محام تابع", names);
        Assert.DoesNotContain("محام غريب", names);
    }

    [PostgresFact]
    public async Task ListLawyers_Mine_ManagerBypassesToFullList()
    {
        await AddUserAsync("محام أ", "محام أ", UserRole.Lawyer, DamascusId);

        var list = await _reads.ListLawyersAsync(DamascusId, "mine", 999, null, false, true);

        Assert.Contains(list, l => l.Username == "محام أ");
    }

    [PostgresFact]
    public async Task ListLawyers_IncludeInactive_FalseExcludes()
    {
        var lawyer = await AddUserAsync("محام خامل", "محام خامل", UserRole.Lawyer, DamascusId);
        var stored = await _db.Users.SingleAsync(u => u.Id == lawyer.Id);
        stored.IsActive = false;
        await _db.SaveChangesAsync();

        Assert.Empty(await _reads.ListLawyersAsync(DamascusId, "branch", 999, null, false, false));
        Assert.NotEmpty(await _reads.ListLawyersAsync(DamascusId, "branch", 999, null, false, true));
    }

    [PostgresFact]
    public async Task CreateUser_SubHeadWithoutSection_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _createUser.CreateUserAsync(
                new CreateUserRequest("شعبة بلا", "شعبة بلا", "subhead", DamascusId, "TestPass1234"), "admin"));
        Assert.Contains("الشعبة إلزامية", ex.Message);
    }

    [PostgresFact]
    public async Task CreateUser_SubHeadFullFlow_SetsSectionAndOccupancy()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", DamascusId);

        var created = await _createUser.CreateUserAsync(
            new CreateUserRequest("رئيس شعبة", "رئيس شعبة", "subhead", DamascusId, "TestPass1234", sectionId), "admin");

        Assert.Equal(sectionId, created.SectionId);
        Assert.Equal("شعبة مصياف", created.SectionName);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _createUser.CreateUserAsync(
                new CreateUserRequest("شعبة ثان", "شعبة ثان", "subhead", DamascusId, "TestPass1234", sectionId), "admin"));
        Assert.Equal("الشعبة مشغولة برئيس مفعّل", ex.Message);
    }

    [PostgresFact]
    public async Task CreateUser_SubHeadSectionWrongBranch_Throws()
    {
        var foreignSection = await AddSectionAsync("شعبة حلب", AleppoId);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _createUser.CreateUserAsync(
                new CreateUserRequest("شعبة خطأ", "شعبة خطأ", "subhead", DamascusId, "TestPass1234", foreignSection), "admin"));
        Assert.Contains("ليست ضمن فرع الحساب", ex.Message);
    }

    [PostgresFact]
    public async Task CreateUser_HeadWithSection_Throws()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", DamascusId);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _createUser.CreateUserAsync(
                new CreateUserRequest("رئيس قسم", "رئيس قسم", "head", DamascusId, "TestPass1234", sectionId), "admin"));
        Assert.Contains("مخصصة لرئيس الشعبة", ex.Message);
    }

    [PostgresFact]
    public async Task CreateUser_Lawyer_SetsCreatedById()
    {
        var creator = await AddUserAsync("منشئ", "منشئ", UserRole.Head, DamascusId);

        var created = await _createUser.CreateUserAsync(
            new CreateUserRequest("محام جديد", "محام جديد", "lawyer", DamascusId, "TestPass1234"), "admin", ct: default, actorUserId: creator.Id);

        Assert.Equal(creator.Id, (await _db.Users.SingleAsync(u => u.Id == created.Id)).CreatedById);
    }

    [PostgresFact]
    public async Task CreateLawyer_SetsCreatedById()
    {
        var creator = await AddUserAsync("منشئ", "منشئ", UserRole.Head, DamascusId);

        var lawyer = await _createLawyer.CreateLawyerAsync(DamascusId,
            new CreateLawyerRequest("محام ميداني", "محام ميداني", "TestPass1234"), "head1", ct: default, actorUserId: creator.Id);

        Assert.Equal(creator.Id, (await _db.Users.SingleAsync(u => u.Username == lawyer.Username)).CreatedById);
    }

    [PostgresFact]
    public async Task UpdateUser_MoveSubHeadBetweenSections_BumpsToken()
    {
        var first = await AddSectionAsync("شعبة أولى", DamascusId);
        var second = await AddSectionAsync("شعبة ثانية", DamascusId);
        var created = await _createUser.CreateUserAsync(
            new CreateUserRequest("رئيس شعبة", "رئيس شعبة", "subhead", DamascusId, "TestPass1234", first), "admin");
        var before = (await _db.Users.AsNoTracking().SingleAsync(u => u.Id == created.Id)).TokenVersion;

        var updated = await _updateUser.UpdateUserAsync(
            created.Id, new UpdateUserRequest(null, null, null, true, null, second), 999, "admin");

        Assert.NotNull(updated);
        Assert.Equal(second, updated.SectionId);
        Assert.True((await _db.Users.AsNoTracking().SingleAsync(u => u.Id == created.Id)).TokenVersion > before);
    }

    [PostgresFact]
    public async Task UpdateUser_SubHeadLosesRole_ClearsSection()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", DamascusId);
        var created = await _createUser.CreateUserAsync(
            new CreateUserRequest("رئيس شعبة", "رئيس شعبة", "subhead", DamascusId, "TestPass1234", sectionId), "admin");

        var updated = await _updateUser.UpdateUserAsync(
            created.Id, new UpdateUserRequest(null, "lawyer", null, true, null), 999, "admin");

        Assert.NotNull(updated);
        Assert.Null(updated.SectionId);
        Assert.Null((await _db.Users.AsNoTracking().SingleAsync(u => u.Id == created.Id)).SectionId);
    }

    [PostgresFact]
    public async Task UpdateUser_LawyerWithSection_Throws()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", DamascusId);
        var lawyer = await AddUserAsync("محام", "محام", UserRole.Lawyer, DamascusId);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _updateUser.UpdateUserAsync(
                lawyer.Id, new UpdateUserRequest(null, null, null, true, null, sectionId), 999, "admin"));
    }

    private async Task<User> AddHeadWithSectionAsync(string username, int branchId, int sectionId)
    {
        var created = await _createUser.CreateUserAsync(
            new CreateUserRequest(username, username, "subhead", branchId, "TestPass1234", sectionId), "admin");
        return await _db.Users.SingleAsync(u => u.Id == created.Id);
    }

    private async Task<User> AddLawyerViaServiceAsync(string username, int branchId, int? creatorId)
    {
        var created = await _createUser.CreateUserAsync(
            new CreateUserRequest(username, username, "lawyer", branchId, "TestPass1234"), "admin",
            ct: default, actorUserId: creatorId);
        return await _db.Users.SingleAsync(u => u.Id == created.Id);
    }

    [PostgresFact]
    public async Task DeactivateHead_WithoutSuccessor_Throws()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", DamascusId);
        var head = await AddHeadWithSectionAsync("رئيس شعبة", DamascusId, sectionId);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _updateUser.UpdateUserAsync(
                head.Id, new UpdateUserRequest(null, null, null, false, null), 999, "admin"));
        Assert.Contains("خلفًا إجباريًا", ex.Message);
    }

    [PostgresFact]
    public async Task DeactivateHead_WithSuccessor_FullSuccession()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", DamascusId);
        var head = await AddHeadWithSectionAsync("رئيس شعبة", DamascusId, sectionId);
        var headToken = head.TokenVersion;
        // محامٍ صفري الملفات من إنشاء السلف + محامٍ بملفات (لا يُنقل).
        var zeroFile = await AddLawyerViaServiceAsync("محام صفري", DamascusId, head.Id);
        var busy = await AddLawyerViaServiceAsync("محام مشغول", DamascusId, head.Id);
        _db.Documents.Add(new Document
        {
            BranchId = DamascusId,
            CreatedById = busy.Id,
            IsDeleted = false,
            BorrowerName = "مقترض",
            GeneralEntitySide = "applicant",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        // تنبيه معلق للسلف + كتاب معلق له (بلا رد).
        var recipient = new HeadAlertRecipient { HeadAlertId = 0, UserId = head.Id };
        var alert = new HeadAlert
        {
            BranchId = DamascusId,
            CreatedById = head.Id,
            TargetType = HeadAlertTargetType.Head,
            Kind = HeadAlertKindCatalog.Legacy,
            Message = "تنبيه معلق",
            Recipients = new List<HeadAlertRecipient> { recipient },
        };
        _db.HeadAlerts.Add(alert);
        var letter = new Correspondence
        {
            BranchId = DamascusId,
            Governorate = "دمشق",
            CreatedById = busy.Id,
            TargetUserId = head.Id,
            CorrespondenceNumber = "ك-1",
            Importance = Correspondence.ImportanceNormal,
        };
        _db.Correspondences.Add(letter);
        await _db.SaveChangesAsync();

        var successor = await AddLawyerViaServiceAsync("الخلف", DamascusId, head.Id);
        var updated = await _updateUser.UpdateUserAsync(
            head.Id, new UpdateUserRequest(null, null, null, false, null, null, successor.Id), 999, "admin");

        Assert.NotNull(updated);
        Assert.False(updated.IsActive);
        // السلف عُطّل وأُبطلت جلساته.
        var storedHead = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == head.Id);
        Assert.False(storedHead.IsActive);
        Assert.True(storedHead.TokenVersion > headToken);
        // الخلف حل محله بكل شيء (الدور + الشعبة + إبطال).
        var storedSucc = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == successor.Id);
        Assert.Equal(UserRole.SubHead, storedSucc.Role);
        Assert.Equal(sectionId, storedSucc.SectionId);
        Assert.True(storedSucc.TokenVersion > 0);
        // محامي الصفر ملفات انتقل؛ صاحب الملفات بقي.
        Assert.Equal(successor.Id, (await _db.Users.AsNoTracking().SingleAsync(u => u.Id == zeroFile.Id)).CreatedById);
        Assert.Equal(head.Id, (await _db.Users.AsNoTracking().SingleAsync(u => u.Id == busy.Id)).CreatedById);
        // التنبيه والكتاب انتقلا.
        Assert.Equal(successor.Id, (await _db.HeadAlertRecipients.AsNoTracking().SingleAsync()).UserId);
        var storedLetter = await _db.Correspondences.AsNoTracking().SingleAsync();
        Assert.Equal(successor.Id, storedLetter.TargetUserId);
        Assert.Equal(sectionId, storedLetter.RecipientSectionId);
        // صفّا التعاقب (الثالث: تعيين السلف عند إنشائه) + تدقيق.
        Assert.Equal(3, _db.HeadSuccessions.Count());
        Assert.Contains(_db.HeadSuccessions.Select(h => h.Event),
            e => e == HeadSuccessionEventCatalog.Deactivated);
        Assert.Contains(_db.HeadSuccessions.Select(h => h.Event),
            e => e == HeadSuccessionEventCatalog.Succeeded);
    }

    [PostgresFact]
    public async Task DeactivateHead_ReadRecipient_StaysWithPredecessor()
    {
        // المقروء تاريخ مكتمل لا يُرحَّل — فقط المعلقة (غير المقروءة) تنتقل.
        var sectionId = await AddSectionAsync("شعبة مصياف", DamascusId);
        var head = await AddHeadWithSectionAsync("رئيس شعبة", DamascusId, sectionId);
        var alert = new HeadAlert
        {
            BranchId = DamascusId,
            CreatedById = head.Id,
            TargetType = HeadAlertTargetType.Head,
            Kind = HeadAlertKindCatalog.Legacy,
            Message = "مقروء",
            Recipients = new List<HeadAlertRecipient> { new() { UserId = head.Id, IsRead = true } },
        };
        _db.HeadAlerts.Add(alert);
        await _db.SaveChangesAsync();
        var successor = await AddLawyerViaServiceAsync("الخلف", DamascusId, head.Id);

        await _updateUser.UpdateUserAsync(
            head.Id, new UpdateUserRequest(null, null, null, false, null, null, successor.Id), 999, "admin");

        Assert.Equal(head.Id, (await _db.HeadAlertRecipients.AsNoTracking().SingleAsync()).UserId);
    }

    [PostgresFact]
    public async Task DeactivateDivisionHead_WithSuccessor_PromotesAndClearsSection()
    {
        // قرار §2.18 يسري على القسم أيضًا: تعطيل رئيس القسم بخلف (محامٍ يُرقَّى).
        var head = await AddUserAsync("رئيس قسم", "رئيس قسم", UserRole.Head, DamascusId);
        var successor = await AddLawyerViaServiceAsync("الخلف", DamascusId, head.Id);

        var updated = await _updateUser.UpdateUserAsync(
            head.Id, new UpdateUserRequest(null, null, null, false, null, null, successor.Id), 999, "admin");

        Assert.NotNull(updated);
        Assert.False(updated.IsActive);
        var storedSucc = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == successor.Id);
        Assert.Equal(UserRole.Head, storedSucc.Role);
        Assert.Null(storedSucc.SectionId);
        var events = _db.HeadSuccessions.Select(h => h.Event).ToList();
        Assert.Contains(HeadSuccessionEventCatalog.Deactivated, events);
        Assert.Contains(HeadSuccessionEventCatalog.Succeeded, events);
    }

    [PostgresFact]
    public async Task DeactivateHead_AnsweredLetter_StaysWithPredecessor()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", DamascusId);
        var head = await AddHeadWithSectionAsync("رئيس شعبة", DamascusId, sectionId);
        var lawyer = await AddLawyerViaServiceAsync("محام", DamascusId, head.Id);
        var letter = new Correspondence
        {
            BranchId = DamascusId,
            Governorate = "دمشق",
            CreatedById = lawyer.Id,
            TargetUserId = head.Id,
            CorrespondenceNumber = "ك-2",
            Importance = Correspondence.ImportanceNormal,
        };
        _db.Correspondences.Add(letter);
        await _db.SaveChangesAsync();
        _db.CorrespondenceMessages.Add(new CorrespondenceMessage
        {
            CorrespondenceId = letter.Id,
            Kind = CorrespondenceMessage.KindReply,
            BodyHtml = "رد",
            BodyPlainText = "رد",
            MessageNumber = "ر-1",
            AuthorId = head.Id,
            AuthorName = "رئيس",
        });
        await _db.SaveChangesAsync();
        var successor = await AddLawyerViaServiceAsync("الخلف", DamascusId, head.Id);

        await _updateUser.UpdateUserAsync(
            head.Id, new UpdateUserRequest(null, null, null, false, null, null, successor.Id), 999, "admin");

        // المجابة بقيت موجهة للسلف (تاريخ ثابت).
        Assert.Equal(head.Id, (await _db.Correspondences.AsNoTracking().SingleAsync()).TargetUserId);
    }

    [PostgresTheory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.EntityManager)]
    public async Task DeactivateHead_BadSuccessorRole_Throws(UserRole successorRole)
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", DamascusId);
        var head = await AddHeadWithSectionAsync("رئيس شعبة", DamascusId, sectionId);
        var bad = await AddUserAsync("خلف سيئ", "خلف سيئ", successorRole, successorRole == UserRole.EntityManager ? null : DamascusId);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _updateUser.UpdateUserAsync(
                head.Id, new UpdateUserRequest(null, null, null, false, null, null, bad.Id), 999, "admin"));
    }

    [PostgresFact]
    public async Task DeactivateHead_InactiveSuccessor_Throws()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", DamascusId);
        var head = await AddHeadWithSectionAsync("رئيس شعبة", DamascusId, sectionId);
        var off = await AddLawyerViaServiceAsync("خلف معطل", DamascusId, head.Id);
        var stored = await _db.Users.SingleAsync(u => u.Id == off.Id);
        stored.IsActive = false;
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _updateUser.UpdateUserAsync(
                head.Id, new UpdateUserRequest(null, null, null, false, null, null, off.Id), 999, "admin"));
    }

    [PostgresFact]
    public async Task DeactivateHead_ForeignBranchSuccessor_Throws()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", DamascusId);
        var head = await AddHeadWithSectionAsync("رئيس شعبة", DamascusId, sectionId);
        var foreign = await AddUserAsync("خلف أجنبي", "خلف أجنبي", UserRole.Lawyer, AleppoId);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _updateUser.UpdateUserAsync(
                head.Id, new UpdateUserRequest(null, null, null, false, null, null, foreign.Id), 999, "admin"));
    }

    [PostgresFact]
    public async Task DeactivateLawyer_WithSuccessor_Throws()
    {
        var lawyer = await AddUserAsync("محام", "محام", UserRole.Lawyer, DamascusId);
        var other = await AddUserAsync("آخر", "آخر", UserRole.Lawyer, DamascusId);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _updateUser.UpdateUserAsync(
                lawyer.Id, new UpdateUserRequest(null, null, null, false, null, null, other.Id), 999, "admin"));
    }

    [PostgresFact]
    public async Task ReactivateHead_WithActiveSuccessor_Throws()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", DamascusId);
        var head = await AddHeadWithSectionAsync("رئيس شعبة", DamascusId, sectionId);
        var successor = await AddLawyerViaServiceAsync("الخلف", DamascusId, head.Id);
        await _updateUser.UpdateUserAsync(
            head.Id, new UpdateUserRequest(null, null, null, false, null, null, successor.Id), 999, "admin");

        // إعادة تفعيل القديم مع خلف مفعّل — رفض برسالة (§9.4) لا خطأ قاعدة.
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _updateUser.UpdateUserAsync(
                head.Id, new UpdateUserRequest(null, null, null, true, null), 999, "admin"));
        Assert.Equal("الشعبة مشغولة برئيس مفعّل", ex.Message);
    }

    [PostgresFact]
    public async Task ListSuccession_OrderedDesc_WithNames()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", DamascusId);
        var head = await AddHeadWithSectionAsync("رئيس شعبة", DamascusId, sectionId);
        var successor = await AddLawyerViaServiceAsync("الخلف", DamascusId, head.Id);
        await _updateUser.UpdateUserAsync(
            head.Id, new UpdateUserRequest(null, null, null, false, null, null, successor.Id), 999, "admin");

        var rows = await _reads.ListSuccessionAsync(null);

        Assert.Equal(3, rows.Count); // تعيين + تعطيل + إحلال.
        Assert.True(rows[0].At >= rows[1].At);
        var succeeded = Assert.Single(rows, r => r.Event == HeadSuccessionEventCatalog.Succeeded);
        Assert.Equal(successor.Id, succeeded.UserId);
        Assert.Equal("شعبة مصياف", succeeded.SectionName);
        Assert.Equal("دمشق", succeeded.BranchName);
    }

    [PostgresFact]
    public async Task ListUsers_ManagerExcludesAdminAccounts_AdminSeesAll()
    {
        // W2/F4: تعليق المتحكم يعد بحد `BQ-001د`، لكن القائمة كانت تعيد حسابات
        // المشرف للمدير أيضًا (قراءة معرّفات كان يُمنع المساس بها كتابةً).
        var admin = await AddUserAsync("admin_list_w2", "مشرف", UserRole.Admin, null);
        var manager = await AddUserAsync("mgr_list_w2", "مدير", UserRole.Manager, null);
        var lawyer = await AddUserAsync("law_list_w2", "محامي", UserRole.Lawyer, DamascusId);

        var asManager = await _reads.Users.HandleAsync(new ListUsersQuery(UserRole.Manager));
        var asAdmin = await _reads.Users.HandleAsync(new ListUsersQuery(UserRole.Admin));

        Assert.DoesNotContain(asManager, u => u.Id == admin.Id);
        Assert.Contains(asManager, u => u.Id == manager.Id);
        Assert.Contains(asManager, u => u.Id == lawyer.Id);
        Assert.Contains(asAdmin, u => u.Id == admin.Id);
    }

    [PostgresFact]
    public async Task UpdateUser_ManagerCanRenameSelf_ButNotChangeOwnRole()
    {
        // W2/F5: الشرط القديم `role != Admin` منع المدير من أي تعديل ذاتي (حتى اسمه).
        var manager = await AddUserAsync("mgr_self_w2", "مدير", UserRole.Manager, null);

        var renamed = await _updateUser.UpdateUserAsync(
            manager.Id,
            new UpdateUserRequest("مدير جديد", "manager", null, true, null),
            manager.Id,
            manager.Username);

        Assert.NotNull(renamed);
        Assert.Equal("مدير جديد", renamed!.Username);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _updateUser.UpdateUserAsync(
                manager.Id,
                new UpdateUserRequest(null, "lawyer", DamascusId, true, null),
                manager.Id,
                manager.Username));
    }

    // ── W2/B3: نطاق رئيس الشعبة على حسابات المحامين ───────────────────────
    private async Task<(User Head, User SubHead, int SectionA, int SectionB)> SeedTwoSectionsAsync()
    {
        var sectionA = await AddSectionAsync("شعبة أ", DamascusId);
        var sectionB = await AddSectionAsync("شعبة ب", DamascusId);
        var head = await AddUserAsync("head_b3", "رئيس القسم", UserRole.Head, DamascusId);
        var subHead = await AddUserAsync("sub_b3", "رئيس الشعبة", UserRole.SubHead, DamascusId);
        subHead.SectionId = sectionA;
        subHead.CreatedById = head.Id;
        await _db.SaveChangesAsync();
        return (head, subHead, sectionA, sectionB);
    }

    private async Task<User> AddLawyerWithDocumentInCircuitAsync(
        string username, string circuitName, int? sectionId, int createdById)
    {
        var circuit = new ExecutionCircuit
        {
            BranchId = DamascusId,
            SectionId = sectionId,
            Name = circuitName,
            NameNorm = circuitName,
            CreatedById = createdById,
        };
        _db.ExecutionCircuits.Add(circuit);
        var lawyer = await AddUserAsync(username, username, UserRole.Lawyer, DamascusId);
        _db.Documents.Add(new Document
        {
            BranchId = DamascusId,
            CreatedById = lawyer.Id,
            ExecutionCircuitId = circuit.Id,
        });
        await _db.SaveChangesAsync();
        return lawyer;
    }

    [PostgresFact]
    public async Task UpdateLawyer_SubHeadOtherSection_ReturnsNullAndKeepsPassword()
    {
        var (head, subHead, sectionA, sectionB) = await SeedTwoSectionsAsync();
        var otherSectionLawyer = await AddLawyerWithDocumentInCircuitAsync(
            "law_other_b3", "دائرة ب", sectionB, head.Id);
        var originalHash = otherSectionLawyer.PasswordHash;

        var result = await _updateLawyer.HandleAsync(new UpdateLawyerCommand(
            otherSectionLawyer.Id,
            new UpdateLawyerRequest(null, "TestPass5678", otherSectionLawyer.Version),
            DamascusId,
            subHead.Username,
            ScopeSectionId: sectionA,
            ActorUserId: subHead.Id));

        Assert.Null(result);
        var reloaded = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == otherSectionLawyer.Id);
        Assert.Equal(originalHash, reloaded.PasswordHash);
        Assert.Equal(0, reloaded.TokenVersion);
    }

    [PostgresFact]
    public async Task UpdateLawyer_SubHeadOwnSection_Allowed()
    {
        var (head, subHead, sectionA, sectionB) = await SeedTwoSectionsAsync();
        _ = sectionB;
        var ownSectionLawyer = await AddLawyerWithDocumentInCircuitAsync(
            "law_own_b3", "دائرة أ", sectionA, head.Id);

        var result = await _updateLawyer.HandleAsync(new UpdateLawyerCommand(
            ownSectionLawyer.Id,
            new UpdateLawyerRequest(null, "TestPass5678", ownSectionLawyer.Version),
            DamascusId,
            subHead.Username,
            ScopeSectionId: sectionA,
            ActorUserId: subHead.Id));

        Assert.NotNull(result);
        var reloaded = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == ownSectionLawyer.Id);
        Assert.True(_hasher.Verify("TestPass5678", reloaded.PasswordHash));
    }

    [PostgresFact]
    public async Task UpdateLawyer_SubHeadCreatedLawyerWithoutSectionCircuit_Allowed()
    {
        var (_, subHead, sectionA, _) = await SeedTwoSectionsAsync();
        var created = await AddUserAsync("law_created_b3", "محامٍ أنشأه الرئيس", UserRole.Lawyer, DamascusId);
        created.CreatedById = subHead.Id;
        await _db.SaveChangesAsync();

        var result = await _updateLawyer.HandleAsync(new UpdateLawyerCommand(
            created.Id,
            new UpdateLawyerRequest(null, "TestPass5678", created.Version),
            DamascusId,
            subHead.Username,
            ScopeSectionId: sectionA,
            ActorUserId: subHead.Id));

        Assert.NotNull(result);
    }

    [PostgresFact]
    public async Task UpdateLawyer_SectionScopeWithoutActor_FailsClosed()
    {
        var (head, _, sectionA, sectionB) = await SeedTwoSectionsAsync();
        var lawyer = await AddLawyerWithDocumentInCircuitAsync("law_noactor", "دائرة أ", sectionA, head.Id);

        var result = await _updateLawyer.HandleAsync(new UpdateLawyerCommand(
            lawyer.Id,
            new UpdateLawyerRequest(null, "TestPass5678", lawyer.Version),
            DamascusId,
            "sub",
            ScopeSectionId: sectionA,
            ActorUserId: null));

        Assert.Null(result);
        _ = sectionB;
    }

    [PostgresFact]
    public async Task SetLawyerActive_SubHeadOtherSection_ReturnsFalse()
    {
        var (head, subHead, sectionA, sectionB) = await SeedTwoSectionsAsync();
        var otherSectionLawyer = await AddLawyerWithDocumentInCircuitAsync(
            "law_other_act", "دائرة ب", sectionB, head.Id);

        var ok = await _setLawyerActive.HandleAsync(new SetLawyerActiveCommand(
            otherSectionLawyer.Id, false, DamascusId, subHead.Username,
            ScopeSectionId: sectionA, ActorUserId: subHead.Id));

        Assert.False(ok);
        var reloaded = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == otherSectionLawyer.Id);
        Assert.True(reloaded.IsActive);
    }

    [PostgresFact]
    public async Task SetLawyerActive_HeadKeepsBranchWideScope()
    {
        var (head, _, _, sectionB) = await SeedTwoSectionsAsync();
        var otherSectionLawyer = await AddLawyerWithDocumentInCircuitAsync(
            "law_head_b3", "دائرة ب", sectionB, head.Id);

        // رئيس القسم يبقى على مستوى الفرع (قرار المنتج): لا ScopeSectionId.
        var ok = await _setLawyerActive.HandleAsync(new SetLawyerActiveCommand(
            otherSectionLawyer.Id, false, DamascusId, head.Username,
            ScopeSectionId: null, ActorUserId: head.Id));

        Assert.True(ok);
    }
}
