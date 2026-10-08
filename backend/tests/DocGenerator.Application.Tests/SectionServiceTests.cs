using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// إدارة الشعب (المرحلة 2أ — قرار §2 + §8): إنشاء/تسمية/تفعيل/حذف مع الوحدانية
/// الاسمية × الفرع وحراس التفريغ وسجل التعاقب عند التسمية.
///
public class SectionServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly ISectionService _service;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _damascusId;
    private readonly int _aleppoId;

    public SectionServiceTests()
    {
        _db = TestDb.Create();
        _db.Branches.AddRange(
            new Branch { Name = "دمشق", Code = "DAM", Governorate = "دمشق" },
            new Branch { Name = "حلب", Code = "ALP", Governorate = "حلب" });
        _db.SaveChanges();

        _damascusId = _db.Branches.Single(b => b.Code == "DAM").Id;
        _aleppoId = _db.Branches.Single(b => b.Code == "ALP").Id;

        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        _service = new SectionService(
            new Repository<Section>(_db),
            new Repository<Branch>(_db),
            new Repository<ExecutionCircuit>(_db),
            new UserRepository(_db),
            new Repository<HeadSuccession>(_db),
            uow, tx, _audit, new DbExceptionClassifier());
    }

    public void Dispose() => _db.Dispose();

    private async Task<User> AddHeadAsync(string username, int branchId, int? sectionId)
    {
        var user = new User
        {
            Username = username,
            FullName = username,
            Role = UserRole.SubHead,
            BranchId = branchId,
            SectionId = sectionId,
            IsActive = true,
            PasswordHash = "x",
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<ExecutionCircuit> AddCircuitAsync(string name, int branchId, int? sectionId, int creatorId)
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
        return circuit;
    }

    private async Task<User> AddManagerAsync()
    {
        var user = new User
        {
            Username = "manager_x",
            FullName = "مدير",
            Role = UserRole.Manager,
            PasswordHash = "x",
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Create_AddsActiveSection_WithZeroCounts()
    {
        var section = await _service.CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");

        Assert.True(section.Id > 0);
        Assert.Equal("شعبة مصياف", section.Name);
        Assert.True(section.IsActive);
        Assert.Equal(0, section.CircuitCount);
        Assert.Null(section.HeadName);
        Assert.Contains("create_section", _audit.Actions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Create_BlankName_Throws(string? name)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateSectionAsync(_damascusId, name, "admin"));
    }

    [Fact]
    public async Task Create_TooLongName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateSectionAsync(_damascusId, new string('س', 201), "admin"));
    }

    [Fact]
    public async Task Create_UnknownBranch_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateSectionAsync(999999, "شعبة يتيمة", "admin"));
    }

    [Fact]
    public async Task Create_DuplicateNormalizedNameSameBranch_Throws()
    {
        await _service.CreateSectionAsync(_damascusId, "شعبة أحمد", "admin");

        // الهمزة المختلفة تُطبَّع للاسم نفسه (PB-001) — تكرار مرفوض برسالة عربية.
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateSectionAsync(_damascusId, "شعبة احمد", "admin"));
        Assert.Contains("موجودة مسبقًا", ex.Message);
    }

    [Fact]
    public async Task Create_SameNameDifferentBranch_Allowed()
    {
        await _service.CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");
        var other = await _service.CreateSectionAsync(_aleppoId, "شعبة مصياف", "admin");

        Assert.Equal(_aleppoId, other.BranchId);
    }

    [Fact]
    public async Task List_ShowsCircuitCountAndHeadName()
    {
        var section = await _service.CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");
        var manager = await AddManagerAsync();
        await AddCircuitAsync("دائرة مصياف", _damascusId, section.Id, manager.Id);
        await AddCircuitAsync("دائرة أخرى", _damascusId, section.Id, manager.Id);
        await AddHeadAsync("sub_masyaf", _damascusId, section.Id);

        var list = await _service.ListSectionsAsync(_damascusId);

        var row = Assert.Single(list, s => s.Id == section.Id);
        Assert.Equal(2, row.CircuitCount);
        Assert.Equal("sub_masyaf", row.HeadName);
        Assert.Equal("دمشق", row.BranchName);
    }

    [Fact]
    public async Task List_UnknownBranch_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ListSectionsAsync(999999));
    }

    [Fact]
    public async Task GetSection_Missing_ReturnsNull()
    {
        Assert.Null(await _service.GetSectionAsync(999999));
    }

    [Fact]
    public async Task Rename_UpdatesName_WritesSuccessionWhenHeaded()
    {
        var section = await _service.CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");
        var head = await AddHeadAsync("sub_masyaf", _damascusId, section.Id);

        var renamed = await _service.RenameSectionAsync(section.Id, "شعبة السلمية", "admin");

        Assert.NotNull(renamed);
        Assert.Equal("شعبة السلمية", renamed.Name);
        Assert.Contains("rename_section", _audit.Actions);
        var succession = Assert.Single(_db.HeadSuccessions.ToList());
        Assert.Equal(HeadSuccessionEventCatalog.Renamed, succession.Event);
        Assert.Equal(head.Id, succession.UserId);
        Assert.Equal(section.Id, succession.SectionId);
    }

    [Fact]
    public async Task Rename_SameName_NoOpWithoutAudit()
    {
        var section = await _service.CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");
        var auditCount = _audit.Actions.Count;

        var same = await _service.RenameSectionAsync(section.Id, "شعبة مصياف", "admin");

        Assert.NotNull(same);
        Assert.Equal(auditCount, _audit.Actions.Count);
        Assert.Empty(_db.HeadSuccessions.ToList());
    }

    [Fact]
    public async Task Rename_HeadlessSection_NoSuccessionRow()
    {
        var section = await _service.CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");

        await _service.RenameSectionAsync(section.Id, "شعبة السلمية", "admin");

        Assert.Contains("rename_section", _audit.Actions);
        Assert.Empty(_db.HeadSuccessions.ToList());
    }

    [Fact]
    public async Task Rename_Duplicate_Throws()
    {
        await _service.CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");
        var other = await _service.CreateSectionAsync(_damascusId, "شعبة السلمية", "admin");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RenameSectionAsync(other.Id, "شعبة مصياف", "admin"));
    }

    [Fact]
    public async Task Rename_Missing_ReturnsNull()
    {
        Assert.Null(await _service.RenameSectionAsync(999999, "اسم", "admin"));
    }

    [Fact]
    public async Task SetActive_DeactivateWithCircuits_Throws()
    {
        var section = await _service.CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");
        var manager = await AddManagerAsync();
        await AddCircuitAsync("دائرة مصياف", _damascusId, section.Id, manager.Id);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SetActiveAsync(section.Id, false, "admin"));
        Assert.Contains("انقل دوائرها", ex.Message);
    }

    [Fact]
    public async Task SetActive_DeactivateEmpty_Succeeds()
    {
        var section = await _service.CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");

        var off = await _service.SetActiveAsync(section.Id, false, "admin");

        Assert.NotNull(off);
        Assert.False(off.IsActive);
        Assert.Contains("deactivate_section", _audit.Actions);

        var on = await _service.SetActiveAsync(section.Id, true, "admin");
        Assert.NotNull(on);
        Assert.True(on.IsActive);
    }

    [Fact]
    public async Task SetActive_Missing_ReturnsNull()
    {
        Assert.Null(await _service.SetActiveAsync(999999, false, "admin"));
    }

    [Fact]
    public async Task Delete_WithCircuits_Throws()
    {
        var section = await _service.CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");
        var manager = await AddManagerAsync();
        await AddCircuitAsync("دائرة مصياف", _damascusId, section.Id, manager.Id);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.DeleteSectionAsync(section.Id, "admin"));
        Assert.Contains("انقل دوائرها", ex.Message);
    }

    [Fact]
    public async Task Delete_WithUsers_Throws()
    {
        var section = await _service.CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");
        await AddHeadAsync("sub_masyaf", _damascusId, section.Id);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.DeleteSectionAsync(section.Id, "admin"));
        Assert.Contains("حساباتها", ex.Message);
    }

    [Fact]
    public async Task Delete_Empty_Succeeds()
    {
        var section = await _service.CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");

        Assert.True(await _service.DeleteSectionAsync(section.Id, "admin"));
        Assert.Null(await _db.Sections.FindAsync(section.Id));
        Assert.Contains("delete_section", _audit.Actions);
    }

    [Fact]
    public async Task Delete_Missing_ReturnsFalse()
    {
        Assert.False(await _service.DeleteSectionAsync(999999, "admin"));
    }
}
