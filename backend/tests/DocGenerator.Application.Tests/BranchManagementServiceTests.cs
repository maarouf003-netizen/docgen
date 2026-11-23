using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Branches;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

public class BranchManagementServiceTests : PostgresDbTest
{
    private readonly IBranchManagementService _service;
    private readonly ICommandHandler<DeleteBranchCommand, bool> _deleteBranch;
    private readonly ICommandHandler<CreateBranchCommand, BranchDto> _createBranch;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _damascusId;
    private readonly int _aleppoId;

    public BranchManagementServiceTests()
    {
        _db.Branches.AddRange(
            new Branch { Name = "دمشق", Code = "DAM", Governorate = "دمشق" },
            new Branch { Name = "حلب", Code = "ALP", Governorate = "حلب" });
        _db.SaveChanges();

        _damascusId = _db.Branches.Single(b => b.Code == "DAM").Id;
        _aleppoId = _db.Branches.Single(b => b.Code == "ALP").Id;

        var branches = new BranchRepository(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        _service = new BranchManagementService(branches, uow, tx, _audit);
        _deleteBranch = new DeleteBranchCommandHandler(
            branches, uow, _audit, Array.Empty<IPipelineBehavior<DeleteBranchCommand, bool>>());
        _createBranch = new CreateBranchCommandHandler(
            branches, uow, _audit, Array.Empty<IPipelineBehavior<CreateBranchCommand, BranchDto>>());
    }

    private Task<BranchDto> CreateBranchAsync(CreateBranchRequest request, string? actorName)
        => _createBranch.HandleAsync(new CreateBranchCommand(request, actorName));

    [PostgresFact]
    public async Task ListBranches_CacheInvalidatedAfterCreate()
    {
        var cache = new DocGenerator.Infrastructure.Caching.ReferenceDataCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCache(
                new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
        var cachedService = new BranchManagementService(
            new BranchRepository(_db), new UnitOfWork(_db), new TransactionRunner(_db), _audit, cache);

        var cachedCreate = new CreateBranchCommandHandler(
            new BranchRepository(_db), new UnitOfWork(_db), _audit,
            Array.Empty<IPipelineBehavior<CreateBranchCommand, BranchDto>>(), cache);

        _ = await cachedService.ListBranchesAsync();
        var created = await cachedCreate.HandleAsync(new CreateBranchCommand(
            new CreateBranchRequest("طرطوس", "TRT", null, null, "طرطوس"), "admin"));
        var after = await cachedService.ListBranchesAsync();

        Assert.Contains(after, b => b.Id == created.Id);
    }


    private int DamascusId => _damascusId;
    private int AleppoId => _aleppoId;

    [PostgresFact]
    public async Task Create_AddsActiveBranch_WithCounts()
    {
        var branch = await CreateBranchAsync(
            new CreateBranchRequest("حمص", "HMS", "شارع الساعة", "031111111", "حمص"), "admin");

        Assert.True(branch.Id > 0);
        Assert.Equal("حمص", branch.Name);
        Assert.Equal("HMS", branch.Code);
        Assert.Equal("حمص", branch.Governorate);
        Assert.True(branch.IsActive);
        Assert.Equal(0, branch.UserCount);
        Assert.Equal(0, branch.DocumentCount);
        Assert.Contains("create_branch", _audit.Actions);
        Assert.Contains(_audit.Details, d => d is not null && d.StartsWith("أنشأ فرعاً: حمص (HMS)", StringComparison.Ordinal));
    }

    [PostgresFact]
    public async Task Create_EmptyGovernorate_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateBranchAsync(new CreateBranchRequest("فرع جديد", "BXX", null, null, "  "), "admin"));

        Assert.Contains("المحافظة مطلوبة", ex.Message);
        Assert.Null(await _db.Branches.FirstOrDefaultAsync(b => b.Code == "BXX"));
    }

    [PostgresFact]
    public async Task Create_EmptyName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateBranchAsync(new CreateBranchRequest("  ", "X1", null, null), "admin"));
    }

    [PostgresFact]
    public async Task Create_EmptyCode_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateBranchAsync(new CreateBranchRequest("فرع جديد", " ", null, null), "admin"));
    }

    [PostgresFact]
    public async Task Create_DuplicateName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateBranchAsync(new CreateBranchRequest("دمشق", "NEW", null, null, "دمشق"), "admin"));
    }

    [PostgresFact]
    public async Task Create_DuplicateCode_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateBranchAsync(new CreateBranchRequest("فرع جديد", "DAM", null, null, "حلب"), "admin"));
    }

    [PostgresFact]
    public async Task Update_ModifiesFields_AndDeactivates()
    {
        var updated = await _service.UpdateBranchAsync(
            DamascusId,
            new UpdateBranchRequest("دمشق المركز", "DAMC", "شارع البريد", "011123456", false, "دمشق"),
            "admin");

        Assert.NotNull(updated);
        Assert.Equal("دمشق المركز", updated.Name);
        Assert.Equal("DAMC", updated.Code);
        Assert.Equal("دمشق", updated.Governorate);
        Assert.False(updated.IsActive);
        Assert.Equal("011123456", updated.Phone);
        Assert.Contains("update_branch", _audit.Actions);
    }

    [PostgresFact]
    public async Task Update_EmptyGovernorate_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateBranchAsync(DamascusId, new UpdateBranchRequest("دمشق", "DAM", null, null, true, "  "), "admin"));

        Assert.Contains("المحافظة مطلوبة", ex.Message);
        Assert.Equal("دمشق", (await _db.Branches.FindAsync(DamascusId))!.Governorate);
    }

    [PostgresFact]
    public async Task Update_DuplicateNameExcludingSelf_Throws()
    {
        // حلب تأخذ اسم "دمشق" — ممنوع (فرع آخر بنفس الاسم).
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateBranchAsync(AleppoId, new UpdateBranchRequest("دمشق", "ALP", null, null, true, "حلب"), "admin"));
    }

    [PostgresFact]
    public async Task Update_SameNameSelf_IsAllowed()
    {
        // الإبقاء على نفس الاسم والكود عند التحديث لا يعد تكراراً.
        var updated = await _service.UpdateBranchAsync(
            DamascusId,
            new UpdateBranchRequest("دمشق", "DAM", null, null, true, "دمشق"),
            "admin");

        Assert.NotNull(updated);
        Assert.Equal("دمشق", updated.Name);
    }

    [PostgresFact]
    public async Task Update_NotFound_ReturnsNull()
    {
        var result = await _service.UpdateBranchAsync(
            999, new UpdateBranchRequest("غير موجود", "XXX", null, null, true), "admin");

        Assert.Null(result);
    }

    [PostgresFact]
    public async Task Update_StaleVersion_ThrowsConflict_WithoutPersistingOrAuditing()
    {
        // P0-06: النسخة القادمة من العميل لا تطابق المحمّلة — رفض مبكر 409.
        var ex = await Assert.ThrowsAsync<DocumentConflictException>(() =>
            _service.UpdateBranchAsync(
                DamascusId,
                new UpdateBranchRequest("اسم مبتذل", "DAM", null, null, true, "دمشق", 99),
                "admin"));

        Assert.Contains("الفرع", ex.Message);
        Assert.Equal("دمشق", (await _db.Branches.AsNoTracking().SingleAsync(b => b.Id == DamascusId)).Name);
        Assert.DoesNotContain("update_branch", _audit.Actions);
    }

    [PostgresFact]
    public async Task Update_MatchingVersion_SucceedsAndReportsIncrementedVersion()
    {
        var stored = await _db.Branches.AsNoTracking().SingleAsync(b => b.Id == DamascusId);
        Assert.Equal(0, stored.Version);

        var updated = await _service.UpdateBranchAsync(
            DamascusId,
            new UpdateBranchRequest("دمشق الجديدة", "DAM", null, null, true, "دمشق", stored.Version),
            "admin");

        Assert.NotNull(updated);
        Assert.Equal(1, updated.Version);
    }

    [PostgresFact]
    public async Task List_ReportsUserAndDocumentCounts()
    {
        var user = new User
        {
            Username = "l1",
            FullName = "محامي دمشق",
            Role = UserRole.Lawyer,
            BranchId = DamascusId,
            PasswordHash = new PasswordHasher().Hash("123456"),
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        _db.Documents.Add(new Document
        {
            BranchId = DamascusId,
            CreatedById = user.Id,
            IsDraft = false,
            BorrowerName = "أحمد",
            AmountNumeric = 0,
            ExecStatus = string.Empty,
        });
        await _db.SaveChangesAsync();

        var branches = await _service.ListBranchesAsync();

        var damascus = branches.Single(b => b.Code == "DAM");
        Assert.Equal(1, damascus.UserCount);
        Assert.Equal(1, damascus.DocumentCount);
        Assert.Equal(0, branches.Single(b => b.Code == "ALP").UserCount);
    }

    [PostgresFact]
    public async Task Delete_UnusedBranch_Succeeds()
    {
        var ok = await _deleteBranch.HandleAsync(new DeleteBranchCommand(AleppoId, "admin"));

        Assert.True(ok);
        Assert.Null(await _db.Branches.FindAsync(AleppoId));
        Assert.Contains("delete_branch", _audit.Actions);
    }

    [PostgresFact]
    public async Task Delete_BranchWithUsers_Throws()
    {
        _db.Users.Add(new User
        {
            Username = "u1",
            FullName = "مستخدم",
            Role = UserRole.Lawyer,
            BranchId = DamascusId,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _deleteBranch.HandleAsync(new DeleteBranchCommand(DamascusId, "admin")));

        Assert.Contains("مستخدمين", ex.Message);
        Assert.NotNull(await _db.Branches.FindAsync(DamascusId));
    }

    [PostgresFact]
    public async Task Delete_BranchWithDocuments_Throws()
    {
        var user = new User
        {
            Username = "doc_owner",
            FullName = "محامي",
            Role = UserRole.Lawyer,
            // فرع مختلف عن المحذوف عمدًا: فحص المستخدمين يسبق فحص المستندات،
            // فمالك في الفرع المحذوف كان سيُفشل الاختبار برسالة «مستخدمين».
            // (محامٍ بلا فرع حالة محرّمة يمنعها قيد القاعدة CK_Users_BranchRequiredForBranchRoles)
            BranchId = AleppoId,
            PasswordHash = new PasswordHasher().Hash("123456"),
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        _db.Documents.Add(new Document
        {
            BranchId = DamascusId,
            CreatedById = user.Id,
            IsDraft = false,
            BorrowerName = "أحمد",
            AmountNumeric = 0,
            ExecStatus = string.Empty,
        });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _deleteBranch.HandleAsync(new DeleteBranchCommand(DamascusId, "admin")));

        Assert.Contains("مستندات", ex.Message);
        Assert.NotNull(await _db.Branches.FindAsync(DamascusId));
    }

    [PostgresFact]
    public async Task Delete_NotFound_ReturnsFalse()
    {
        var ok = await _deleteBranch.HandleAsync(new DeleteBranchCommand(999, "admin"));

        Assert.False(ok);
    }
}
