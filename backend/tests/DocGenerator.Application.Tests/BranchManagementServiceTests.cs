using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

public class BranchManagementServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IBranchManagementService _service;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _damascusId;
    private readonly int _aleppoId;

    public BranchManagementServiceTests()
    {
        _db = TestDb.Create();
        _db.Branches.AddRange(
            new Branch { Name = "دمشق", Code = "DAM" },
            new Branch { Name = "حلب", Code = "ALP" });
        _db.SaveChanges();

        _damascusId = _db.Branches.Single(b => b.Code == "DAM").Id;
        _aleppoId = _db.Branches.Single(b => b.Code == "ALP").Id;

        var branches = new BranchRepository(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        _service = new BranchManagementService(branches, uow, tx, _audit);
    }

    public void Dispose() => _db.Dispose();

    private int DamascusId => _damascusId;
    private int AleppoId => _aleppoId;

    [Fact]
    public async Task Create_AddsActiveBranch_WithCounts()
    {
        var branch = await _service.CreateBranchAsync(
            new CreateBranchRequest("حمص", "HMS", "شارع الساعة", "031111111"), "admin");

        Assert.True(branch.Id > 0);
        Assert.Equal("حمص", branch.Name);
        Assert.Equal("HMS", branch.Code);
        Assert.True(branch.IsActive);
        Assert.Equal(0, branch.UserCount);
        Assert.Equal(0, branch.DocumentCount);
        Assert.Contains("create_branch", _audit.Actions);
    }

    [Fact]
    public async Task Create_EmptyName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateBranchAsync(new CreateBranchRequest("  ", "X1", null, null), "admin"));
    }

    [Fact]
    public async Task Create_EmptyCode_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateBranchAsync(new CreateBranchRequest("فرع جديد", " ", null, null), "admin"));
    }

    [Fact]
    public async Task Create_DuplicateName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateBranchAsync(new CreateBranchRequest("دمشق", "NEW", null, null), "admin"));
    }

    [Fact]
    public async Task Create_DuplicateCode_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateBranchAsync(new CreateBranchRequest("فرع جديد", "DAM", null, null), "admin"));
    }

    [Fact]
    public async Task Update_ModifiesFields_AndDeactivates()
    {
        var updated = await _service.UpdateBranchAsync(
            DamascusId,
            new UpdateBranchRequest("دمشق المركز", "DAMC", "شارع البريد", "011123456", false),
            "admin");

        Assert.NotNull(updated);
        Assert.Equal("دمشق المركز", updated.Name);
        Assert.Equal("DAMC", updated.Code);
        Assert.False(updated.IsActive);
        Assert.Equal("011123456", updated.Phone);
        Assert.Contains("update_branch", _audit.Actions);
    }

    [Fact]
    public async Task Update_DuplicateNameExcludingSelf_Throws()
    {
        // حلب تأخذ اسم "دمشق" — ممنوع (فرع آخر بنفس الاسم).
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateBranchAsync(AleppoId, new UpdateBranchRequest("دمشق", "ALP", null, null, true), "admin"));
    }

    [Fact]
    public async Task Update_SameNameSelf_IsAllowed()
    {
        // الإبقاء على نفس الاسم والكود عند التحديث لا يعد تكراراً.
        var updated = await _service.UpdateBranchAsync(
            DamascusId,
            new UpdateBranchRequest("دمشق", "DAM", null, null, true),
            "admin");

        Assert.NotNull(updated);
        Assert.Equal("دمشق", updated.Name);
    }

    [Fact]
    public async Task Update_NotFound_ReturnsNull()
    {
        var result = await _service.UpdateBranchAsync(
            999, new UpdateBranchRequest("غير موجود", "XXX", null, null, true), "admin");

        Assert.Null(result);
    }

    [Fact]
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

    [Fact]
    public async Task Delete_UnusedBranch_Succeeds()
    {
        var ok = await _service.DeleteBranchAsync(AleppoId, "admin");

        Assert.True(ok);
        Assert.Null(await _db.Branches.FindAsync(AleppoId));
        Assert.Contains("delete_branch", _audit.Actions);
    }

    [Fact]
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
            _service.DeleteBranchAsync(DamascusId, "admin"));

        Assert.Contains("مستخدمين", ex.Message);
        Assert.NotNull(await _db.Branches.FindAsync(DamascusId));
    }

    [Fact]
    public async Task Delete_BranchWithDocuments_Throws()
    {
        var user = new User
        {
            Username = "doc_owner",
            FullName = "محامي",
            Role = UserRole.Lawyer,
            BranchId = null,
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
            _service.DeleteBranchAsync(DamascusId, "admin"));

        Assert.Contains("مستندات", ex.Message);
        Assert.NotNull(await _db.Branches.FindAsync(DamascusId));
    }

    [Fact]
    public async Task Delete_NotFound_ReturnsFalse()
    {
        var ok = await _service.DeleteBranchAsync(999, "admin");

        Assert.False(ok);
    }
}
