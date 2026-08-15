using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات قائمة الجهات طالبة التنفيذ (ApplicantPublicEntity)، وحقلي ورود الملف،
/// وسجل التعاقب على الملف (منشئ + إحالات) في وضع «الجهة العامة طالبة تنفيذ».
/// </summary>
public class ApplicantAndAssignmentTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IDocumentService _service;
    private readonly FakeAuditLogger _audit = new();

    public ApplicantAndAssignmentTests()
    {
        _db = TestDb.Create();
        _db.Branches.Add(new Branch { Name = "دمشق", Code = "DAM" });
        _db.Users.Add(new User
        {
            Username = "lawyer1",
            FullName = "محامي أول",
            Role = UserRole.Lawyer,
            BranchId = 1,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        _db.Users.Add(new User
        {
            Username = "lawyer2",
            FullName = "محامي ثانٍ",
            Role = UserRole.Lawyer,
            BranchId = 1,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        _db.SaveChanges();
        var documents = new DocumentRepository(_db);
        var users = new UserRepository(_db);
        var guarantors = new Repository<Guarantor>(_db);
        var estates = new Repository<RealEstate>(_db);
        var actions = new Repository<ExecutionAction>(_db);
        var baseNumbers = new Repository<DocumentBaseNumber>(_db);
        var registrationDates = new Repository<DocumentRegistrationDate>(_db);
        var occurrences = new Repository<DocumentOccurrence>(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        _service = new DocumentService(documents, users, guarantors, estates, actions, baseNumbers, registrationDates, occurrences, uow, tx, _audit);
    }

    public void Dispose() => _db.Dispose();

    private static DocumentUpsertRequest ApplicantSample() => new()
    {
        BorrowerName = "أحمد",
        AmountNumeric = 1000,
        Currency = "ليرة سورية",
        FileNumber = "520",
        FileYear = "2024",
        FileRegistrationDate = "1/1/2024",
    };

    private static DocumentUpsertRequest ExecutedSample() => new()
    {
        GeneralEntitySide = GeneralEntitySideCatalog.Executed,
        FileNumber = "777",
        FileYear = "2024",
        ContractTypeSelector = "عادي",
        FileReceiptDate = "5/1/2024",
        ExecutedRequiredAmount = 1000m,
    };

    [Fact]
    public async Task Create_WithApplicantEntities_StoresListAndDerivesApplicantText()
    {
        var req = ApplicantSample();
        req.ApplicantPublicEntities = new()
        {
            new ApplicantPublicEntityDto(null, "المصرف التجاري السوري", "فرع 1", "دمشق"),
            new ApplicantPublicEntityDto(null, "مديرية زراعة اللاذقية", null, "اللاذقية"),
        };
        req.FileArrivalNumber = "ر-100";
        req.FileArrivalDate = "5/1/2024";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        Assert.Equal(2, loaded!.ApplicantPublicEntities.Count);
        Assert.Equal("المصرف التجاري السوري", loaded.ApplicantPublicEntities[0].Name);
        Assert.Equal("فرع 1", loaded.ApplicantPublicEntities[0].Branch);
        Assert.Equal("دمشق", loaded.ApplicantPublicEntities[0].Governorate);
        Assert.Equal("مديرية زراعة اللاذقية", loaded.ApplicantPublicEntities[1].Name);
        Assert.Equal("اللاذقية", loaded.ApplicantPublicEntities[1].Governorate);
        Assert.Equal("المصرف التجاري السوري - محافظة دمشق و مديرية زراعة اللاذقية - محافظة اللاذقية", loaded.Applicant);
        Assert.Equal("ر-100", loaded.FileArrivalNumber);
        Assert.Equal("5/1/2024", loaded.FileArrivalDate);
    }

    [Fact]
    public async Task Create_WithLegacyApplicantString_CreatesSingleEntity()
    {
        var req = ApplicantSample();
        req.Applicant = "المدعي القديم";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        var e = Assert.Single(loaded!.ApplicantPublicEntities);
        Assert.Equal("المدعي القديم", e.Name);
        Assert.Equal("المدعي القديم", loaded.Applicant);
    }

    [Fact]
    public async Task Create_ExecutedSide_ClearsApplicantListAndArrivalFields()
    {
        var req = ExecutedSample();
        req.ApplicantPublicEntities = new() { new ApplicantPublicEntityDto(null, "جهة زائدة", null) };
        req.FileArrivalNumber = "يجب أن يُصفَّر";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        Assert.Empty(loaded!.ApplicantPublicEntities);
        Assert.Null(loaded.FileArrivalNumber);
        Assert.Null(loaded.FileArrivalDate);
    }

    [Fact]
    public async Task CreateAsync_RecordsCreateAssignment()
    {
        var doc = await _service.CreateAsync(ApplicantSample(), 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        var create = Assert.Single(loaded!.Assignments);
        Assert.Equal(AssignmentKindCatalog.Create, create.Kind);
        Assert.Equal("محامي أول", create.LawyerName);
        Assert.Null(create.AssignedByName);
    }

    [Fact]
    public async Task TransferAsync_RecordsTransferAssignment()
    {
        var doc = await _service.CreateAsync(ApplicantSample(), 1, "lawyer1", 1);

        await _service.TransferAsync(doc.Id, 2, "lawyer1");

        var loaded = await _service.GetAsync(doc.Id);
        Assert.Equal(2, loaded!.Assignments.Count);
        var transfer = loaded.Assignments.Single(a => a.Kind == AssignmentKindCatalog.Transfer);
        Assert.Equal("محامي ثانٍ", transfer.LawyerName);
        Assert.Equal("lawyer1", transfer.AssignedByName);
        Assert.True(transfer.AssignedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task SearchText_IncludesApplicantEntityAndArrivalNumber()
    {
        var req = ApplicantSample();
        req.ApplicantPublicEntities = new()
        {
            new ApplicantPublicEntityDto(null, "شركة النور", "فرع 2"),
        };
        req.FileArrivalNumber = "ر-777";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var stored = await _db.Documents.SingleAsync(d => d.Id == doc.Id);

        Assert.Contains("شركة النور", stored.SearchText);
        Assert.Contains("ر-777", stored.SearchText);
    }
}
