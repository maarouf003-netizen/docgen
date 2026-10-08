using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>
/// نطاق الإحصاءات (§12/قرار 27 + قاعدة 20): القسم لإحصاء قسمه (دوائر القسم
/// وبلا دائرة)، والشعبة لشعبتها، والإدارة للفرع ككل — كل ملف لمالك واحد.
/// قاعدة كل اختبار جديدة (TestDb) — بلا تلوث متبادل.
/// </summary>
public class SubHeadStatsTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IStatisticsRepository _stats;
    private readonly IExecutionCircuitService _circuits;
    private readonly IHeadAlertService _alerts;
    private readonly FakeAuditLogger _audit = new();

    private readonly Branch _branch;
    private readonly User _lawyer1;
    private readonly User _lawyer2;
    private readonly User _head1;

    public SubHeadStatsTests()
    {
        _db = TestDb.Create();

        _branch = new Branch { Name = "دمشق", Code = "DAM", Governorate = "دمشق" };
        _db.Branches.Add(_branch);
        _db.SaveChanges();

        _lawyer1 = User(_branch.Id, "lawyer1", "محامي دمشق");
        _lawyer2 = User(_branch.Id, "lawyer2", "محامي دمشق ثانٍ");
        _head1 = User(_branch.Id, "head1", "رئيس قسم دمشق", UserRole.Head);
        _db.Users.AddRange(_lawyer1, _lawyer2, _head1);
        _db.SaveChanges();

        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        _stats = new StatisticsRepository(_db, TimeProvider.System, TestClock.TimeZone);
        _circuits = new ExecutionCircuitService(
            new Repository<ExecutionCircuit>(_db),
            new Repository<Branch>(_db),
            new Repository<Section>(_db),
            new DocumentRepository(_db),
            new DelegationRepository(_db),
            new AppealRepository(_db),
            new Repository<DocumentOccurrence>(_db),
            new Repository<DocumentBaseNumber>(_db),
            new Repository<HeadSuccession>(_db),
            new HeadAlertRepository(_db),
            new UserRepository(_db),
            uow,
            tx,
            _audit,
            new DbExceptionClassifier(),
            TimeProvider.System,
            TestClock.TimeZone);
        _alerts = new HeadAlertService(
            new HeadAlertRepository(_db),
            new DocumentRepository(_db),
            new UserRepository(_db),
            new Repository<Branch>(_db),
            uow,
            tx,
            _audit);
    }

    public void Dispose() => _db.Dispose();

    private static User User(int? branchId, string username, string fullName, UserRole role = UserRole.Lawyer) => new()
    {
        Username = username,
        FullName = fullName,
        Role = role,
        BranchId = branchId,
        IsActive = true,
        PasswordHash = new Services.PasswordHasher().Hash("123456"),
    };

    private static int s_seq;

    private async Task<int> AddSectionAsync(string name)
    {
        var section = new Section
        {
            Name = name,
            NameNorm = ArabicNameNormalizer.Normalize(name),
            BranchId = _branch.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Sections.Add(section);
        await _db.SaveChangesAsync();
        return section.Id;
    }

    private async Task<int> AddCircuitAsync(string name, int? sectionId)
    {
        var circuit = new ExecutionCircuit
        {
            BranchId = _branch.Id,
            SectionId = sectionId,
            Name = name,
            NameNorm = ArabicNameNormalizer.Normalize(name),
            IsActive = true,
            CreatedById = _head1.Id,
        };
        _db.ExecutionCircuits.Add(circuit);
        await _db.SaveChangesAsync();
        return circuit.Id;
    }

    private async Task<Document> AddDocumentAsync(int ownerId, int? circuitId, string? fileNumber = null)
    {
        var number = fileNumber ?? $"580{System.Threading.Interlocked.Increment(ref s_seq):D4}";
        var doc = new Document
        {
            BranchId = _branch.Id,
            CreatedById = ownerId,
            IsDraft = false,
            BorrowerName = "أحمد",
            BorrowerFather = "خالد",
            BorrowerFamily = "الخطيب",
            FileNumber = $"{number}/2026",
            FileType = "تنفيذي",
            FileYear = "2026",
            Court = "دائرة تنفيذ دمشق",
            ExecutionCircuitId = circuitId,
            AmountNumeric = 0,
            ExecStatus = string.Empty,
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        return doc;
    }

    [Fact]
    public async Task ManagerStats_HeadSeesDivisionOnly_SubSeesOwnOnly()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف");
        var divisionCircuit = await AddCircuitAsync("دائرة القسم", null);
        var sectionCircuit = await AddCircuitAsync("دائرة الشعبة", sectionId);
        await AddDocumentAsync(_lawyer1.Id, divisionCircuit);
        await AddDocumentAsync(_lawyer1.Id, sectionCircuit);
        await AddDocumentAsync(_lawyer2.Id, null);

        // الإدارة: الفرع ككل (3) — بلا تضييق.
        var full = await _stats.GetManagerStatsAsync(StatsPeriod.Yearly, _branch.Id);
        Assert.Equal(3, full.TotalFiles);

        // القسم: دائرته + بلا دائرة (2) — تضييق مقصود (قرار 27).
        var division = await _stats.GetManagerStatsAsync(
            StatsPeriod.Yearly, _branch.Id, ct: default, ownerSectionId: null, fullAccess: false);
        Assert.Equal(2, division.TotalFiles);

        // الشعبة: دائرتها فقط (1).
        var section = await _stats.GetManagerStatsAsync(
            StatsPeriod.Yearly, _branch.Id, ct: default, ownerSectionId: sectionId, fullAccess: false);
        Assert.Equal(1, section.TotalFiles);
    }

    [Fact]
    public async Task ManagerStats_NullCircuitFile_CountsInDivision()
    {
        await AddDocumentAsync(_lawyer1.Id, null);

        var division = await _stats.GetManagerStatsAsync(
            StatsPeriod.Yearly, _branch.Id, ct: default, ownerSectionId: null, fullAccess: false);
        Assert.Equal(1, division.TotalFiles);
    }

    [Fact]
    public async Task ManagerLawyerStats_CountsWithinOwnerScope()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف");
        var divisionCircuit = await AddCircuitAsync("دائرة القسم", null);
        var sectionCircuit = await AddCircuitAsync("دائرة الشعبة", sectionId);
        // محامٍ بملفين في نطاقين: يُحتسب حيث دائرته فقط.
        await AddDocumentAsync(_lawyer1.Id, divisionCircuit);
        await AddDocumentAsync(_lawyer1.Id, sectionCircuit);

        var division = await _stats.GetManagerLawyerStatsAsync(
            StatsPeriod.Yearly, _branch.Id, ct: default, ownerSectionId: null, fullAccess: false);
        // القائمة بأسماء محامي الفرع (تسريب مقبول §17/1)، والعدّ ضمن النطاق فقط.
        Assert.Equal(1, Assert.Single(division, l => l.LawyerId == _lawyer1.Id).TotalCount);
        Assert.Equal(0, Assert.Single(division, l => l.LawyerId == _lawyer2.Id).TotalCount);

        var section = await _stats.GetManagerLawyerStatsAsync(
            StatsPeriod.Yearly, _branch.Id, ct: default, ownerSectionId: sectionId, fullAccess: false);
        Assert.Equal(1, Assert.Single(section, l => l.LawyerId == _lawyer1.Id).TotalCount);

        var full = await _stats.GetManagerLawyerStatsAsync(StatsPeriod.Yearly, _branch.Id);
        Assert.Equal(2, Assert.Single(full, l => l.LawyerId == _lawyer1.Id).TotalCount);
    }

    [Fact]
    public async Task AvailablePeriods_Scoped()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف");
        var sectionCircuit = await AddCircuitAsync("دائرة الشعبة", sectionId);
        await AddDocumentAsync(_lawyer1.Id, sectionCircuit);

        Assert.Empty(await _stats.GetAvailablePeriodsAsync(_branch.Id, null, default, null, false));
        Assert.NotEmpty(await _stats.GetAvailablePeriodsAsync(_branch.Id, null, default, sectionId, false));
    }
    [Fact]
    public async Task DashboardAndMonthly_HeadDivision_SubSection()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف");
        var divisionCircuit = await AddCircuitAsync("دائرة القسم", null);
        var sectionCircuit = await AddCircuitAsync("دائرة الشعبة", sectionId);
        await AddDocumentAsync(_lawyer1.Id, divisionCircuit);
        await AddDocumentAsync(_lawyer1.Id, sectionCircuit);

        var headDash = await _stats.GetDashboardStatsAsync(_branch.Id, default, null, false);
        Assert.Equal(1, headDash.TotalDocuments);
        var subDash = await _stats.GetDashboardStatsAsync(_branch.Id, default, sectionId, false);
        Assert.Equal(1, subDash.TotalDocuments);
        var fullDash = await _stats.GetDashboardStatsAsync(_branch.Id);
        Assert.Equal(2, fullDash.TotalDocuments);

        var headMonthly = await _stats.GetMonthlyStatsAsync(_branch.Id, default, null, false);
        Assert.Equal(1, headMonthly.Sum(m => m.Count));
        var subMonthly = await _stats.GetMonthlyStatsAsync(_branch.Id, default, sectionId, false);
        Assert.Equal(1, subMonthly.Sum(m => m.Count));
    }

    [Fact]
    public async Task CircuitStats_CarriesSectionColumns()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف");
        var divisionCircuit = await AddCircuitAsync("دائرة القسم", null);
        var sectionCircuit = await AddCircuitAsync("دائرة الشعبة", sectionId);
        await AddDocumentAsync(_lawyer1.Id, divisionCircuit);
        await AddDocumentAsync(_lawyer1.Id, sectionCircuit);

        var rows = await _circuits.CircuitStatsAsync(_branch.Id, null, true);

        var division = Assert.Single(rows, r => r.CircuitId == divisionCircuit);
        Assert.Null(division.SectionId);
        Assert.Null(division.SectionName);
        Assert.Equal(1, division.FileCount);
        var section = Assert.Single(rows, r => r.CircuitId == sectionCircuit);
        Assert.Equal(sectionId, section.SectionId);
        Assert.Equal("شعبة مصياف", section.SectionName);
        Assert.Equal(1, section.FileCount);
    }

    [Fact]
    public async Task Alerts_HeadSeesOwnOnly_SubSeesOwnOnly()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف");
        var sub = User(_branch.Id, "sub_masyaf", "رئيس الشعبة", UserRole.SubHead);
        sub.SectionId = sectionId;
        _db.Users.Add(sub);
        await _db.SaveChangesAsync();

        // تنبيه موجَّه لرئيس الشعبة وتنبيه لرئيس القسم.
        await _alerts.CreateAsync(new DocGenerator.Application.DTOs.CreateHeadAlertRequest(
            "head", null, null, "للشعبة", RecipientUserId: sub.Id), _head1.Id, _branch.Id, "head1");
        await _alerts.CreateAsync(new DocGenerator.Application.DTOs.CreateHeadAlertRequest(
            "head", null, null, "للقسم", RecipientUserId: _head1.Id), _head1.Id, _branch.Id, "head1");

        // قراءة بالمستلم (§8): كلٌّ يرى تنبيهه فقط.
        var subList = await _alerts.ListForHeadAsync(sub.Id, _branch.Id);
        Assert.Equal("للشعبة", Assert.Single(subList).Message);
        var headList = await _alerts.ListForHeadAsync(_head1.Id, _branch.Id);
        Assert.Equal("للقسم", Assert.Single(headList).Message);

        // تعليم القراءة يعمل للمستلم الرئيس.
        var own = Assert.Single(headList);
        Assert.False(own.IsRead);
        Assert.True(await _alerts.MarkReadAsync(own.Id, _head1.Id));
        Assert.True(Assert.Single(await _alerts.ListForHeadAsync(_head1.Id, _branch.Id)).IsRead);
    }
}
