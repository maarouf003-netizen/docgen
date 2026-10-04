using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات `PB-001` (`INT-014` ← `BQ-034`): تطبيع كل الحقول — المتغيرات
/// الإملائية تُطابَق (هوية/بحث/ترقيم) والعرض يبقى خامًا.
/// تفشل قبل الإصلاح (انشطار/تفويت/ازدواج) وتخضر بعده.
/// </summary>
public class PB001NormalizationTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IPublicEntityService _registry;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _damascusId;
    private readonly int _managerId;
    private readonly int _lawyerId;

    public PB001NormalizationTests()
    {
        _db = TestDb.Create();
        _db.Branches.Add(new Branch { Name = "الفرع الرئيسي - دمشق", Code = "DAM", Governorate = "دمشق" });
        _db.SaveChanges();
        _damascusId = _db.Branches.Single(b => b.Code == "DAM").Id;

        var mgr = new User { Username = "mgr", FullName = "المدير", Role = UserRole.Manager, PasswordHash = "x" };
        var lawyer = new User { Username = "lawyer1", FullName = "محامي دمشق", Role = UserRole.Lawyer, BranchId = _damascusId, PasswordHash = "x" };
        _db.Users.AddRange(mgr, lawyer);
        _db.SaveChanges();
        _managerId = mgr.Id;
        _lawyerId = lawyer.Id;

        _registry = new PublicEntityService(
            new PublicEntityRepository(_db),
            new Repository<Branch>(_db),
            new HeadAlertRepository(_db),
            new Repository<PublicEntityChangeEvent>(_db),
            new Repository<DocumentOccurrence>(_db),
            new Repository<ParentEditSuggestion>(_db),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit,
            new UserRepository(_db),
            new AppealRepository(_db));
    }

    public void Dispose() => _db.Dispose();

    private EntityRegistryActor ManagerActor() => new(_managerId, "المدير", UserRole.Manager, null);

    private DocumentService NewDocumentService() => new(
        new DocumentRepository(_db),
        new UserRepository(_db),
        new Repository<Guarantor>(_db),
        new Repository<Asset>(_db),
        new Repository<ExecutionAction>(_db),
        new Repository<DocumentBaseNumber>(_db),
        new Repository<DocumentRegistrationDate>(_db),
        new Repository<DocumentOccurrence>(_db),
        new DelegationRepository(_db),
        new AppealRepository(_db),
        new HeadAlertService(new HeadAlertRepository(_db), new DocumentRepository(_db), new UserRepository(_db), new Repository<Branch>(_db), new UnitOfWork(_db), new TransactionRunner(_db), _audit),
        new UnitOfWork(_db),
        new TransactionRunner(_db),
        _audit,
        Options.Create(new DocGenerator.Application.Common.ExportOptions()),
        TimeProvider.System,
        TestClock.TimeZone,
        new DbExceptionClassifier());

    private static DocumentUpsertRequest DocRequest(string borrower, string? number = null) => new()
    {
        DocumentType = "بيان دعوى",
        BorrowerName = borrower,
        Applicant = "المدعي",
        Court = "دائرة تنفيذ دمشق",
        ContractType = "تعهد",
        AmountNumeric = 100,
        FileNumber = number,
        FileType = number is null ? null : "عادي",
        FileYear = number is null ? null : "2026",
        FileRegistrationDate = number is null ? null : "1/8/2026",
    };

    [Fact]
    public async Task CreateEntry_BranchVariant_Rejected()
    {
        // نفس الهوية والمحافظة بفرع متغير إملائيًا: اليوم قيد ثانٍ؛ بعده `400`.
        await _registry.CreateAsync(new CreatePublicEntityRequest(
            "وزارة الصحة", "ministry", "دمشق", "الفرع الرئيسي"), ManagerActor());

        await Assert.ThrowsAsync<ArgumentException>(() => _registry.CreateAsync(new CreatePublicEntityRequest(
            "وزارة الصحة", "ministry", "دمشق", "الفرع الرئيسى"), ManagerActor()));
    }

    [Fact]
    public void SearchTextBlob_Normalized()
    {
        // توصيف البلوب: مطبَّع (يخضر بعد التطبيع عند البناء).
        var doc = new Document { BorrowerName = "أحمد", BorrowerFamily = "الخطيب", Court = "دائرة تنفيذ دمشق" };
        var blob = DocumentSearchTextBuilder.Build(doc);

        Assert.Contains("احمد", blob);
        Assert.DoesNotContain("أحمد", blob);
    }

    [Fact]
    public async Task Search_VariantName_Finds()
    {
        // بحث «احمد» يجد ملف «أحمد»: اليوم فوات؛ بعده إصابة عبر البلوب المطبَّع.
        var service = NewDocumentService();
        await service.CreateAsync(DocRequest("أحمد"), _lawyerId, "tester", _damascusId);

        var result = await service.SearchAsync("احمد", null, null, null, null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Numbering_CourtVariant_Conflict()
    {
        // نفس الرقم بدائرتين متغيرتين إملائيًا: اليوم نجاح مكرر؛ بعده `409`.
        var service = NewDocumentService();
        await service.CreateAsync(DocRequest("الأول", "555"), _lawyerId, "tester", _damascusId);

        var second = DocRequest("الثاني", "555");
        second.Court = "دائره تنفيذ دمشق";
        await Assert.ThrowsAsync<DocGenerator.Application.Common.DocumentConflictException>(
            () => service.CreateAsync(second, _lawyerId, "tester", _damascusId));
    }

    [Fact]
    public async Task Display_StaysRaw()
    {
        // توصيف (يخضر قبل/بعد): العرض يبقى بالإملاء الأصلي دائمًا.
        var service = NewDocumentService();
        var created = await service.CreateAsync(DocRequest("أحمد"), _lawyerId, "tester", _damascusId);

        Assert.Equal("أحمد", created.BorrowerName);
        Assert.Equal("دائرة تنفيذ دمشق", created.Court);
    }
}
