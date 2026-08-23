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
/// اختبارات «وقوعات الملف» (شطب/تجديد) في وضع «منفذ عليه»/«عرض وايداع»:
/// التسجيل التلقائي عند الشطب والتجديد، والإدارة اليدوية (إضافة/تعديل/حذف)،
/// والتحقق من صحة النوع والحقول، وترتيب الوقوعات في الاستجابة.
/// </summary>
public class OccurrenceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IDocumentService _service;
    private readonly FakeAuditLogger _audit = new();

    public OccurrenceTests()
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
        _service = new DocumentService(documents, users, guarantors, estates, actions, baseNumbers, registrationDates, occurrences, new DelegationRepository(_db), new AppealRepository(_db), uow, tx, _audit, Microsoft.Extensions.Options.Options.Create(new DocGenerator.Application.Common.ExportOptions()));
    }

    public void Dispose() => _db.Dispose();

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

    private static UpsertOccurrenceRequest StruckOffRequest() => new()
    {
        OccurrenceType = OccurrenceTypeCatalog.StruckOff,
        EventDate = "1/8/2026",
        FileNumber = "777",
        Year = 2026,
    };

    private static UpsertOccurrenceRequest RenewalRequest() => new()
    {
        OccurrenceType = OccurrenceTypeCatalog.Renewal,
        EventDate = "5/9/2026",
        FileNumber = "2026/55",
        FileType = "قضية تنفيذ",
        Year = 2026,
        ReceiptNumber = "45",
        ReceiptDate = "2/9/2026",
    };

    [Fact]
    public async Task UpdateExecutedStatus_ToStruckOff_RecordsStruckOffOccurrence()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);

        var ok = await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");
        Assert.True(ok);

        var loaded = await _service.GetAsync(doc.Id);
        var occurrence = Assert.Single(loaded!.Occurrences);
        Assert.Equal(OccurrenceTypeCatalog.StruckOff, occurrence.OccurrenceType);
        Assert.Equal(loaded.StruckOffDate, occurrence.EventDate);
        Assert.Equal("777", occurrence.FileNumber);
        Assert.Equal(loaded.StruckOffDate!.Value.Year, occurrence.Year);
        // التسجيل التلقائي تبعية لعملية الشطب المدفوعة بالفعل بـ «executed-status» — لا تدقيق مستقل.
        Assert.Contains("executed-status", _audit.Actions);
    }

    [Fact]
    public async Task RestoreStruckOff_RecordsRenewalOccurrence_AndKeepsStruckOffOccurrence()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");

        var ok = await _service.RestoreStruckOffAsync(doc.Id, new RenewalRequest
        {
            RenewalFileNumber = "2026/55",
            RenewalFileType = "قضية تنفيذ",
            RenewalFileReceiptNumber = "45",
            RenewalFileReceiptDate = "2/9/2026",
            RenewalDate = "5/9/2026",
        }, "lawyer1");
        Assert.True(ok);

        var loaded = await _service.GetAsync(doc.Id);
        Assert.Equal(2, loaded!.Occurrences.Count);

        var renewal = loaded.Occurrences.Single(o => o.OccurrenceType == OccurrenceTypeCatalog.Renewal);
        Assert.Equal("2026/55", renewal.FileNumber);
        Assert.Equal("قضية تنفيذ", renewal.FileType);
        Assert.Equal(2026, renewal.Year);
        Assert.Equal("45", renewal.ReceiptNumber);
        Assert.Equal(new DateTime(2026, 9, 5), renewal.EventDate);

        var struck = loaded.Occurrences.Single(o => o.OccurrenceType == OccurrenceTypeCatalog.StruckOff);
        Assert.Equal("777", struck.FileNumber);
    }

    [Fact]
    public async Task UpdateAsync_ToStruckOff_RecordsStruckOffOccurrence()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);

        var req = ExecutedSample();
        req.ExecutedStatus = ExecutedStatusCatalog.StruckOff;
        req.StruckOffDate = "10/6/2026";
        var updated = await _service.UpdateAsync(doc.Id, req, "lawyer1", 1);

        var loaded = await _service.GetAsync(doc.Id);
        var occurrence = Assert.Single(loaded!.Occurrences);
        Assert.Equal(OccurrenceTypeCatalog.StruckOff, occurrence.OccurrenceType);
        Assert.Equal(new DateTime(2026, 6, 10), occurrence.EventDate);
    }

    [Fact]
    public async Task UpdateAsync_ReStrikeAfterRenewal_RecordsSecondStruckOffOccurrence()
    {
        // شطب ثم تجديد ثم شطب من جديد: يُسجَّل وقعتا شطب معًا في السجل الزمني.
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");
        await _service.RestoreStruckOffAsync(doc.Id, new RenewalRequest { RenewalFileNumber = "2026/55" }, "lawyer1");

        var req = ExecutedSample();
        req.ExecutedStatus = ExecutedStatusCatalog.StruckOff;
        req.StruckOffDate = "10/7/2026";
        await _service.UpdateAsync(doc.Id, req, "lawyer1", 1);

        var loaded = await _service.GetAsync(doc.Id);
        Assert.Equal(3, loaded!.Occurrences.Count);
        Assert.Equal(2, loaded.Occurrences.Count(o => o.OccurrenceType == OccurrenceTypeCatalog.StruckOff));
        Assert.Equal(1, loaded.Occurrences.Count(o => o.OccurrenceType == OccurrenceTypeCatalog.Renewal));
        // مرتبة تصاعديًا زمنيًا.
        var dates = loaded.Occurrences.Select(o => o.EventDate).ToList();
        Assert.Equal(dates.OrderBy(d => d), dates);
    }

    [Fact]
    public async Task AddOccurrence_AddsStruckOffManually()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);

        var occurrence = await _service.AddOccurrenceAsync(doc.Id, StruckOffRequest(), 1, "lawyer1");

        Assert.True(occurrence.Id > 0);
        Assert.Equal(OccurrenceTypeCatalog.StruckOff, occurrence.OccurrenceType);
        Assert.Equal(new DateTime(2026, 8, 1), occurrence.EventDate);
        Assert.Equal("777", occurrence.FileNumber);
        Assert.Contains("occurrence", _audit.Actions);
    }

    [Fact]
    public async Task UpdateOccurrence_UpdatesFields()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        var occurrence = await _service.AddOccurrenceAsync(doc.Id, StruckOffRequest(), 1, "lawyer1");

        var request = RenewalRequest();
        var updated = await _service.UpdateOccurrenceAsync(doc.Id, occurrence.Id, request, "lawyer1");

        Assert.NotNull(updated);
        Assert.Equal(OccurrenceTypeCatalog.Renewal, updated!.OccurrenceType);
        Assert.Equal("2026/55", updated.FileNumber);
        Assert.Equal("45", updated.ReceiptNumber);
        Assert.Contains("occurrence", _audit.Actions);
    }

    [Fact]
    public async Task DeleteOccurrence_RemovesFromList()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        var occurrence = await _service.AddOccurrenceAsync(doc.Id, StruckOffRequest(), 1, "lawyer1");

        var deleted = await _service.DeleteOccurrenceAsync(doc.Id, occurrence.Id, "lawyer1");
        Assert.True(deleted);

        var loaded = await _service.GetAsync(doc.Id);
        Assert.Empty(loaded!.Occurrences);
        Assert.Contains("occurrence", _audit.Actions);
    }

    [Fact]
    public async Task AddOccurrence_RenewalWithoutFileNumber_Throws()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        var request = RenewalRequest();
        request.FileNumber = null;

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AddOccurrenceAsync(doc.Id, request, 1, "lawyer1"));
    }

    [Fact]
    public async Task AddOccurrence_InvalidType_Throws()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        var request = StruckOffRequest();
        request.OccurrenceType = "غير-صالحة";

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AddOccurrenceAsync(doc.Id, request, 1, "lawyer1"));
    }

    [Fact]
    public async Task AddOccurrence_OnApplicantSideFile_AllowsStatusChangeOccurrence()
    {
        var doc = await _service.CreateAsync(new DocumentUpsertRequest
        {
            BorrowerName = "أحمد",
            AmountNumeric = 1000,
            Currency = "ليرة سورية",
            FileNumber = "520",
            FileYear = "2024",
            FileRegistrationDate = "1/1/2024",
        }, 1, "lawyer1", 1);

        // وقوعات تغيير الحالة متاحة لملفات «طالبة تنفيذ» أيضاً (تريث بحقوله).
        var added = await _service.AddOccurrenceAsync(doc.Id, new UpsertOccurrenceRequest
        {
            OccurrenceType = OccurrenceTypeCatalog.Deferred,
            EventDate = "5/1/2024",
            Details = new Dictionary<string, string?>
            {
                ["tarithNumber"] = "33",
                ["tarithDate"] = "5/1/2024",
                ["tarithRegNumber"] = "44",
                ["tarithRegDate"] = "6/1/2024",
            },
        }, 1, "lawyer1");

        Assert.Equal(OccurrenceTypeCatalog.Deferred, added.OccurrenceType);
        Assert.NotNull(added.Details);
        Assert.Equal("33", added.Details["tarithNumber"]);
    }

    [Fact]
    public async Task AddOccurrence_OnMissingDocument_Throws()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.AddOccurrenceAsync(99999, StruckOffRequest(), 1, "lawyer1"));
    }

    [Fact]
    public async Task UpdateOccurrence_OfAnotherDocument_ReturnsNull()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        var occurrence = await _service.AddOccurrenceAsync(doc.Id, StruckOffRequest(), 1, "lawyer1");
        var other = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);

        var result = await _service.UpdateOccurrenceAsync(other.Id, occurrence.Id, RenewalRequest(), "lawyer1");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_OrdersOccurrencesByEventDateAscending()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        var late = StruckOffRequest();
        late.EventDate = "10/12/2026";
        var early = StruckOffRequest();
        early.EventDate = "1/1/2026";
        await _service.AddOccurrenceAsync(doc.Id, late, 1, "lawyer1");
        await _service.AddOccurrenceAsync(doc.Id, early, 1, "lawyer1");

        var loaded = await _service.GetAsync(doc.Id);
        var dates = loaded!.Occurrences.Select(o => o.EventDate!.Value).ToList();
        Assert.Equal(new DateTime(2026, 1, 1), dates[0]);
        Assert.Equal(new DateTime(2026, 12, 10), dates[1]);
    }

    [Fact]
    public async Task BackfilledLikeData_FromCreateWithStruckOffStatus_IsRecorded()
    {
        // إنشاء ملف مشطوب منذ البداية: يجب أن يُسجَّل وقعة شطب تلقائيًا أيضًا.
        var req = ExecutedSample();
        req.ExecutedStatus = ExecutedStatusCatalog.StruckOff;
        req.StruckOffDate = "10/6/2024";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var loaded = await _service.GetAsync(doc.Id);
        var occurrence = Assert.Single(loaded!.Occurrences);
        Assert.Equal(OccurrenceTypeCatalog.StruckOff, occurrence.OccurrenceType);
        Assert.Equal(new DateTime(2024, 6, 10), occurrence.EventDate);
    }
}
