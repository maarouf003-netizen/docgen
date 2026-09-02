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
/// اختبارات ربط الملفات بالسجل المرجعي للجهات العامة (المرحلة 2): تخزين
/// RegistryId على صفوف الطرفين، نسخة التسريع ApplicantRegistryId على الملف،
/// وتدقيق قبل/بعد لتغيّر الربط عبر محرك تتبع الحقول.
/// </summary>
public class EntityRegistryLinkTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IDocumentService _service;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _entryAId;
    private readonly int _entryBId;

    public EntityRegistryLinkTests()
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

        var groupA = new PublicEntityGroup { CanonicalName = "وزارة التعليم", EntityType = PublicEntityTypeCatalog.Ministry };
        groupA.Entries.Add(new PublicEntity { Governorate = "دمشق", BranchName = "الفرع الرئيسي", Status = EntityStatusCatalog.Final, CreatedById = 1 });
        var groupB = new PublicEntityGroup { CanonicalName = "مديرية النقل", EntityType = PublicEntityTypeCatalog.Administration };
        groupB.Entries.Add(new PublicEntity { Governorate = "دمشق", BranchName = "فرع النقل", Status = EntityStatusCatalog.Final, CreatedById = 1 });
        _db.PublicEntityGroups.AddRange(groupA, groupB);
        _db.SaveChanges();

        _entryAId = groupA.Entries.First().Id;
        _entryBId = groupB.Entries.First().Id;

        var documents = new DocumentRepository(_db);
        var users = new UserRepository(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        _service = new DocumentService(
            documents, users,
            new Repository<Guarantor>(_db),
            new Repository<Asset>(_db),
            new Repository<ExecutionAction>(_db),
            new Repository<DocumentBaseNumber>(_db),
            new Repository<DocumentRegistrationDate>(_db),
            new Repository<DocumentOccurrence>(_db),
            new DelegationRepository(_db), new AppealRepository(_db),
            uow, tx, _audit,
            Microsoft.Extensions.Options.Options.Create(new DocGenerator.Application.Common.ExportOptions()));
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

    [Fact]
    public async Task Create_WithRegistryLink_PersistsRowLink_AndAccelerator()
    {
        var req = ApplicantSample();
        req.ApplicantPublicEntities = new()
        {
            new ApplicantPublicEntityDto(null, "وزارة التعليم", "الفرع الرئيسي", "دمشق", _entryAId),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        var row = Assert.Single(loaded!.ApplicantPublicEntities);
        Assert.Equal(_entryAId, row.RegistryId);
        Assert.Equal(_entryAId, loaded.ApplicantRegistryId);
    }

    [Fact]
    public async Task Create_Accelerator_TakesFirstLinkedRow_WhenSecondUnlinked()
    {
        var req = ApplicantSample();
        req.ApplicantPublicEntities = new()
        {
            new ApplicantPublicEntityDto(null, "مديرية النقل", "فرع النقل", "دمشق", null),
            new ApplicantPublicEntityDto(null, "وزارة التعليم", "الفرع الرئيسي", "دمشق", _entryAId),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        Assert.Equal(_entryAId, loaded!.ApplicantRegistryId);
    }

    [Fact]
    public async Task Update_ChangingRegistryLink_RecomputesAccelerator_AndLogsBeforeAfter()
    {
        var req = ApplicantSample();
        req.ApplicantPublicEntities = new()
        {
            new ApplicantPublicEntityDto(null, "وزارة التعليم", "الفرع الرئيسي", "دمشق", _entryAId),
        };
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        _audit.ChangeLogs.Clear();

        var update = ApplicantSample();
        update.ApplicantPublicEntities = new()
        {
            new ApplicantPublicEntityDto(null, "مديرية النقل", "فرع النقل", "دمشق", _entryBId),
        };
        await _service.UpdateAsync(doc.Id, update, "lawyer1", 1);

        var loaded = await _service.GetAsync(doc.Id);
        Assert.Equal(_entryBId, loaded!.ApplicantRegistryId);

        var change = _audit.ChangeLogs
            .SelectMany(c => c.Changes)
            .Single(c => c.FieldKey == nameof(DocGenerator.Domain.Entities.Document.ApplicantRegistryId));
        Assert.Equal("ربط جهة الطالب بالسجل المرجعي", change.FieldLabel);
        Assert.Equal(_entryAId.ToString(), change.OldValue);
        Assert.Equal(_entryBId.ToString(), change.NewValue);
    }

    [Fact]
    public async Task Update_RemovingRegistryLink_ClearsAccelerator_AndLogsClear()
    {
        var req = ApplicantSample();
        req.ApplicantPublicEntities = new()
        {
            new ApplicantPublicEntityDto(null, "وزارة التعليم", "الفرع الرئيسي", "دمشق", _entryAId),
        };
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        _audit.ChangeLogs.Clear();

        var update = ApplicantSample();
        update.ApplicantPublicEntities = new()
        {
            new ApplicantPublicEntityDto(null, "وزارة التعليم", "الفرع الرئيسي", "دمشق", null),
        };
        await _service.UpdateAsync(doc.Id, update, "lawyer1", 1);

        var loaded = await _service.GetAsync(doc.Id);
        Assert.Null(loaded!.ApplicantRegistryId);

        var change = _audit.ChangeLogs
            .SelectMany(c => c.Changes)
            .Single(c => c.FieldKey == nameof(DocGenerator.Domain.Entities.Document.ApplicantRegistryId));
        Assert.Null(change.NewValue);
    }

    [Fact]
    public async Task ExecutedSide_KeepsRegistryLink_OnPublicRows_AndClearsAccelerator()
    {
        var req = new DocumentUpsertRequest
        {
            GeneralEntitySide = GeneralEntitySideCatalog.Executed,
            FileNumber = "777",
            FileYear = "2024",
            ContractTypeSelector = "عادي",
            ExecutedRequiredAmount = 500m,
        };
        req.ExecutedPublicEntities = new()
        {
            new ExecutedPublicEntityDto(null, "وزارة التعليم", "فرع التنفيذ", PartyNatureCatalog.PublicEntity, null, null, null, null, "دمشق", _entryAId),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        var row = Assert.Single(loaded!.ExecutedPublicEntities);
        Assert.Equal(_entryAId, row.RegistryId);
        // نسخة التسريع خاصة بطرف الطالب فقط.
        Assert.Null(loaded.ApplicantRegistryId);
    }

    [Fact]
    public async Task ExecutedLegalNature_DropsRegistryLink_EvenWhenProvided()
    {
        var req = new DocumentUpsertRequest
        {
            GeneralEntitySide = GeneralEntitySideCatalog.Executed,
            FileNumber = "778",
            FileYear = "2024",
            ContractTypeSelector = "عادي",
            ExecutedRequiredAmount = 500m,
        };
        req.ExecutedPublicEntities = new()
        {
            new ExecutedPublicEntityDto(null, "شركة الأمل", null, PartyNatureCatalog.Legal, "1234", null, null, null, null, _entryAId),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        var row = Assert.Single(loaded!.ExecutedPublicEntities);
        Assert.Equal(PartyNatureCatalog.Legal, row.Nature);
        Assert.Null(row.RegistryId);
    }

    private static DocumentUpsertRequest ExecutionApplicantSample() => new()
    {
        GeneralEntitySide = GeneralEntitySideCatalog.Executed,
        FileNumber = "779",
        FileYear = "2024",
        ContractTypeSelector = "عادي",
        ExecutedRequiredAmount = 500m,
    };

    [Fact]
    public async Task ExecutionApplicant_LegalWithRegistry_KeepsRegistryLink_AcceleratorStaysNull()
    {
        var req = ExecutionApplicantSample();
        req.ExecutionApplicants = new()
        {
            new ExecutionApplicantDto(null, "وزارة التعليم", null, null, null, null,
                null, null, null, null, null, null, null, null,
                null, PartyNatureCatalog.Legal, null, null, null, null, _entryAId),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        var row = Assert.Single(loaded!.ExecutionApplicants);
        Assert.Equal(PartyNatureCatalog.Legal, row.Nature);
        Assert.Equal(_entryAId, row.RegistryId);
        // نسخة التسريع خاصة بالجهة الطالبة الكلاسية (ApplicantPublicEntity) — لا تُمس.
        Assert.Null(loaded.ApplicantRegistryId);
    }

    [Fact]
    public async Task ExecutionApplicant_Natural_DropsRegistryLink()
    {
        var req = ExecutionApplicantSample();
        req.ExecutionApplicants = new()
        {
            new ExecutionApplicantDto(null, "سليم", "حسن", "علي", null, "أصالة",
                null, null, null, null, null, null, null, null,
                null, PartyNatureCatalog.Natural, null, null, null, null, _entryAId),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        var row = Assert.Single(loaded!.ExecutionApplicants);
        Assert.Equal(PartyNatureCatalog.Natural, row.Nature);
        Assert.Null(row.RegistryId);
    }
}
