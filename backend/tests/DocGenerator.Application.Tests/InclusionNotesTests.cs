using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات حقل «ملاحظات السند (القرارات الأخرى)» (<c>InclusionNotes</c>) تحت «المتضمن»:
/// حفظ واسترجاع وتحديث في الصفات الثلاث (طالبة تنفيذ/منفذ عليها/عرض وايداع)،
/// مع بقاء الفارغ مقبولًا.
/// </summary>
public class InclusionNotesTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IDocumentService _service;
    private readonly FakeAuditLogger _audit = new();

    public InclusionNotesTests()
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
        _db.SaveChanges();
        var documents = new DocumentRepository(_db);
        var users = new UserRepository(_db);
        var actions = new Repository<ExecutionAction>(_db);
        var baseNumbers = new Repository<DocumentBaseNumber>(_db);
        var registrationDates = new Repository<DocumentRegistrationDate>(_db);
        var occurrences = new Repository<DocumentOccurrence>(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        _service = DocumentServiceFactory.Create(_db, _audit, documents: documents, users: users, actions: actions, baseNumbers: baseNumbers, registrationDates: registrationDates, occurrences: occurrences, uow: uow, tx: tx).Documents;
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
        ContractTypeSelector = "عادي",
        InclusionText = "خلاصة القرار الأول",
    };

    private static DocumentUpsertRequest ExecutedSample(string side) => new()
    {
        GeneralEntitySide = side,
        FileNumber = "777",
        FileYear = "2024",
        ContractTypeSelector = "عادي",
        ContractType = "قرار تنفيذ",
        FileReceiptDate = "5/1/2024",
        ExecutedRequiredAmount = 1000m,
        InclusionText = "خلاصة القرار",
    };

    [Fact]
    public async Task Create_ApplicantWithNotes_PersistsAndReturns()
    {
        var req = ApplicantSample();
        req.InclusionNotes = "القرار الثاني رقم 5 لعام 2024";

        var created = await _service.CreateAsync(req, 1, "lawyer1", 1);
        Assert.Equal("القرار الثاني رقم 5 لعام 2024", created.InclusionNotes);
        Assert.Equal("خلاصة القرار الأول", created.InclusionText);

        var loaded = await _service.GetAsync(created.Id);
        Assert.Equal("القرار الثاني رقم 5 لعام 2024", loaded!.InclusionNotes);
    }

    [Theory]
    [InlineData("executed")]
    [InlineData("deposit")]
    public async Task Create_ExecutedLikeWithNotes_Persists(string side)
    {
        var req = ExecutedSample(side);
        req.InclusionNotes = "قرارات أخرى: 12 و 13";

        var created = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(created.Id);

        Assert.Equal("قرارات أخرى: 12 و 13", loaded!.InclusionNotes);
    }

    [Fact]
    public async Task Update_NotesToEmpty_ClearsValue()
    {
        var req = ApplicantSample();
        req.InclusionNotes = "ملاحظة أولى";
        var created = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var edit = ApplicantSample();
        edit.FileNumber = "520";
        edit.FileYear = "2024";
        edit.InclusionText = "خلاصة القرار الأول";
        edit.InclusionNotes = "";
        var updated = await _service.UpdateAsync(created.Id, edit, "lawyer1", 1);

        Assert.Equal("", updated!.InclusionNotes);
    }

    [Fact]
    public async Task Create_WithoutNotes_ReturnsNullOrEmpty()
    {
        var created = await _service.CreateAsync(ApplicantSample(), 1, "lawyer1", 1);
        Assert.True(string.IsNullOrEmpty(created.InclusionNotes));
    }
}
