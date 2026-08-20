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
/// اختبارات طبيعة أطراف الملف (شخص طبيعي/شخص اعتباري):
/// تخزين الطبيعة والحقول الاعتبارية، وتصفير حقول الهوية الطبيعية عند الاعتباري،
/// والاعتباري في وضع «منفذ عليه» (جهة عامة/شخص اعتباري)، والتخزين ذهابًا وإيابًا.
/// </summary>
public class PartyNatureTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IDocumentService _service;
    private readonly FakeAuditLogger _audit = new();

    public PartyNatureTests()
    {
        _db = TestDb.Create();
        _db.Branches.Add(new Branch { Name = "دمشق", Code = "DAM" });
        _db.Users.Add(new User
        {
            Username = "lawyer1",
            FullName = "محامي",
            Role = UserRole.Lawyer,
            BranchId = 1,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        _db.SaveChanges();
        var documents = new DocumentRepository(_db);
        var users = new UserRepository(_db);
        var guarantors = new Repository<Guarantor>(_db);
        var estates = new Repository<Asset>(_db);
        var actions = new Repository<ExecutionAction>(_db);
        var baseNumbers = new Repository<DocumentBaseNumber>(_db);
        var registrationDates = new Repository<DocumentRegistrationDate>(_db);
        var occurrences = new Repository<DocumentOccurrence>(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        _service = new DocumentService(documents, users, guarantors, estates, actions, baseNumbers, registrationDates, occurrences, new DelegationRepository(_db), uow, tx, _audit);
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
        Court = "دمشق",
        FileReceiptDate = "5/1/2024",
        ExecutedRequiredAmount = 1000m,
        ExecutionApplicants = new()
        {
            new ExecutionApplicantDto(null, "أحمد", "خالد", "الخطيب", null, "أصالة", null, null, null, null, null, null, null, null, new()),
        },
        ExecutedPublicEntities = new()
        {
            new ExecutedPublicEntityDto(null, "المصرف العقاري", "فرع المزة"),
        },
        ExecutedNaturalPersons = new()
        {
            new ExecutedNaturalPersonDto(null, "سامر", "حسن", "علي", "عنوان", "دمشق - المزة", "أصالة", null, null, null, null, null, null, null, null, null, new()),
        },
    };

    [Fact]
    public async Task Create_LegalBorrower_StoresLegalNatureAndClearsNaturalIdentity()
    {
        var req = ApplicantSample();
        req.BorrowerName = "شركة الفلاح";
        req.BorrowerNature = PartyNatureCatalog.Legal;
        req.BorrowerFather = "أب زائد";
        req.BorrowerFamily = "عائلة زائدة";
        req.BorrowerNationalId = "000";
        req.BorrowerRegistrationNumber = "12345";
        req.BorrowerRepresentedBy = "المدير العام";
        req.BorrowerRepresentativeName = "وصي زائد";
        req.BorrowerRepresentativeCapacity = "وصي";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        Assert.Equal(PartyNatureCatalog.Legal, loaded!.BorrowerNature);
        Assert.Equal("شركة الفلاح", loaded.BorrowerName);
        Assert.Null(loaded.BorrowerFather);
        Assert.Null(loaded.BorrowerFamily);
        Assert.Null(loaded.BorrowerMother);
        Assert.Null(loaded.BorrowerNationalId);
        Assert.Equal("12345", loaded.BorrowerRegistrationNumber);
        Assert.Equal("المدير العام", loaded.BorrowerRepresentedBy);
        // الممثل الشرعي (ولي/وصي/قيم) مفهوم يخص الشخص الطبيعي — يُصفَّر عند الاعتباري.
        Assert.Null(loaded.BorrowerRepresentativeName);
        Assert.Null(loaded.BorrowerRepresentativeCapacity);

        var stored = await _db.Documents.SingleAsync();
        Assert.Contains("12345", stored.SearchText);
    }

    [Fact]
    public async Task Create_NaturalBorrower_ClearsLegalFields()
    {
        var req = ApplicantSample();
        req.BorrowerNature = PartyNatureCatalog.Natural;
        req.BorrowerRegistrationNumber = "يجب أن يُصفَّر";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        Assert.Equal(PartyNatureCatalog.Natural, loaded!.BorrowerNature);
        Assert.Null(loaded.BorrowerRegistrationNumber);
        Assert.Null(loaded.BorrowerRepresentedBy);
    }

    [Fact]
    public async Task Create_LegalGuarantor_StoresLegalNatureAndClearsNaturalIdentity()
    {
        var req = ApplicantSample();
        req.Guarantors = new()
        {
            new GuarantorDto(
                Id: null,
                GuarantorNumber: 1,
                Name: "شركة الضامن",
                Father: "أب زائد",
                Family: "عائلة زائدة",
                Mother: null, Birth: null, Register: null, NationalId: "000",
                Address: "حلب", AddressType: "عنوان",
                RepresentativeName: "وصي زائد", RepresentativeFather: null, RepresentativeFamily: null,
                RepresentativeCapacity: "وصي", RepresentativeAddressType: null, RepresentativeAddress: null,
                Heirs: null,
                Nature: PartyNatureCatalog.Legal,
                RegistrationNumber: "999",
                RepresentedBy: "وكيلها"),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        var g = Assert.Single(loaded!.Guarantors);
        Assert.Equal(PartyNatureCatalog.Legal, g.Nature);
        Assert.Equal("شركة الضامن", g.Name);
        Assert.Null(g.Father);
        Assert.Null(g.Family);
        Assert.Null(g.NationalId);
        Assert.Equal("999", g.RegistrationNumber);
        Assert.Equal("وكيلها", g.RepresentedBy);
        Assert.Null(g.RepresentativeName);
    }

    [Fact]
    public async Task Create_LegalApplicant_StoresLegalFieldsAndClearsNaturalIdentity()
    {
        var req = ExecutedSample();
        req.ExecutionApplicants = new()
        {
            new ExecutionApplicantDto(
                Id: null, Name: "شركة التنفيذ", Father: "أب زائد", Family: "عائلة زائدة",
                LegalRepresentative: "وكيل", RepresentationType: "إضافة لتركة",
                DeceasedName: null, DeceasedFather: null, DeceasedFamily: null,
                RepresentativeName: null, RepresentativeFather: null, RepresentativeFamily: null,
                RepresentativeCapacity: null, RepresentativeLegalRepresentative: null,
                Heirs: null,
                Nature: PartyNatureCatalog.Legal,
                RegistrationNumber: "555",
                RepresentedBy: "ممثلها",
                AddressType: "يمثله",
                Address: "محامي الشركة"),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        var a = Assert.Single(loaded!.ExecutionApplicants);
        Assert.Equal(PartyNatureCatalog.Legal, a.Nature);
        Assert.Equal("شركة التنفيذ", a.Name);
        Assert.Null(a.Father);
        Assert.Null(a.Family);
        Assert.Null(a.LegalRepresentative);
        Assert.Equal("أصالة", a.RepresentationType);
        Assert.Equal("555", a.RegistrationNumber);
        Assert.Equal("ممثلها", a.RepresentedBy);
        Assert.Equal("يمثله", a.AddressType);
        Assert.Equal("محامي الشركة", a.Address);
        Assert.Empty(a.Heirs ?? new());
    }

    [Fact]
    public async Task Create_LegalEntity_StoresLegalFieldsAndClearsBranch()
    {
        var req = ExecutedSample();
        req.ExecutedPublicEntities = new()
        {
            new ExecutedPublicEntityDto(
                Id: null, EntityName: "شركة الهدى", EntityBranch: "فرع زائد",
                Nature: PartyNatureCatalog.Legal,
                RegistrationNumber: "777",
                RepresentedBy: "المدير",
                AddressType: "موطن مختار",
                Address: "دمشق",
                Governorate: "حلب"),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        var e = Assert.Single(loaded!.ExecutedPublicEntities);
        Assert.Equal(PartyNatureCatalog.Legal, e.Nature);
        Assert.Equal("شركة الهدى", e.EntityName);
        Assert.Null(e.EntityBranch);
        Assert.Equal("777", e.RegistrationNumber);
        Assert.Equal("المدير", e.RepresentedBy);
        Assert.Equal("موطن مختار", e.AddressType);
        Assert.Equal("دمشق", e.Address);
        Assert.Equal("حلب", e.Governorate);

        var stored = await _db.Documents.SingleAsync();
        Assert.Contains("حلب", stored.SearchText);
    }

    [Fact]
    public async Task Create_PublicEntity_StoresGovernorateAndIncludesItInSearch()
    {
        var req = ExecutedSample();
        req.ExecutedPublicEntities = new()
        {
            new ExecutedPublicEntityDto(null, "المصرف العقاري", "فرع المزة", Governorate: "دمشق"),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        var e = Assert.Single(loaded!.ExecutedPublicEntities);
        Assert.Equal(PartyNatureCatalog.PublicEntity, e.Nature);
        Assert.Equal("المصرف العقاري", e.EntityName);
        Assert.Equal("فرع المزة", e.EntityBranch);
        Assert.Equal("دمشق", e.Governorate);

        var stored = await _db.Documents.SingleAsync();
        Assert.Contains("دمشق", stored.SearchText);
    }

    [Fact]
    public async Task Create_PublicEntity_DefaultsToPublicAndKeepsBranch()
    {
        var req = ExecutedSample();

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        var e = Assert.Single(loaded!.ExecutedPublicEntities);
        Assert.Equal(PartyNatureCatalog.PublicEntity, e.Nature);
        Assert.Equal("المصرف العقاري", e.EntityName);
        Assert.Equal("فرع المزة", e.EntityBranch);
        Assert.Null(e.RegistrationNumber);
        Assert.Null(e.RepresentedBy);
        Assert.Null(e.Address);
        // عند غياب المحافظة تُخزَّن فراغًا كما هو مألوف مع الفرع — دون اختلاف الطبيعة.
        Assert.Equal(string.Empty, e.Governorate);
    }

    [Fact]
    public async Task InvalidNature_DefaultsToNatural()
    {
        var req = ApplicantSample();
        req.BorrowerNature = "قيمة غير صالحة";
        req.BorrowerRegistrationNumber = "يُصفَّر";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        Assert.Equal(PartyNatureCatalog.Natural, loaded!.BorrowerNature);
        Assert.Null(loaded.BorrowerRegistrationNumber);
    }
}
