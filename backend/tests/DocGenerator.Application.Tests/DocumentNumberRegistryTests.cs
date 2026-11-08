using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.Tests.TestHelpers;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using DocGenerator.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

public class DocumentNumberRegistryTests : IDisposable
{
    private readonly DocGeneratorDbContext _db = TestDb.Create();
    private readonly FakeAuditLogger _audit = new();
    private readonly DocumentNumberRegistry _registry;
    private readonly DocumentRepository _documents;
    private readonly int _userId;
    private readonly int _branchId;
    private readonly int _circuitA;
    private readonly int _circuitB;
    private readonly int _currentYear;

    public DocumentNumberRegistryTests()
    {
        var branch = new Branch { Name = "دمشق", Code = "DAM" };
        _db.Branches.Add(branch);
        var user = new User
        {
            Username = "lawyer",
            FullName = "محامي",
            Role = UserRole.Lawyer,
            BranchId = 1,
            PasswordHash = "seed",
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        _branchId = branch.Id;
        _userId = user.Id;

        var circuits = new Repository<ExecutionCircuit>(_db);
        var circuitA = new ExecutionCircuit
        {
            Name = "دائرة أ", NameNorm = "دائره ا", BranchId = _branchId,
            IsActive = true, CreatedById = _userId,
        };
        var circuitB = new ExecutionCircuit
        {
            Name = "دائرة ب", NameNorm = "دائره ب", BranchId = _branchId,
            IsActive = true, CreatedById = _userId,
        };
        _db.ExecutionCircuits.AddRange(circuitA, circuitB);
        _db.SaveChanges();
        _circuitA = circuitA.Id;
        _circuitB = circuitB.Id;

        var tx = new TransactionRunner(_db);
        _documents = new DocumentRepository(_db);
        _registry = new DocumentNumberRegistry(
            _documents,
            new DocumentBaseNumberRepository(_db),
            circuits,
            new UnitOfWork(_db),
            tx,
            tx,
            _audit,
            new NumberWarningTokenService(),
            TimeProvider.System,
            TestClock.TimeZone);
        _currentYear = ServerClock.CurrentYear(TimeProvider.System, TestClock.TimeZone);
    }

    public void Dispose() => _db.Dispose();

    private Document AddDocument(
        int circuitId, string number, string year, string type = "صلح",
        bool deleted = false)
    {
        var doc = new Document
        {
            CreatedById = _userId,
            BranchId = _branchId,
            ExecutionCircuitId = circuitId,
            Court = circuitId == _circuitA ? "دائرة أ" : "دائرة ب",
            CourtNorm = circuitId == _circuitA ? "دائره ا" : "دائره ب",
            FileNumber = number,
            FileYear = year,
            FileType = type,
            IsDraft = false,
            NeedsRegistration = false,
            IsDeleted = deleted,
            Lawyer = "محامي",
        };
        _db.Documents.Add(doc);
        _db.SaveChanges();
        return doc;
    }

    private Task Sync(Document doc) =>
        _registry.SyncRegistrationAsync(doc, _userId, "lawyer", null, default);

    [Fact]
    public async Task SyncRegistration_CreatesActiveRegistrationRow_WithCircuitAndKeys()
    {
        var doc = AddDocument(_circuitA, " ١٢٣ ", _currentYear.ToString());
        await Sync(doc);

        var row = await _db.BaseNumbers.AsNoTracking().SingleAsync(b => b.DocumentId == doc.Id);
        Assert.Equal("123", row.BaseNumber);
        Assert.Equal("123", row.NumberKey);
        Assert.Equal("صلح", row.TypeKey);
        Assert.Equal(_circuitA, row.ExecutionCircuitId);
        Assert.Equal(_currentYear, row.Year);
        Assert.Equal(NumberSources.Registration, row.Source);
        Assert.Null(row.VoidedAt);
    }

    [Fact]
    public async Task SyncRegistration_YearChange_VoidsOldAndInsertsNew_RowRemainsInHistory()
    {
        var doc = AddDocument(_circuitA, "500", "2024");
        await Sync(doc);
        _ = await _db.BaseNumbers.AsNoTracking().SingleAsync(b => b.DocumentId == doc.Id);

        doc.FileYear = "2025";
        await Sync(doc);

        var rows = await _db.BaseNumbers.AsNoTracking()
            .Where(b => b.DocumentId == doc.Id).OrderBy(b => b.Year).ToListAsync();
        Assert.Equal(2, rows.Count);
        var old = rows.Single(r => r.Year == 2024);
        Assert.NotNull(old.VoidedAt);
        Assert.Equal(NumberVoidReasons.RegistrationSuperseded, old.VoidReason);
        var current = rows.Single(r => r.Year == 2025);
        Assert.Null(current.VoidedAt);
        Assert.Equal(NumberSources.Registration, current.Source);
    }

    [Fact]
    public async Task SyncRegistration_ClearingNumber_VoidsRegistration_AndKeepsRow()
    {
        var doc = AddDocument(_circuitA, "501", "2025");
        await Sync(doc);
        doc.FileNumber = "   ";
        await Sync(doc);

        var row = await _db.BaseNumbers.AsNoTracking().SingleAsync(b => b.DocumentId == doc.Id);
        Assert.NotNull(row.VoidedAt);
        Assert.Equal(NumberVoidReasons.RegistrationCleared, row.VoidReason);
    }

    [Fact]
    public async Task SaveRotation_InsertsRotationInCurrentYear_AndEffectiveUsesIt()
    {
        var doc = AddDocument(_circuitA, "600", (_currentYear - 1).ToString());
        await Sync(doc);
        var before = await _documents.GetByIdAsync(doc.Id) ?? throw new InvalidOperationException();
        Assert.True(DocumentBaseNumberPolicy.IsEligible(before, _currentYear));

        await _registry.SaveRotationsAsync(
            _userId, "lawyer", new List<BaseNumberEntry> { new(doc.Id, "900") }, null, default);

        var row = await _db.BaseNumbers.AsNoTracking().SingleAsync(b =>
            b.DocumentId == doc.Id && b.Year == _currentYear && b.VoidedAt == null);
        Assert.Equal(NumberSources.Rotation, row.Source);
        var loaded = await _documents.GetByIdAsync(doc.Id) ?? throw new InvalidOperationException();
        Assert.Equal("900", EffectiveFileIdentity.Number(loaded, _currentYear));
    }

    [Fact]
    public async Task SaveRotation_HardDuplicate_SameCircuit_IsRejectedWithoutOverride()
    {
        var existing = AddDocument(_circuitA, "700", _currentYear.ToString());
        await Sync(existing);
        var target = AddDocument(_circuitA, "701", (_currentYear - 1).ToString());
        await Sync(target);

        var ex = await Assert.ThrowsAsync<DocumentConflictException>(() =>
            _registry.SaveRotationsAsync(
                _userId, "lawyer", new List<BaseNumberEntry> { new(target.Id, "700") }, null, default));
        Assert.Equal("number_duplicate", ex.Code);
    }

    [Fact]
    public async Task SaveRotation_SoftDuplicate_OtherCircuit_WarnsThenAcceptsWithToken()
    {
        var existing = AddDocument(_circuitA, "710", _currentYear.ToString());
        await Sync(existing);
        var target = AddDocument(_circuitB, "711", (_currentYear - 1).ToString());
        await Sync(target);

        var warning = await Assert.ThrowsAsync<NumberReuseWarningException>(() =>
            _registry.SaveRotationsAsync(
                _userId, "lawyer", new List<BaseNumberEntry> { new(target.Id, "710") }, null, default));

        await _registry.SaveRotationsAsync(
            _userId, "lawyer", new List<BaseNumberEntry> { new(target.Id, "710") },
            warning.WarningToken, default);

        Assert.True(await _db.BaseNumbers.AnyAsync(b =>
            b.DocumentId == target.Id && b.Year == _currentYear
            && b.BaseNumber == "710" && b.VoidedAt == null));
    }

    [Fact]
    public async Task Correct_UpdatesRowInPlace_AndAuditsBeforeAndAfter()
    {
        var doc = AddDocument(_circuitA, "800", _currentYear.ToString());
        await Sync(doc);
        var row = await _db.BaseNumbers.AsNoTracking().SingleAsync(b => b.DocumentId == doc.Id);

        await _registry.CorrectAsync(
            doc.Id, _currentYear, row.Id, "801", _userId, "lawyer", null, default);

        var updated = await _db.BaseNumbers.AsNoTracking().SingleAsync(b => b.Id == row.Id);
        Assert.Equal("801", updated.BaseNumber);
        Assert.Equal("801", updated.NumberKey);
        Assert.Equal(row.Id, updated.Id);
        var audit = Assert.Single(_audit.Details.Where(d => d?.Contains("صحّح رقم أساس") == true));
        Assert.Contains("قبل «800»", audit);
        Assert.Contains("بعد «801»", audit);
    }

    [Fact]
    public async Task UndoRotation_CurrentYear_VoidsRotationAndKeepsRow()
    {
        var doc = AddDocument(_circuitA, "810", (_currentYear - 1).ToString());
        await Sync(doc);
        await _registry.SaveRotationsAsync(
            _userId, "lawyer", new List<BaseNumberEntry> { new(doc.Id, "910") }, null, default);

        await _registry.UndoRotationAsync(doc.Id, _currentYear, _userId, "lawyer", default);

        var row = await _db.BaseNumbers.AsNoTracking().SingleAsync(b =>
            b.DocumentId == doc.Id && b.Source == NumberSources.Rotation);
        Assert.NotNull(row.VoidedAt);
        Assert.Equal(NumberVoidReasons.UndoRotation, row.VoidReason);
        Assert.Equal("910", row.BaseNumber);
    }

    [Fact]
    public async Task VoidForDocumentDelete_VoidsAllRows_DocumentDeletedHistoryKept()
    {
        var doc = AddDocument(_circuitA, "820", "2025");
        await Sync(doc);
        doc.FileYear = "2024";
        await Sync(doc);

        await _registry.VoidForDocumentDeleteAsync(doc, _userId, "lawyer", default);

        var rows = await _db.BaseNumbers.AsNoTracking().Where(b => b.DocumentId == doc.Id).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.NotNull(r.VoidedAt);
            Assert.Equal(NumberVoidReasons.DocumentDeleted, r.VoidReason);
        });
    }

    [Fact]
    public async Task History_IncludesVoidedRows_AndMarksEffective()
    {
        var doc = AddDocument(_circuitA, "830", "2024");
        await Sync(doc);
        doc.FileYear = "2025";
        await Sync(doc);

        var history = await _registry.GetHistoryAsync(doc.Id, includeDeletedDocument: false, default);

        Assert.Equal(2, history.Count);
        Assert.Equal(1, history.Count(h => h.IsVoided));
        Assert.Equal(1, history.Count(h => h.IsEffective));
        Assert.Contains(history, h => h.VoidReason == NumberVoidReasons.RegistrationSuperseded);
    }
}
