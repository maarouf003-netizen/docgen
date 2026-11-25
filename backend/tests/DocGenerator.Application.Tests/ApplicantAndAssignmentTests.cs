using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Documents;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات قائمة الجهات طالبة التنفيذ (ApplicantPublicEntity)، وحقلي ورود الملف،
/// وسجل التعاقب على الملف (منشئ + إحالات) في وضع «الجهة العامة طالبة تنفيذ».
/// </summary>
public class ApplicantAndAssignmentTests : PostgresDbTest
{
    private readonly IQueryHandler<GetDocumentQuery, DocumentResponse?> _service;
    private readonly ICommandHandler<CreateDocumentCommand, DocumentResponse> _create;
    private readonly ICommandHandler<UpdateDocumentCommand, DocumentResponse?> _update;
    private readonly ICommandHandler<TransferDocumentCommand, DocumentResponse> _transfer;
    private readonly FakeAuditLogger _audit = new();

    public ApplicantAndAssignmentTests()
    {
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
        var actions = new Repository<ExecutionAction>(_db);
        var baseNumbers = new Repository<DocumentBaseNumber>(_db);
        var registrationDates = new Repository<DocumentRegistrationDate>(_db);
        var occurrences = new Repository<DocumentOccurrence>(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        var bundle = DocumentServiceFactory.Create(_db, _audit, documents: documents, users: users, actions: actions, baseNumbers: baseNumbers, registrationDates: registrationDates, occurrences: occurrences, uow: uow, tx: tx);
        _service = bundle.Reads.GetDocument;
        _create = bundle.CreateDocument;
        _update = bundle.UpdateDocument;
        _transfer = bundle.TransferDocument;
    }


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

    [PostgresFact]
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

        var doc = await _create.CreateAsync(req, 1, "lawyer1", 1);
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

    [PostgresFact]
    public async Task Create_WithManyLongApplicantEntities_TruncatesApplicantTextToColumnLimit()
    {
        var req = ApplicantSample();
        var longName = new string('ا', 100);
        req.ApplicantPublicEntities = new()
        {
            new ApplicantPublicEntityDto(null, longName, "فرع 1", "دمشق"),
            new ApplicantPublicEntityDto(null, longName, "فرع 2", "حلب"),
            new ApplicantPublicEntityDto(null, longName, "فرع 3", "حمص"),
        };

        var doc = await _create.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        Assert.NotNull(loaded);
        Assert.True(loaded!.Applicant!.Length <= ApplicantTextBuilder.MaxLength);
        Assert.EndsWith("…", loaded.Applicant);
    }

    [PostgresFact]
    public async Task Create_WithLegacyApplicantString_CreatesSingleEntity()
    {
        var req = ApplicantSample();
        req.Applicant = "المدعي القديم";

        var doc = await _create.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        var e = Assert.Single(loaded!.ApplicantPublicEntities);
        Assert.Equal("المدعي القديم", e.Name);
        Assert.Equal("المدعي القديم", loaded.Applicant);
    }

    [PostgresFact]
    public async Task Create_ExecutedSide_ClearsApplicantListAndArrivalFields()
    {
        var req = ExecutedSample();
        req.ApplicantPublicEntities = new() { new ApplicantPublicEntityDto(null, "جهة زائدة", null) };
        req.FileArrivalNumber = "يجب أن يُصفَّر";

        var doc = await _create.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        Assert.Empty(loaded!.ApplicantPublicEntities);
        Assert.Null(loaded.FileArrivalNumber);
        Assert.Null(loaded.FileArrivalDate);
    }

    [PostgresFact]
    public async Task Create_ExecutedSide_WithLegacyApplicantString_DoesNotCreatePhantomEntity()
    {
        // C-01: النص القديم `Applicant` توافقٌ لوضع «طالبة تنفيذ» فقط — لا يُحوَّل
        // إلى صف جهة عامة بلا RegistryId في الأوضاع الأخرى.
        var req = ExecutedSample();
        req.Applicant = "مدعي قديم";

        var doc = await _create.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        Assert.Empty(loaded!.ApplicantPublicEntities);
        Assert.True(string.IsNullOrEmpty(loaded.Applicant));
    }

    [PostgresFact]
    public async Task Update_StruckOffApplicant_PreservesStruckOffDate()
    {
        var created = await _create.CreateAsync(ApplicantSample(), 1, "lawyer1", 1);
        var stored = await _db.Documents.SingleAsync(d => d.Id == created.Id);
        stored.ExecStatus = ExecutionStatusCatalog.StateStruckOff;
        stored.StruckOffDate = new DateTime(2026, 1, 1);
        await _db.SaveChangesAsync();

        var update = ApplicantSample();
        update.BorrowerName = "اسم محدث";
        var updated = await _update.UpdateAsync(created.Id, update, "lawyer1");

        Assert.NotNull(updated);
        Assert.Equal(new DateTime(2026, 1, 1), updated!.StruckOffDate);
        Assert.Equal(ExecutionStatusCatalog.StateStruckOff, updated.ExecStatus);
    }

    [PostgresFact]
    public async Task CreateAsync_RecordsCreateAssignment()
    {
        var doc = await _create.CreateAsync(ApplicantSample(), 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        var create = Assert.Single(loaded!.Assignments);
        Assert.Equal(AssignmentKindCatalog.Create, create.Kind);
        Assert.Equal("محامي أول", create.LawyerName);
        Assert.Null(create.AssignedByName);
    }

    [PostgresFact]
    public async Task TransferAsync_RecordsTransferAssignment()
    {
        var doc = await _create.CreateAsync(ApplicantSample(), 1, "lawyer1", 1);

        await _transfer.TransferAsync(doc.Id, 2, "lawyer1");

        var loaded = await _service.GetAsync(doc.Id);
        Assert.Equal(2, loaded!.Assignments.Count);
        var transfer = loaded.Assignments.Single(a => a.Kind == AssignmentKindCatalog.Transfer);
        Assert.Equal("محامي ثانٍ", transfer.LawyerName);
        Assert.Equal("lawyer1", transfer.AssignedByName);
        Assert.True(transfer.AssignedAt <= DateTime.UtcNow);
    }

    [PostgresFact]
    public async Task SearchText_IncludesApplicantEntityAndArrivalNumber()
    {
        var req = ApplicantSample();
        req.ApplicantPublicEntities = new()
        {
            new ApplicantPublicEntityDto(null, "شركة النور", "فرع 2"),
        };
        req.FileArrivalNumber = "ر-777";

        var doc = await _create.CreateAsync(req, 1, "lawyer1", 1);
        var stored = await _db.Documents.SingleAsync(d => d.Id == doc.Id);

        // `PB-001`: البلوب مطبَّع.
        Assert.Contains("شركه النور", stored.SearchText);
        Assert.Contains("ر-777", stored.SearchText);
    }
}
