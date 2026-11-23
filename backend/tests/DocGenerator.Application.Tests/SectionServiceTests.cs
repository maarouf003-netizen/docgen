using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Sections;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

/// <summary>
/// إدارة الشعب (المرحلة 2أ — قرار §2 + §8): إنشاء/تسمية/تفعيل/حذف مع الوحدانية
/// الاسمية × الفرع وحراس التفريغ وسجل التعاقب عند التسمية.
///
public class SectionServiceTests : PostgresDbTest
{
    private readonly ISectionService _service;
    private readonly ICommandHandler<CreateSectionCommand, SectionDto> _createSection;
    private readonly ICommandHandler<DeleteSectionCommand, bool> _deleteSection;
    private readonly ICommandHandler<RenameSectionCommand, SectionDto?> _renameSection;
    private readonly ICommandHandler<SetSectionActiveCommand, SectionDto?> _setSectionActive;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _damascusId;
    private readonly int _aleppoId;

    public SectionServiceTests()
    {
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
            new UserRepository(_db));
        _createSection = new CreateSectionCommandHandler(
            new Repository<Section>(_db),
            new Repository<Branch>(_db),
            uow,
            _audit,
            new DbExceptionClassifier(),
            new IPipelineBehavior<CreateSectionCommand, SectionDto>[]
            {
                new TransactionBehavior<CreateSectionCommand, SectionDto>(tx),
            });
        _deleteSection = new DeleteSectionCommandHandler(
            new Repository<Section>(_db),
            new Repository<ExecutionCircuit>(_db),
            new UserRepository(_db),
            uow,
            _audit,
            new IPipelineBehavior<DeleteSectionCommand, bool>[]
            {
                new TransactionBehavior<DeleteSectionCommand, bool>(tx),
            });
        _renameSection = new RenameSectionCommandHandler(
            new Repository<Section>(_db),
            new Repository<Branch>(_db),
            new Repository<ExecutionCircuit>(_db),
            new UserRepository(_db),
            new Repository<HeadSuccession>(_db),
            uow,
            _audit,
            new DbExceptionClassifier(),
            new IPipelineBehavior<RenameSectionCommand, SectionDto?>[]
            {
                new TransactionBehavior<RenameSectionCommand, SectionDto?>(tx),
            });
        _setSectionActive = new SetSectionActiveCommandHandler(
            new Repository<Section>(_db),
            new Repository<Branch>(_db),
            new Repository<ExecutionCircuit>(_db),
            new UserRepository(_db),
            uow,
            _audit,
            new IPipelineBehavior<SetSectionActiveCommand, SectionDto?>[]
            {
                new TransactionBehavior<SetSectionActiveCommand, SectionDto?>(tx),
            });
    }

    private Task<SectionDto> CreateSectionAsync(int branchId, string? name, string? actorName)
        => _createSection.HandleAsync(new CreateSectionCommand(branchId, name, actorName));

    private Task<bool> DeleteSectionAsync(int sectionId, string? actorName)
        => _deleteSection.HandleAsync(new DeleteSectionCommand(sectionId, actorName));

    private Task<SectionDto?> RenameSectionAsync(
        int sectionId, string? name, string? actorName, long? version = null)
        => _renameSection.HandleAsync(new RenameSectionCommand(sectionId, name, actorName, version));

    private Task<SectionDto?> SetActiveAsync(
        int sectionId, bool isActive, string? actorName, long? version = null)
        => _setSectionActive.HandleAsync(
            new SetSectionActiveCommand(sectionId, isActive, actorName, version));

    [PostgresFact]
    public async Task ListSections_CacheInvalidatedAfterCreateAndRename()
    {
        var cache = new DocGenerator.Infrastructure.Caching.ReferenceDataCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCache(
                new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
        var cachedService = new SectionService(
            new Repository<Section>(_db),
            new Repository<Branch>(_db),
            new Repository<ExecutionCircuit>(_db),
            new UserRepository(_db),
            cache);

        var cachedCreate = new CreateSectionCommandHandler(
            new Repository<Section>(_db),
            new Repository<Branch>(_db),
            new UnitOfWork(_db),
            _audit,
            new DbExceptionClassifier(),
            Array.Empty<IPipelineBehavior<CreateSectionCommand, SectionDto>>(),
            cache);
        var cachedRename = new RenameSectionCommandHandler(
            new Repository<Section>(_db),
            new Repository<Branch>(_db),
            new Repository<ExecutionCircuit>(_db),
            new UserRepository(_db),
            new Repository<HeadSuccession>(_db),
            new UnitOfWork(_db),
            _audit,
            new DbExceptionClassifier(),
            Array.Empty<IPipelineBehavior<RenameSectionCommand, SectionDto?>>(),
            cache);

        _ = await cachedService.ListSectionsAsync(_damascusId);
        _ = await cachedService.ListSectionsAsync(_damascusId);

        var created = await cachedCreate.HandleAsync(
            new CreateSectionCommand(_damascusId, "شعبة الكاش", "admin"));
        var afterCreate = await cachedService.ListSectionsAsync(_damascusId);
        Assert.Contains(afterCreate, s => s.Id == created.Id);

        var renamed = await cachedRename.HandleAsync(
            new RenameSectionCommand(created.Id, "شعبة الكاش 2", "admin", created.Version));
        Assert.NotNull(renamed);
        var afterRename = await cachedService.ListSectionsAsync(_damascusId);
        Assert.Contains(afterRename, s => s.Name == "شعبة الكاش 2");
    }


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

    [PostgresFact]
    public async Task Create_AddsActiveSection_WithZeroCounts()
    {
        var section = await CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");

        Assert.True(section.Id > 0);
        Assert.Equal("شعبة مصياف", section.Name);
        Assert.True(section.IsActive);
        Assert.Equal(0, section.CircuitCount);
        Assert.Null(section.HeadName);
        Assert.Contains("create_section", _audit.Actions);
        Assert.Contains(_audit.Details, d => d == "أنشأ شعبة «شعبة مصياف» في فرع دمشق");
    }

    [PostgresTheory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Create_BlankName_Throws(string? name)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateSectionAsync(_damascusId, name, "admin"));
    }

    [PostgresFact]
    public async Task Create_TooLongName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateSectionAsync(_damascusId, new string('س', 201), "admin"));
    }

    [PostgresFact]
    public async Task Create_UnknownBranch_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateSectionAsync(999999, "شعبة يتيمة", "admin"));
    }

    [PostgresFact]
    public async Task Create_DuplicateNormalizedNameSameBranch_Throws()
    {
        await CreateSectionAsync(_damascusId, "شعبة أحمد", "admin");

        // الهمزة المختلفة تُطبَّع للاسم نفسه (PB-001) — تكرار مرفوض برسالة عربية.
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateSectionAsync(_damascusId, "شعبة احمد", "admin"));
        Assert.Contains("موجودة مسبقًا", ex.Message);
    }

    [PostgresFact]
    public async Task Create_SameNameDifferentBranch_Allowed()
    {
        await CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");
        var other = await CreateSectionAsync(_aleppoId, "شعبة مصياف", "admin");

        Assert.Equal(_aleppoId, other.BranchId);
    }

    [PostgresFact]
    public async Task List_ShowsCircuitCountAndHeadName()
    {
        var section = await CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");
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

    [PostgresFact]
    public async Task List_UnknownBranch_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ListSectionsAsync(999999));
    }

    [PostgresFact]
    public async Task GetSection_Missing_ReturnsNull()
    {
        Assert.Null(await _service.GetSectionAsync(999999));
    }

    [PostgresFact]
    public async Task Rename_UpdatesName_WritesSuccessionWhenHeaded()
    {
        var section = await CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");
        var head = await AddHeadAsync("sub_masyaf", _damascusId, section.Id);

        var renamed = await RenameSectionAsync(section.Id, "شعبة السلمية", "admin");

        Assert.NotNull(renamed);
        Assert.Equal("شعبة السلمية", renamed.Name);
        Assert.Contains("rename_section", _audit.Actions);
        var succession = Assert.Single(_db.HeadSuccessions.ToList());
        Assert.Equal(HeadSuccessionEventCatalog.Renamed, succession.Event);
        Assert.Equal(head.Id, succession.UserId);
        Assert.Equal(section.Id, succession.SectionId);
    }

    [PostgresFact]
    public async Task Rename_SameName_NoOpWithoutAudit()
    {
        var section = await CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");
        var auditCount = _audit.Actions.Count;

        var same = await RenameSectionAsync(section.Id, "شعبة مصياف", "admin");

        Assert.NotNull(same);
        Assert.Equal(auditCount, _audit.Actions.Count);
        Assert.Empty(_db.HeadSuccessions.ToList());
    }

    [PostgresFact]
    public async Task Rename_HeadlessSection_NoSuccessionRow()
    {
        var section = await CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");

        await RenameSectionAsync(section.Id, "شعبة السلمية", "admin");

        Assert.Contains("rename_section", _audit.Actions);
        Assert.Empty(_db.HeadSuccessions.ToList());
    }

    [PostgresFact]
    public async Task Rename_Duplicate_Throws()
    {
        await CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");
        var other = await CreateSectionAsync(_damascusId, "شعبة السلمية", "admin");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            RenameSectionAsync(other.Id, "شعبة مصياف", "admin"));
    }

    [PostgresFact]
    public async Task Rename_Missing_ReturnsNull()
    {
        Assert.Null(await RenameSectionAsync(999999, "اسم", "admin"));
    }

    [PostgresFact]
    public async Task Rename_StaleVersion_ThrowsConflict_WithoutPersisting()
    {
        var section = await CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");

        var ex = await Assert.ThrowsAsync<DocumentConflictException>(() =>
            RenameSectionAsync(section.Id, "شعبة السلمية", "admin", section.Version + 7));

        Assert.Contains("الشعبة", ex.Message);
        Assert.Equal("شعبة مصياف", (await _db.Sections.AsNoTracking().SingleAsync(s => s.Id == section.Id)).Name);
        Assert.DoesNotContain("rename_section", _audit.Actions);
    }

    [PostgresFact]
    public async Task Rename_MatchingVersion_SucceedsAndIncrementsVersion()
    {
        var section = await CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");
        Assert.Equal(0, section.Version);

        var renamed = await RenameSectionAsync(section.Id, "شعبة السلمية", "admin", section.Version);

        Assert.NotNull(renamed);
        Assert.Equal(1, renamed.Version);
        Assert.Contains(_audit.Details,
            d => d == "أعاد تسمية الشعبة من «شعبة مصياف» إلى «شعبة السلمية»");
    }

    [PostgresFact]
    public async Task SetActive_StaleVersion_ThrowsConflict()
    {
        var section = await CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");

        await Assert.ThrowsAsync<DocumentConflictException>(() =>
            SetActiveAsync(section.Id, false, "admin", section.Version + 1));

        Assert.True((await _db.Sections.AsNoTracking().SingleAsync(s => s.Id == section.Id)).IsActive);
    }

    [PostgresFact]
    public async Task SetActive_DeactivateWithCircuits_Throws()
    {
        var section = await CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");
        var manager = await AddManagerAsync();
        await AddCircuitAsync("دائرة مصياف", _damascusId, section.Id, manager.Id);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            SetActiveAsync(section.Id, false, "admin"));
        Assert.Contains("انقل دوائرها", ex.Message);
    }

    [PostgresFact]
    public async Task SetActive_DeactivateEmpty_Succeeds()
    {
        var section = await CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");

        var off = await SetActiveAsync(section.Id, false, "admin");

        Assert.NotNull(off);
        Assert.False(off.IsActive);
        Assert.Contains("deactivate_section", _audit.Actions);
        Assert.Contains(_audit.Details,
            d => d == "عطّل الشعبة «شعبة مصياف» — التعطيل إخفاء من القوائم فقط ولا يوقف عمل رئيسها");

        var on = await SetActiveAsync(section.Id, true, "admin");
        Assert.NotNull(on);
        Assert.True(on.IsActive);
    }

    [PostgresFact]
    public async Task SetActive_Missing_ReturnsNull()
    {
        Assert.Null(await SetActiveAsync(999999, false, "admin"));
    }

    [PostgresFact]
    public async Task Delete_WithCircuits_Throws()
    {
        var section = await CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");
        var manager = await AddManagerAsync();
        await AddCircuitAsync("دائرة مصياف", _damascusId, section.Id, manager.Id);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DeleteSectionAsync(section.Id, "admin"));
        Assert.Contains("انقل دوائرها", ex.Message);
    }

    [PostgresFact]
    public async Task Delete_WithUsers_Throws()
    {
        var section = await CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");
        await AddHeadAsync("sub_masyaf", _damascusId, section.Id);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            DeleteSectionAsync(section.Id, "admin"));
        Assert.Contains("حساباتها", ex.Message);
    }

    [PostgresFact]
    public async Task Delete_Empty_Succeeds()
    {
        var section = await CreateSectionAsync(_damascusId, "شعبة مصياف", "admin");

        Assert.True(await DeleteSectionAsync(section.Id, "admin"));
        Assert.Null(await _db.Sections.FindAsync(section.Id));
        Assert.Contains("delete_section", _audit.Actions);
        Assert.Contains(_audit.Details, d => d == "حذف الشعبة «شعبة مصياف»");
    }

    [PostgresFact]
    public async Task Delete_Missing_ReturnsFalse()
    {
        Assert.False(await DeleteSectionAsync(999999, "admin"));
    }
}
