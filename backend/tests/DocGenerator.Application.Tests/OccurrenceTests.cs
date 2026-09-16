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
/// التسجيل التلقائي عند الشطب والتجديد (نظامي المصدر)، والقراءة، والتحقق من
/// صحة الحقول، وترتيب الوقوعات في الاستجابة. الإدارة اليدوية (إضافة/تعديل/حذف)
/// أُلغيت من المحرر ونقاط النهاية — فلا اختبارات لها هنا.
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
        _service = new DocumentService(documents, users, guarantors, estates, actions, baseNumbers, registrationDates, occurrences, new DelegationRepository(_db), new AppealRepository(_db), uow, tx, _audit, Microsoft.Extensions.Options.Options.Create(new DocGenerator.Application.Common.ExportOptions()), TimeProvider.System, TestClock.TimeZone);
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

    [Fact]
    public async Task UpdateExecutedStatus_ToStruckOff_RecordsStruckOffOccurrence()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);

        var ok = await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");
        Assert.True(ok);

        var loaded = await _service.GetAsync(doc.Id);
        var occurrence = Assert.Single(loaded!.Occurrences);
        Assert.Equal(OccurrenceTypeCatalog.StruckOff, occurrence.OccurrenceType);
        Assert.Equal(OccurrenceSourceCatalog.System, occurrence.Source);
        Assert.Equal(loaded.StruckOffDate, occurrence.EventDate);
        Assert.Equal("777", occurrence.FileNumber);
        Assert.Null(occurrence.FileType);
        Assert.Equal(loaded.StruckOffDate!.Value.Year, occurrence.Year);
        // التسجيل التلقائي تبعية لعملية الشطب المدفوعة بالفعل بـ «executed-status» — لا تدقيق مستقل.
        Assert.Contains("executed-status", _audit.Actions);
    }

    [Fact]
    public async Task UpdateExecutedStatus_ToStruckOff_CarriesFileTypeIntoOccurrence()
    {
        var req = ExecutedSample();
        req.FileType = "قضية تنفيذ";
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var ok = await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");
        Assert.True(ok);

        var loaded = await _service.GetAsync(doc.Id);
        var occurrence = Assert.Single(loaded!.Occurrences);
        Assert.Equal(OccurrenceTypeCatalog.StruckOff, occurrence.OccurrenceType);
        Assert.Equal(OccurrenceSourceCatalog.System, occurrence.Source);
        Assert.Equal("777", occurrence.FileNumber);
        Assert.Equal("قضية تنفيذ", occurrence.FileType);
    }

    [Fact]
    public async Task UpdateStatus_ApplicantSideStruckOff_RecordsFileNumberTypeAndYear()
    {
        // نظام «الجهة العامة طالبة تنفيذ»: وقعة الشطب تحمل رقم الملف المشطوب ونوعه وسنة شطبه
        // كما في مسار «منفذ عليه»/«عرض وايداع» لتظهر في نافذة تفاصيل الوقوعات كلها.
        var req = ExecutedSample();
        req.GeneralEntitySide = GeneralEntitySideCatalog.Applicant;
        req.BorrowerName = "أحمد";
        req.BorrowerFather = "خالد";
        req.BorrowerFamily = "الخطيب";
        req.FileRegistrationDate = "1/1/2026";
        req.FileType = "قضية تنفيذ";
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var ok = await _service.UpdateStatusAsync(doc.Id, ExecutionStatusCatalog.StateStruckOff,
            new Dictionary<string, string?> { ["struckOffDate"] = "1/8/2026" }, "lawyer1");
        Assert.True(ok);

        var loaded = await _service.GetAsync(doc.Id);
        var occurrence = Assert.Single(loaded!.Occurrences);
        Assert.Equal(OccurrenceTypeCatalog.StruckOff, occurrence.OccurrenceType);
        Assert.Equal("777", occurrence.FileNumber);
        Assert.Equal("قضية تنفيذ", occurrence.FileType);
        Assert.Equal(2026, occurrence.Year);
        Assert.Equal(new DateTime(2026, 8, 1), occurrence.EventDate);
    }

    [Fact]
    public async Task UpdateExecutedStatus_ToStruckOff_WithEligibleRotation_StoresEffectiveNumberAtStrike()
    {
        // وقعة الشطب يجب أن تحمل الهوية الفعّالة وقت الشطب (آخر رقم أساس ≤ سنة الشطب عبر
        // المحلل المركزي) لا رقم الملف الأصلي — فيظهر رقم التدوير الأحدث عند عرض الوقعة.
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = doc.Id,
            Year = DateTime.Today.Year - 1,
            BaseNumber = "900",
            CreatedById = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var ok = await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");
        Assert.True(ok);

        var loaded = await _service.GetAsync(doc.Id);
        var occurrence = Assert.Single(loaded!.Occurrences);
        Assert.Equal(OccurrenceTypeCatalog.StruckOff, occurrence.OccurrenceType);
        Assert.Equal("900", occurrence.FileNumber);
        Assert.Equal(loaded.StruckOffDate!.Value.Year, occurrence.Year);
    }

    [Fact]
    public async Task UpdateStatus_ApplicantSideStruckOff_WithEligibleRotation_StoresEffectiveNumber()
    {
        // نفس قاعدة الهوية الفعّالة تنطبق على وقعة الشطب في نظام «طالبة تنفيذ».
        var req = ExecutedSample();
        req.GeneralEntitySide = GeneralEntitySideCatalog.Applicant;
        req.BorrowerName = "أحمد";
        req.BorrowerFather = "خالد";
        req.BorrowerFamily = "الخطيب";
        req.FileRegistrationDate = "1/1/2026";
        req.FileType = "قضية تنفيذ";
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = doc.Id,
            Year = DateTime.Today.Year - 1,
            BaseNumber = "900",
            CreatedById = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var ok = await _service.UpdateStatusAsync(doc.Id, ExecutionStatusCatalog.StateStruckOff,
            new Dictionary<string, string?> { ["struckOffDate"] = "1/8/2026" }, "lawyer1");
        Assert.True(ok);

        var loaded = await _service.GetAsync(doc.Id);
        var occurrence = Assert.Single(loaded!.Occurrences);
        Assert.Equal(OccurrenceTypeCatalog.StruckOff, occurrence.OccurrenceType);
        Assert.Equal(OccurrenceSourceCatalog.System, occurrence.Source);
        Assert.Equal("900", occurrence.FileNumber);
        Assert.Equal("قضية تنفيذ", occurrence.FileType);
        Assert.Equal(2026, occurrence.Year);
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
        Assert.Equal(OccurrenceSourceCatalog.System, renewal.Source);
        Assert.Equal("2026/55", renewal.FileNumber);
        Assert.Equal("قضية تنفيذ", renewal.FileType);
        Assert.Equal(2026, renewal.Year);
        Assert.Equal("45", renewal.ReceiptNumber);
        Assert.Equal(new DateTime(2026, 9, 5), renewal.EventDate);

        var struck = loaded.Occurrences.Single(o => o.OccurrenceType == OccurrenceTypeCatalog.StruckOff);
        Assert.Equal(OccurrenceSourceCatalog.System, struck.Source);
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
        // وقعة الشطب في مسار التعديل (UpdateAsync) نظامية التوسيم كمسار الحالة المنفذة.
        Assert.Equal(OccurrenceSourceCatalog.System, occurrence.Source);
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
        // كل وقوعات هذا المسار (شطب آلي/تجديد استعادة/شطب آلي ثانٍ) نظامية التوسيم.
        Assert.All(loaded.Occurrences, o => Assert.Equal(OccurrenceSourceCatalog.System, o.Source));
    }

    [Fact]
    public async Task GetAsync_OrdersOccurrencesByEventDateAscending()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        // بذر مباشر في القاعدة (الإدخال اليدوي عبر الخدمة أُلغي مع نقاط النهاية) —
        // السجل الزمني يبقى مقروءًا ومرتبًا زمنيًا في الاستجابة.
        await _db.DocumentOccurrences.AddRangeAsync(
            new DocumentOccurrence
            {
                DocumentId = doc.Id,
                OccurrenceType = OccurrenceTypeCatalog.StruckOff,
                Source = OccurrenceSourceCatalog.System,
                EventDate = new DateTime(2026, 12, 10),
                FileNumber = "777",
                Year = 2026,
                CreatedById = 1,
            },
            new DocumentOccurrence
            {
                DocumentId = doc.Id,
                OccurrenceType = OccurrenceTypeCatalog.StruckOff,
                Source = OccurrenceSourceCatalog.System,
                EventDate = new DateTime(2026, 1, 1),
                FileNumber = "777",
                Year = 2026,
                CreatedById = 1,
            });
        await _db.SaveChangesAsync();

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

    private static DocumentUpsertRequest DepositSample() => new()
    {
        GeneralEntitySide = GeneralEntitySideCatalog.Deposit,
        DocumentType = "عرض وايداع",
        FileNumber = "888",
        FileYear = "2024",
        ContractTypeSelector = "عادي",
        Court = "دمشق",
        Applicant = "معروض",
        FileReceiptDate = "5/1/2024",
        ExecutedRequiredAmount = 1500m,
        ExecutionApplicants = new()
        {
            new ExecutionApplicantDto(null, "هاني", "سامر", "النجار", null, "أصالة", null, null, null, null, null, null, null, null, new()),
        },
        ExecutedPublicEntities = new()
        {
            new ExecutedPublicEntityDto(null, "المصرف التجاري", "فرع دمشق"),
        },
        ExecutedNaturalPersons = new()
        {
            new ExecutedNaturalPersonDto(null, "رامي", "سالم", "عبد", "عنوان", "دمشق - المدينة", "أصالة", null, null, null, null, null, null, null, null, null, new()),
        },
    };

    [Fact]
    public async Task UpdateAsync_EditStruckOffToExecuted_Direct_Throws()
    {
        // §4.6 حارس: مشطوب → منفذ مباشر عبر نموذج التعديل ممنوع — يجب الإعادة إلى المتداول أولًا.
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");

        var req = ExecutedSample();
        req.ExecutedStatus = ExecutedStatusCatalog.Executed;

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateAsync(doc.Id, req, "lawyer1", 1));
        Assert.Contains("لا يمكن نقل ملف مشطوب إلى «منفذ» مباشرة", ex.Message);

        // الحالة لم تتغير في قاعدة البيانات.
        var loaded = await _service.GetAsync(doc.Id);
        Assert.Equal(ExecutedStatusCatalog.StruckOff, loaded!.ExecutedStatus);
    }

    [Fact]
    public async Task UpdateAsync_EditExecutedStatus_SameStatus_StaysAllowed()
    {
        // §4.6: الإبقاء على «منفذ» دون تغيير يبقى مقبولًا في نموذج التعديل (حواري مستقر).
        var req = ExecutedSample();
        req.ExecutedStatus = ExecutedStatusCatalog.Executed;
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var updated = await _service.UpdateAsync(doc.Id, req, "lawyer1", 1);
        Assert.Equal(ExecutedStatusCatalog.Executed, updated!.ExecutedStatus);
    }

    [Fact]
    public async Task UpdateAsync_EditExecutedLikeFromExecuted_ToStruck_Throws()
    {
        // §4.6: وضع «منفذ» في صفة «الجهة العامة منفذ عليها» نهائي — لا يُشطب بالتحرير.
        var req = ExecutedSample();
        req.ExecutedStatus = ExecutedStatusCatalog.Executed;
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var edit = ExecutedSample();
        edit.ExecutedStatus = ExecutedStatusCatalog.StruckOff;
        edit.StruckOffDate = "10/8/2026";

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateAsync(doc.Id, edit, "lawyer1", 1));
        Assert.Contains("الجهة العامة منفذ عليها» نهائي", ex.Message);

        var loaded = await _service.GetAsync(doc.Id);
        Assert.Equal(ExecutedStatusCatalog.Executed, loaded!.ExecutedStatus);
        Assert.Empty(loaded.Occurrences);
    }

    [Fact]
    public async Task UpdateAsync_EditDepositFromExecuted_ToStruck_Throws()
    {
        // §4.6: «عرض وايداع» من وضع «منفذ» لا يُشطب في نموذج التعديل.
        var req = DepositSample();
        req.ExecutedStatus = ExecutedStatusCatalog.Executed;
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var edit = DepositSample();
        edit.ExecutedStatus = ExecutedStatusCatalog.StruckOff;
        edit.StruckOffDate = "10/8/2026";

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateAsync(doc.Id, edit, "lawyer1", 1));
        Assert.Contains("لا يمكن شطب «عرض وايداع» من وضع «منفذ»", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_EditDepositFromExecuted_ToTraded_Direct_Throws()
    {
        // §4.6: إرجاع «عرض وايداع» من «منفذ» إلى المتداول عبر التحرير ممنوع (كتاب السير بالنافذة حصرًا).
        var req = DepositSample();
        req.ExecutedStatus = ExecutedStatusCatalog.Executed;
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var edit = DepositSample();
        edit.ExecutedStatus = null;

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateAsync(doc.Id, edit, "lawyer1", 1));
        Assert.Contains("كتاب السير بالملف", ex.Message);
    }
}