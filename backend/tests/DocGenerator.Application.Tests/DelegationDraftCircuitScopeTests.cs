using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>
/// نطاق الدائرة المنابة عند تسطير المسودة (S2): معرف دائرة من محافظة أخرى
/// يُرفض مبكرًا بدل كشف اسمها ثم الفشل المتأخر عند الاعتماد.
/// </summary>
public class DelegationDraftCircuitScopeTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IDocumentDelegationService _service;
    private readonly FakeAuditLogger _audit = new();

    private readonly Branch _branchA;
    private readonly Branch _branchB;
    private readonly User _lawyer;

    public DelegationDraftCircuitScopeTests()
    {
        _db = TestDb.Create();

        _branchA = new Branch { Name = "فرع دمشق", Code = "DAM", Governorate = "دمشق" };
        _branchB = new Branch { Name = "فرع اللاذقية", Code = "LAT", Governorate = "اللاذقية" };
        _db.Branches.AddRange(_branchA, _branchB);
        _db.SaveChanges();

        _lawyer = new User
        {
            Username = "scope_lawyer",
            FullName = "محامي النطاق",
            Role = UserRole.Lawyer,
            BranchId = _branchA.Id,
            PasswordHash = new Services.PasswordHasher().Hash("123456"),
        };
        _db.Users.Add(_lawyer);
        _db.SaveChanges();

        var documents = new DocumentRepository(_db);
        var users = new UserRepository(_db);
        var branches = new Repository<Branch>(_db);
        var registrationDates = new Repository<DocumentRegistrationDate>(_db);
        var occurrences = new Repository<DocumentOccurrence>(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        var headAlerts = new HeadAlertService(
            new HeadAlertRepository(_db),
            documents,
            users,
            branches,
            uow,
            tx,
            _audit);

        _service = new DocumentDelegationService(
            new DelegationRepository(_db),
            new DelegationReservationRepository(_db),
            new DbExceptionClassifier(),
            documents,
            users,
            branches,
            registrationDates,
            occurrences,
            uow,
            tx,
            _audit,
            headAlerts,
            TimeProvider.System,
            TestClock.TimeZone,
            circuits: new Repository<ExecutionCircuit>(_db));
    }

    public void Dispose() => _db.Dispose();

    private static int s_sourceSeq;

    private async Task<(Document Doc, int AssetId)> CreateSourceAsync()
    {
        var doc = new Document
        {
            CreatedById = _lawyer.Id,
            BranchId = _branchA.Id,
            BranchName = _branchA.Name,
            GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
            IsDraft = false,
            BorrowerName = "أحمد",
            BorrowerFather = "خالد",
            BorrowerFamily = "الخطيب",
            AmountNumeric = 1_000_000,
            Currency = "ليرة سورية",
            ContractType = "عقد قرض",
            ContractNumber = "12/2024",
            Court = "دمشق",
            Applicant = "المدعي",
            FileNumber = $"530{System.Threading.Interlocked.Increment(ref s_sourceSeq):D4}",
            FileYear = "2024",
            DocumentType = "متداول - أحمد خالد الخطيب",
            SearchText = "أحمد الخطيب المدعي",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        var asset = new Asset
        {
            DocumentId = doc.Id,
            AssetKind = AssetKindCatalog.RealEstate,
            PropertyNumber = "77",
            PropertyDistrict = "المزة",
            SeizureDate = new DateTime(2026, 8, 1),
        };
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();
        return (doc, asset.Id);
    }

    private int CreateCircuit(string name, int branchId)
    {
        var circuit = new ExecutionCircuit
        {
            BranchId = branchId,
            CreatedById = _lawyer.Id,
            Name = name,
            NameNorm = ArabicNameNormalizer.Normalize(name),
            IsActive = true,
        };
        _db.ExecutionCircuits.Add(circuit);
        _db.SaveChanges();
        return circuit.Id;
    }

    private static UpsertDelegationRequest DraftRequest(int assetId, int? circuitId) =>
        new UpsertDelegationRequest(
            "دائرة تنفيذ حلب",
            false,
            null,
            "1/8/2026",
            "الإنابة على العقار المذكور",
            "كتاب-1",
            "2/8/2026",
            new List<int> { assetId },
            circuitId);

    [Fact]
    public async Task Draft_WithForeignGovernorateCircuit_RejectedEarly()
    {
        var (doc, assetId) = await CreateSourceAsync();
        var foreignId = CreateCircuit("دائرة اللاذقية الأولى", _branchB.Id);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(doc.Id, DraftRequest(assetId, foreignId), _lawyer.Id, "المحامي"));
        Assert.Contains("محافظة", ex.Message);
    }

    [Fact]
    public async Task Draft_WithSameGovernorateCircuit_Accepted()
    {
        var (doc, assetId) = await CreateSourceAsync();
        var sameId = CreateCircuit("دائرة دمشق الأولى", _branchA.Id);

        var dto = await _service.CreateAsync(doc.Id, DraftRequest(assetId, sameId), _lawyer.Id, "المحامي");

        Assert.Equal("دائرة دمشق الأولى", dto.DelegatedCourt);
        Assert.Equal(sameId, _db.DocumentDelegations.Single(d => d.Id == dto.Id).DelegatedCircuitId);
    }

    [Fact]
    public async Task Draft_WithUnknownGovernorates_LenientAsApprovePath()
    {
        // بلا محافظات مسجلة: التساهل نفسه المطبق في مسار الاعتماد (لا رفض مبكر).
        _branchA.Governorate = null;
        _branchB.Governorate = null;
        await _db.SaveChangesAsync();
        var (doc, assetId) = await CreateSourceAsync();
        var foreignId = CreateCircuit("دائرة اللاذقية الأولى", _branchB.Id);

        var dto = await _service.CreateAsync(doc.Id, DraftRequest(assetId, foreignId), _lawyer.Id, "المحامي");

        Assert.Equal(foreignId, _db.DocumentDelegations.Single(d => d.Id == dto.Id).DelegatedCircuitId);
    }
}
