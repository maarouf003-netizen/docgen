using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

public class DocumentAppealServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IDocumentAppealService _service;
    private readonly IHeadAlertService _alertService;
    private readonly FakeAuditLogger _audit = new();

    private readonly Branch _branch;
    private readonly Branch _otherBranch;
    private readonly User _lawyer1;
    private readonly User _lawyer2;
    private readonly User _head1;
    private readonly User _head2;

    public DocumentAppealServiceTests()
    {
        _db = TestDb.Create();

        _branch = new Branch { Name = "دمشق", Code = "DAM" };
        _otherBranch = new Branch { Name = "اللاذقية", Code = "LAT" };
        _db.Branches.AddRange(_branch, _otherBranch);
        _db.SaveChanges();

        _lawyer1 = User(_branch.Id, "lawyer1", "محامي دمشق");
        _lawyer2 = User(_branch.Id, "lawyer2", "محامي دمشق ثانٍ");
        _head1 = User(_branch.Id, "head1", "رئيس قسم دمشق", UserRole.Head);
        _head2 = User(_otherBranch.Id, "head2", "رئيس قسم اللاذقية", UserRole.Head);
        _db.Users.AddRange(_lawyer1, _lawyer2, _head1, _head2);
        _db.SaveChanges();

        _alertService = new HeadAlertService(
            new HeadAlertRepository(_db),
            new DocumentRepository(_db),
            new UserRepository(_db),
            new Repository<Branch>(_db),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit);

        _service = new DocumentAppealService(
            new AppealRepository(_db),
            new DocumentRepository(_db),
            new UserRepository(_db),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit,
            _alertService,
            TimeProvider.System,
            TestClock.TimeZone);
    }

    public void Dispose() => _db.Dispose();

    private static User User(int? branchId, string username, string fullName, UserRole role = UserRole.Lawyer) => new()
    {
        Username = username,
        FullName = fullName,
        Role = role,
        BranchId = branchId,
        IsActive = true,
        PasswordHash = new Services.PasswordHasher().Hash("123456"),
    };

    /// <summary>ملف «طالبة تنفيذ» مقيد بجهتين عامتين طالبتين، في ملكية lawyer1.</summary>
    private async Task<Document> CreateApplicantDocAsync(bool isDraft = false)
    {
        var doc = NewDoc(GeneralEntitySideCatalog.Applicant);
        doc.IsDraft = isDraft;
        doc.BorrowerName = "أحمد";
        doc.BorrowerFather = "خالد";
        doc.BorrowerFamily = "الخطيب";
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _db.ApplicantPublicEntities.AddRange(
            new ApplicantPublicEntity { DocumentId = doc.Id, Name = "المؤسسة العامة للكهرباء" },
            new ApplicantPublicEntity { DocumentId = doc.Id, Name = "مديرية الموارد المائية" });
        await _db.SaveChangesAsync();
        return doc;
    }

    /// <summary>ملف «منفذ عليه» بطبيعيين منفذ عليهما، في ملكية lawyer1.</summary>
    private async Task<Document> CreateExecutedDocAsync()
    {
        var doc = NewDoc(GeneralEntitySideCatalog.Executed);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _db.ExecutedNaturalPersons.AddRange(
            new ExecutedNaturalPerson { DocumentId = doc.Id, Name = "سامر", Father = "نبيل", Family = "الحلبي" },
            new ExecutedNaturalPerson { DocumentId = doc.Id, Name = "فادي", Father = "سمير", Family = "الدمشقي" });
        await _db.SaveChangesAsync();
        return doc;
    }

    private Document NewDoc(string side) => new()
    {
        CreatedById = _lawyer1.Id,
        BranchId = _branch.Id,
        BranchName = _branch.Name,
        GeneralEntitySide = side,
        IsDraft = false,
        Court = "دمشق",
        FileNumber = "520",
        FileType = "حقوق",
        FileYear = "2024",
        DocumentType = "متداول - أحمد خالد الخطيب",
    };

    private static UpsertAppealRequest Request(
        string direction,
        List<AppealPartySelectionDto>? appellants,
        string decisionDate = "1/8/2026") => new(
        Direction: direction,
        Appellants: appellants,
        AppealTypeLabel: "عادي",
        AppealedDecisionText: "نص القرار المستأنف",
        AppealedDecisionSummary: "ملخص القرار",
        AppealedDecisionDate: decisionDate,
        InspectionBookNumber: "كتاب-10",
        InspectionBookDate: "2/8/2026",
        GroundsSummary: "موجبات الاستئناف",
        NoticeNumber: null,
        NoticeDate: null,
        AppellateCourt: null,
        AppealBaseNumber: null,
        AppealYear: null,
        DepositBookNumber: null,
        DepositBookDate: null,
        DefenseOpinion: null,
        Notes: null);

    [Fact]
    public async Task Create_AppellantsDirection_BuildsSnapshotsAndAlertsHead()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities
            .Where(e => e.DocumentId == doc.Id).OrderBy(e => e.Id).ToListAsync();

        var dto = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");

        Assert.Equal(AppealStatusCatalog.Pending, dto.Status);
        Assert.Equal("مستأنِفين", dto.DirectionLabel);
        Assert.Equal("2026-08-01", dto.AppealedDecisionDate);
        Assert.Equal("2026-08-02", dto.InspectionBookDate);
        var appellant = Assert.Single(dto.Appellants);
        Assert.Equal("المؤسسة العامة للكهرباء", appellant.Name);
        // المستأنف عليهم = كل أطراف الملف ناقص المختار: بقية الجهة + المقترض (مواجهة الجميع حكمًا).
        Assert.Equal(2, dto.Appellees.Count);
        Assert.Contains(dto.Appellees, p => p.Name == "مديرية الموارد المائية");
        Assert.Contains(dto.Appellees, p => p.Name == "أحمد خالد الخطيب");
        Assert.DoesNotContain(dto.Appellees, p => p.Name == "المؤسسة العامة للكهرباء");

        var headAlerts = await _alertService.ListForHeadAsync(_head1.Id, _branch.Id);
        Assert.Contains(headAlerts, a => a.TargetType == "head" && a.Message.Contains("يرجى اختيار محامي"));
    }

    [Fact]
    public async Task Create_DepositFields_NulledForAppellants_KeptForAgainstUs()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities
            .Where(e => e.DocumentId == doc.Id).OrderBy(e => e.Id).ToListAsync();

        // مسار «مستأنِفين»: قيم الإيداع المرسلة تُصفَّر (R1).
        var appellants = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) })
            with { DepositBookNumber = "K-1", DepositBookDate = "3/8/2026" },
            _lawyer1.Id, "lawyer1");
        Assert.Null(appellants.DepositBookNumber);
        Assert.Null(appellants.DepositBookDate);

        // مسار «مستأنف علينا»: القيم تُحفظ كما هي.
        var againstUs = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.AgainstUs,
                new List<AppealPartySelectionDto> { new("borrower", doc.Id) })
            with { DepositBookNumber = "K-2", DepositBookDate = "4/8/2026" },
            _lawyer1.Id, "lawyer1");
        Assert.Equal("K-2", againstUs.DepositBookNumber);
        Assert.Equal("2026-08-04", againstUs.DepositBookDate);
    }

    [Fact]
    public async Task Create_ByNonOwner_Throws()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer2.Id, "lawyer2"));
    }

    [Fact]
    public async Task Create_OnDraftFile_Throws()
    {
        var doc = await CreateApplicantDocAsync(isDraft: true);
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1"));
        Assert.Contains("تحت الرفع", ex.Message);
    }

    [Fact]
    public async Task Create_WithoutSelection_Throws()
    {
        var doc = await CreateApplicantDocAsync();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(doc.Id, Request(AppealDirectionCatalog.Appellants, null), _lawyer1.Id, "lawyer1"));
    }
    [Fact]
    public async Task Create_AgainstUsDirection_SelectsFromExecutedParties()
    {
        var doc = await CreateExecutedDocAsync();
        var persons = await _db.ExecutedNaturalPersons
            .Where(p => p.DocumentId == doc.Id).OrderBy(p => p.Id).ToListAsync();

        var dto = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.AgainstUs,
                new List<AppealPartySelectionDto> { new("executed-natural", persons[0].Id) }),
            _lawyer1.Id, "lawyer1");

        Assert.Equal("مستأنف علينا", dto.DirectionLabel);
        Assert.Equal("سامر نبيل الحلبي", Assert.Single(dto.Appellants).Name);
        Assert.Equal("فادي سمير الدمشقي", Assert.Single(dto.Appellees).Name);
    }

    [Fact]
    public async Task Create_AgainstUs_OnApplicantFile_IncludesPublicEntityInAppellees()
    {
        var doc = await CreateApplicantDocAsync();
        var created = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.AgainstUs,
                new List<AppealPartySelectionDto> { new("borrower", doc.Id) }),
            _lawyer1.Id, "lawyer1");

        // المستأنف عليهم يشمل الجهة العامة الطالبة تلقائيًا (مواجهة الجميع).
        Assert.Contains(created.Appellees, p => p.Kind == "applicant-entity");
        Assert.DoesNotContain(created.Appellees, p => p.Name == "أحمد خالد الخطيب");
    }

    [Fact]
    public async Task Create_WithForeignPartySelection_Throws()
    {
        var doc = await CreateApplicantDocAsync();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", 99999) }),
            _lawyer1.Id, "lawyer1"));
        Assert.Contains("لا يتبع أطراف الملف", ex.Message);
    }

    [Fact]
    public async Task UpdateAndDelete_BeforeAssignment_ByCreator_Succeed()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        var selections = new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) };
        var created = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants, selections), _lawyer1.Id, "lawyer1");

        var updated = await _service.UpdateAsync(created.Id,
            Request(AppealDirectionCatalog.Appellants, selections), _lawyer1.Id, "lawyer1");
        Assert.NotNull(updated);

        Assert.True(await _service.DeleteAsync(created.Id, _lawyer1.Id, "lawyer1"));
        Assert.Null(await _service.GetAsync(created.Id));
    }

    [Fact]
    public async Task Update_AfterAssignment_IsBlocked()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        var selections = new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) };
        var created = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants, selections), _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateAsync(created.Id, Request(AppealDirectionCatalog.Appellants, selections), _lawyer1.Id, "lawyer1"));
    }

    [Fact]
    public async Task Assign_ByHeadOfOtherBranch_Throws()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        var created = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head2.Id, "head2"));
    }

    [Fact]
    public async Task Assign_CleansHeadAlertAndNotifiesLawyer()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        var created = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");

        var assigned = await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");

        Assert.NotNull(assigned);
        Assert.Equal(_lawyer2.Id, assigned!.AssignedLawyerId);
        // تنبيه الرئيس المعلّق صُفّي، ووصل المحامي تنبيه الإحالة.
        var headAlerts = await _alertService.ListForHeadAsync(_head1.Id, _branch.Id);
        Assert.DoesNotContain(headAlerts, a => a.Message.Contains("يرجى اختيار محامي"));
        var lawyerAlerts = await _alertService.ListForLawyerAsync(_lawyer2.Id);
        Assert.Contains(lawyerAlerts, a => a.Message.Contains("أحال إليك رئيس القسم استئناف"));

        // إسناد مكرر مرفوض.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1"));
    }
    [Fact]
    public async Task Decide_ByFollower_SetsOutcomeAndNotifiesBaseLawyer()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        var created = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");

        var decided = await _service.DecideAsync(created.Id,
            new DecideAppealRequest("قرار-55", "15/9/2026", "قبول الاستئناف جزئيًا", AppealOutcomeCatalog.InFavor),
            _lawyer2.Id, "lawyer2");

        Assert.NotNull(decided);
        Assert.Equal(AppealStatusCatalog.Decided, decided!.Status);
        Assert.Equal("2026-09-15", decided.DecisionDate);
        Assert.Equal("للصالح", decided.OutcomeLabel);

        // إشعار محامي الملف الأساس (مختلف عن المتابع) بالحسم.
        var baseLawyerAlerts = await _alertService.ListForLawyerAsync(_lawyer1.Id);
        Assert.Contains(baseLawyerAlerts, a => a.Message.Contains("محسومًا"));

        // حسم مكرر مرفوض، وحسم من غير المتابع مرفوض.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.DecideAsync(created.Id,
                new DecideAppealRequest("قرار-56", "20/9/2026", "نص", AppealOutcomeCatalog.Against),
                _lawyer2.Id, "lawyer2"));
    }

    [Fact]
    public async Task Strike_ByFollower_SetsStruckFields()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        var created = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");

        var struck = await _service.StrikeAsync(created.Id,
            new StrikeAppealRequest("قرار-شطب-3", "1/10/2026"), _lawyer2.Id, "lawyer2");

        Assert.NotNull(struck);
        Assert.Equal(AppealStatusCatalog.StruckOff, struck!.Status);
        Assert.Equal("2026-10-01", struck.StruckOffDate);
        Assert.Equal("قرار-شطب-3", struck.StruckOffDecisionNumber);
    }

    [Fact]
    public async Task Decide_WithInvalidOutcomeOrMissingFields_Throws()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        var created = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.DecideAsync(created.Id,
                new DecideAppealRequest(null, "1/10/2026", "نص", AppealOutcomeCatalog.InFavor), _lawyer2.Id, "lawyer2"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.DecideAsync(created.Id,
                new DecideAppealRequest("رقم", "تاريخ غير صالح", "نص", AppealOutcomeCatalog.InFavor), _lawyer2.Id, "lawyer2"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.DecideAsync(created.Id,
                new DecideAppealRequest("رقم", "1/10/2026", "نص", "قيمة غريبة"), _lawyer2.Id, "lawyer2"));
    }

    [Fact]
    public async Task UpdateRegistration_ByAssignedLawyer_ParsesFreeDate()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        var created = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");

        var updated = await _service.UpdateRegistrationAsync(created.Id,
            new UpdateAppealRegistrationRequest("جمركي", "محكمة استئناف دمشق", "1450", "2026", "7/8/2026"),
            _lawyer2.Id, "lawyer2");

        Assert.NotNull(updated);
        Assert.Equal("محكمة استئناف دمشق", updated!.AppellateCourt);
        Assert.Equal("1450", updated.AppealBaseNumber);
        Assert.Equal("2026-08-07", updated.RegistrationDate);
        // من غير المتابع مرفوض.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateRegistrationAsync(created.Id,
                new UpdateAppealRegistrationRequest(null, null, null, null, null), _lawyer1.Id, "lawyer1"));
    }
    [Fact]
    public async Task Rotation_OldYearNeedsRotation_ThenCurrentYearClearsIt()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        var created = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");

        // القيد قبل التدوير: رقم أساس لسنة سابقة.
        var previousYear = (DateTime.Today.Year - 1).ToString();
        await _service.UpdateRegistrationAsync(created.Id,
            new UpdateAppealRegistrationRequest(null, null, "900", previousYear, null),
            _lawyer2.Id, "lawyer2");

        // قبل التدوير: رقم لسنة سابقة بلا رقم سنة حالية ← يحتاج تدويرًا.
        var before = await _service.GetAsync(created.Id);
        Assert.NotNull(before);
        Assert.True(before!.NeedsRotation);
        Assert.Equal("900", before.CurrentBaseNumber);

        await _service.SaveBaseNumbersAsync(created.Id,
            new SaveAppealBaseNumbersRequest(new List<AppealBaseNumberEntry>
            {
                new(DateTime.Today.Year.ToString()),
            }), _lawyer2.Id, "lawyer2");

        var after = await _service.GetAsync(created.Id);
        Assert.NotNull(after);
        Assert.False(after!.NeedsRotation);
        Assert.Equal(DateTime.Today.Year.ToString(), after.CurrentBaseNumber);
        var history = await _service.GetBaseNumberHistoryAsync(created.Id);
        Assert.Equal(2, history.Count);
        Assert.Contains(history, h => h.BaseNumber == "900");

        // حفظ ثانٍ لنفس السنة يُنشئ سجلًا جديدًا (لا يحدّث) والمعتبر هو الأحدث CreatedAt.
        await _service.SaveBaseNumbersAsync(created.Id,
            new SaveAppealBaseNumbersRequest(new List<AppealBaseNumberEntry>
            {
                new("1450"),
            }), _lawyer2.Id, "lawyer2");

        var after2 = await _service.GetAsync(created.Id);
        Assert.NotNull(after2);
        Assert.False(after2!.NeedsRotation);
        Assert.Equal("1450", after2.CurrentBaseNumber);
        var history2 = await _service.GetBaseNumberHistoryAsync(created.Id);
        Assert.Equal(3, history2.Count);
        // ترتيب تصاعدي بالسنوات، وداخل السنة نفسها الأحدث CreatedAt أولًا: القديمة ثم الرقمان الحاليان.
        Assert.Equal("900", history2[0].BaseNumber);
        Assert.Equal("1450", history2[1].BaseNumber);
        Assert.Equal(DateTime.Today.Year.ToString(), history2[2].BaseNumber);
        Assert.Contains(history2, h => h.BaseNumber == "900");
    }

    [Fact]
    public async Task Rotation_PastYearRowNeedsRotation_UntilCurrentYearRowExists()
    {
        // سجل رقم أساس فعلي لسنة سابقة (لا مجرد عمودي القيد) ولا سجل لسنة اليوم
        // ← يحتاج تدويرًا، ويبقى الرقم الفعّال المعروض هو رقم السنة السابقة.
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        var created = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");

        _db.AppealBaseNumbers.Add(new AppealBaseNumber
        {
            AppealId = created.Id,
            Year = DateTime.Today.Year - 1,
            BaseNumber = "777",
            CreatedById = _lawyer2.Id,
        });
        await _db.SaveChangesAsync();

        var before = await _service.GetAsync(created.Id);
        Assert.NotNull(before);
        Assert.True(before!.NeedsRotation);
        Assert.Equal("777", before.CurrentBaseNumber);

        await _service.SaveBaseNumbersAsync(created.Id,
            new SaveAppealBaseNumbersRequest(new List<AppealBaseNumberEntry>
            {
                new(DateTime.Today.Year.ToString()),
            }), _lawyer2.Id, "lawyer2");

        var after = await _service.GetAsync(created.Id);
        Assert.NotNull(after);
        Assert.False(after!.NeedsRotation);
    }

    private static SaveAppealBaseNumbersRequest SaveAs(string number) =>
        new(new List<AppealBaseNumberEntry> { new(number) });

    [Fact]
    public async Task Rotation_RequiresAssigneeAndPending()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        var created = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");

        // بلا إسناد: المنشئ نفسه مرفوض (R3).
        var exUnassigned = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveBaseNumbersAsync(created.Id, SaveAs("100"), _lawyer1.Id, "lawyer1"));
        Assert.Contains("لا تتابعه", exUnassigned.Message);

        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");

        // المنشئ غير المسند مرفوض بعد الإسناد.
        var exCreator = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveBaseNumbersAsync(created.Id, SaveAs("100"), _lawyer1.Id, "lawyer1"));
        Assert.Contains("لا تتابعه", exCreator.Message);

        // المحسوم: المسند نفسه مرفوض بعد الحسم.
        await _service.DecideAsync(created.Id,
            new DecideAppealRequest("قرار-1", "15/9/2026", "نص", AppealOutcomeCatalog.InFavor),
            _lawyer2.Id, "lawyer2");
        var exDecided = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveBaseNumbersAsync(created.Id, SaveAs("100"), _lawyer2.Id, "lawyer2"));
        Assert.Contains("لم يبق منظورًا", exDecided.Message);

        // المشطوب: مرفوض أيضًا.
        var second = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[1].Id) }),
            _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(second.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");
        await _service.StrikeAsync(second.Id,
            new StrikeAppealRequest("شطب-1", "1/10/2026"), _lawyer2.Id, "lawyer2");
        var exStruck = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveBaseNumbersAsync(second.Id, SaveAs("100"), _lawyer2.Id, "lawyer2"));
        Assert.Contains("لم يبق منظورًا", exStruck.Message);
    }

    [Fact]
    public async Task Rotation_WithoutAnyNumber_IsAllowedForAssignee()
    {
        // Request() بلا AppealBaseNumber/AppealYear — المسند المنظور بلا أي رقم يحتاج تدويرًا (R3).
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        var created = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");

        var before = await _service.GetAsync(created.Id);
        Assert.NotNull(before);
        Assert.True(before!.NeedsRotation);

        await _service.SaveBaseNumbersAsync(created.Id, SaveAs("100"), _lawyer2.Id, "lawyer2");

        var after = await _service.GetAsync(created.Id);
        Assert.NotNull(after);
        Assert.Equal("100", after!.CurrentBaseNumber);
        Assert.False(after.NeedsRotation);
    }

    [Fact]
    public async Task Actions_CrudAndReminders_WorkForAssignedLawyer()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        var created = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");

        var action = await _service.AddActionAsync(created.Id,
            new AddAppealActionRequest("action", "إيداع موجبات الاستئناف", "3/8/2026", "أسبوع", "أحمر"),
            _lawyer2.Id, "lawyer2");
        Assert.True(action.Id > 0);

        var updatedAction = await _service.UpdateActionAsync(created.Id, action.Id,
            new UpdateAppealActionRequest("action", "نص معدّل", "4/8/2026", "شهر", "أصفر"),
            _lawyer2.Id, "lawyer2");
        Assert.Equal("نص معدّل", updatedAction!.Text);
        Assert.Equal("أصفر", updatedAction!.ReminderColor);
        Assert.Equal("4/8/2026", updatedAction!.ActionDate);

        // تذكير يظهر للمتابع فقط.
        var reminders = await _service.GetRemindersAsync(_lawyer2.Id);
        Assert.Contains(reminders, r => r.ActionId == action.Id && r.AppealId == created.Id);
        var otherReminders = await _service.GetRemindersAsync(_lawyer1.Id);
        Assert.DoesNotContain(otherReminders, r => r.ActionId == action.Id);

        Assert.True(await _service.ClearReminderAsync(created.Id, action.Id, _lawyer2.Id, "lawyer2"));
        Assert.DoesNotContain(await _service.GetRemindersAsync(_lawyer2.Id), r => r.ActionId == action.Id);

        Assert.True(await _service.DeleteActionAsync(created.Id, action.Id, _lawyer2.Id, "lawyer2"));
        Assert.Empty(await _service.GetActionsAsync(created.Id));
    }

    private async Task<int> CreateAppealWithAssigneeAsync()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        var created = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");
        return created.Id;
    }

    [Theory]
    [InlineData("3/8/2026", "سنة", "أحمر")]
    [InlineData("3/8/2026", "أسبوع", "أخضر")]
    [InlineData("ليس تاريخا", "أسبوع", "أحمر")]
    public async Task Actions_InvalidReminderOrDate_Throws(string? date, string? duration, string? color)
    {
        // اتساقًا مع مسار إجراءات الملفات: لا مدد/ألوان/تواريخ ميتة تُخزَّن.
        var appealId = await CreateAppealWithAssigneeAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddActionAsync(appealId,
                new AddAppealActionRequest("action", "إجراء بتذكير ميت", date, duration, color),
                _lawyer2.Id, "lawyer2"));
    }

    [Fact]
    public async Task Actions_UpdateWithInvalidDuration_Throws()
    {
        var appealId = await CreateAppealWithAssigneeAsync();
        var action = await _service.AddActionAsync(appealId,
            new AddAppealActionRequest("action", "إجراء سليم", "3/8/2026", "أسبوع", "أحمر"),
            _lawyer2.Id, "lawyer2");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateActionAsync(appealId, action.Id,
                new UpdateAppealActionRequest("action", "إجراء سليم", "3/8/2026", "سنة", "أحمر"),
                _lawyer2.Id, "lawyer2"));
    }

    [Fact]
    public async Task Actions_InvalidActionType_ThrowsOnAddAndUpdate()
    {
        var appealId = await CreateAppealWithAssigneeAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddActionAsync(appealId,
                new AddAppealActionRequest("xyz", "إجراء بنوع ميت", "3/8/2026", null, null),
                _lawyer2.Id, "lawyer2"));

        var action = await _service.AddActionAsync(appealId,
            new AddAppealActionRequest("action", "إجراء سليم", "3/8/2026", null, null),
            _lawyer2.Id, "lawyer2");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateActionAsync(appealId, action.Id,
                new UpdateAppealActionRequest("xyz", "إجراء سليم", "3/8/2026", null, null),
                _lawyer2.Id, "lawyer2"));
    }

    [Fact]
    public async Task Actions_DateRequiredForAction_OptionalForNote()
    {
        // مرآة قاعدة إجراءات الملف: الإجراء بلا تاريخ مرفوض، والملاحظة بلا تاريخ مباحة.
        var appealId = await CreateAppealWithAssigneeAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddActionAsync(appealId,
                new AddAppealActionRequest("action", "إجراء بلا تاريخ", null, null, null),
                _lawyer2.Id, "lawyer2"));
        Assert.Contains("تاريخ الإجراء", ex.Message);

        var note = await _service.AddActionAsync(appealId,
            new AddAppealActionRequest("note", "ملاحظة بلا تاريخ", null, null, null),
            _lawyer2.Id, "lawyer2");
        Assert.Null(note.ActionDate);

        var action = await _service.AddActionAsync(appealId,
            new AddAppealActionRequest("action", "إجراء مؤرخ", "5/8/2026", null, null),
            _lawyer2.Id, "lawyer2");
        var exUpdate = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateActionAsync(appealId, action.Id,
                new UpdateAppealActionRequest("action", "إجراء مؤرخ", null, null, null),
                _lawyer2.Id, "lawyer2"));
        Assert.Contains("تاريخ الإجراء", exUpdate.Message);
    }

    [Fact]
    public async Task Transfer_IndividualAndAll_AreIndependentFromFiles()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();

        // استئنافان: الأول أُسند إلى lawyer2، والثاني لم يُسند بعد.
        var first = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(first.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");
        var second = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[1].Id) }),
            _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(second.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");

        // نقل فردي: من lawyer2 إلى lawyer1.
        var transferred = await _service.TransferAsync(first.Id,
            new TransferAppealRequest(_lawyer1.Id), _head1.Id, "head1");
        Assert.NotNull(transferred);
        Assert.Equal(_lawyer1.Id, transferred!.AssignedLawyerId);

        // نقل جملة ضمن الفرع: كل استئنافات lawyer2 تصير لـ lawyer1 (الاستئناف الثاني).
        var count = await _service.TransferAllAsync(
            new TransferAllAppealsRequest(_lawyer2.Id, _lawyer1.Id), _branch.Id, null, "head1");
        Assert.Equal(1, count);

        // نقل جملة من رئيس فرع آخر مرفوض (نطاق الفرع).
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.TransferAllAsync(new TransferAllAppealsRequest(_lawyer1.Id, _lawyer2.Id), _otherBranch.Id, null, "head2"));
    }

    [Fact]
    public async Task TransferAll_SkipsDecidedAndStruckOff()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        var first = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(first.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");
        var second = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[1].Id) }),
            _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(second.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");

        await _service.DecideAsync(first.Id,
            new DecideAppealRequest("قرار-1", "15/9/2026", "نص", AppealOutcomeCatalog.InFavor),
            _lawyer2.Id, "lawyer2");
        await _service.StrikeAsync(second.Id,
            new StrikeAppealRequest("شطب-1", "1/10/2026"), _lawyer2.Id, "lawyer2");

        // لا منظور قابل للنقل ← صفر، والمحسوم والمشطوب يبقيان عند المسند الأصلي (R5).
        var count = await _service.TransferAllAsync(
            new TransferAllAppealsRequest(_lawyer2.Id, _lawyer1.Id), _branch.Id, null, "head1");
        Assert.Equal(0, count);
        Assert.Equal(_lawyer2.Id, (await _service.GetAsync(first.Id))!.AssignedLawyerId);
        Assert.Equal(_lawyer2.Id, (await _service.GetAsync(second.Id))!.AssignedLawyerId);
    }

    [Fact]
    public async Task Create_WithOverlongText_ThrowsFriendlyError()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        var request = Request(AppealDirectionCatalog.Appellants,
            new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) })
            with { AppealedDecisionText = new string('م', 2001) };

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(doc.Id, request, _lawyer1.Id, "lawyer1"));
        Assert.Contains("يتجاوز الحد الأقصى", ex.Message);
    }

    [Fact]
    public async Task CountByAssigneeForHead_ScopesToBranch_AndRejectsBranchlessHead()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        var first = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");
        var second = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[1].Id) }),
            _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(first.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");

        Assert.Equal(1, await _service.CountByAssigneeForHeadAsync(_lawyer2.Id, _branch.Id, null));
        Assert.Equal(0, await _service.CountByAssigneeForHeadAsync(_lawyer1.Id, _branch.Id, null));

        // استئناف محسوم مسند لنفس المحامي لا يدخل العدّاد (المنظورة فقط — R5/C3).
        await _service.AssignAsync(second.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");
        await _service.DecideAsync(second.Id,
            new DecideAppealRequest("قرار-2", "15/9/2026", "نص", AppealOutcomeCatalog.InFavor),
            _lawyer2.Id, "lawyer2");
        Assert.Equal(1, await _service.CountByAssigneeForHeadAsync(_lawyer2.Id, _branch.Id, null));

        // رئيس قسم بلا فرع يُرفض بدل تسريب العدّادات.
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CountByAssigneeForHeadAsync(_lawyer2.Id, null, null));
        Assert.True(second.Id > 0);
    }

    [Fact]
    public async Task Search_ScopesAndTextMatching_WorkPerRole()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities
            .Where(e => e.DocumentId == doc.Id).OrderBy(e => e.Id).ToListAsync();
        var first = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(first.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");

        // بحث بالاسم المستأنف (من اللقطة).
        var byName = await _service.SearchAsync("المؤسسة العامة للكهرباء", null, null, null, null, 1, 20);
        Assert.Single(byName.Items);

        // نطاق المحامي المسند إليه يرى الاستئناف؛ والمنشئ غير المسند لا يراه (R6).
        var followerScope = await _service.SearchAsync(null, null, null, _lawyer2.Id, null, 1, 20);
        Assert.Single(followerScope.Items);
        var creatorScope = await _service.SearchAsync(null, null, null, _lawyer1.Id, null, 1, 20);
        Assert.Empty(creatorScope.Items);

        // فلتر الحالة.
        var pendingOnly = await _service.SearchAsync(null, AppealStatusCatalog.Pending, null, null, null, 1, 20);
        Assert.Single(pendingOnly.Items);
    }

    [Fact]
    public async Task AppealDto_DocumentEffectiveNumber_FollowsFileRotation()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities
            .Where(e => e.DocumentId == doc.Id).OrderBy(e => e.Id).ToListAsync();

        var dto = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");

        // بدون تدوير: الرقم الفعّال = رقم الملف الأصلي وسنته.
        Assert.Equal("520", dto.DocumentEffectiveNumber);
        Assert.Equal("2024", dto.DocumentEffectiveYear);

        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = doc.Id,
            Year = DateTime.Today.Year,
            BaseNumber = "1500",
            CreatedById = _lawyer1.Id,
        });
        await _db.SaveChangesAsync();

        var rotated = await _service.GetAsync(dto.Id);
        Assert.Equal("1500", rotated!.DocumentEffectiveNumber);
        Assert.Equal(DateTime.Today.Year.ToString(), rotated.DocumentEffectiveYear);
    }

    [Fact]
    public async Task AppealDto_CurrentBaseNumber_IgnoresFutureYears()
    {
        var doc = await CreateApplicantDocAsync();
        var entities = await _db.ApplicantPublicEntities
            .Where(e => e.DocumentId == doc.Id).OrderBy(e => e.Id).ToListAsync();

        var dto = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            _lawyer1.Id, "lawyer1");

        _db.AppealBaseNumbers.Add(new AppealBaseNumber
        {
            AppealId = dto.Id,
            Year = DateTime.Today.Year + 1,
            BaseNumber = "9999",
            CreatedById = _lawyer1.Id,
        });
        await _db.SaveChangesAsync();

        var list = await _service.GetAsync(dto.Id);

        // لا يوجد رقم أساس فعّال حتى الآن (المستقبلي لا يُحتسب) → لا يُعرض رقم مستقبلي أبدًا.
        Assert.Null(list!.CurrentBaseNumber);
    }

    private async Task<int> AddSectionAsync(string name, int branchId)
    {
        var section = new Section { BranchId = branchId, Name = name, NameNorm = name, IsActive = true };
        _db.Sections.Add(section);
        await _db.SaveChangesAsync();
        return section.Id;
    }

    private async Task<User> AddSubHeadAsync(string username, int branchId, int sectionId)
    {
        var user = User(branchId, username, username, UserRole.SubHead);
        user.SectionId = sectionId;
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<int> AddCircuitAsync(string name, int branchId, int? sectionId, int creatorId)
    {
        var circuit = new ExecutionCircuit
        {
            BranchId = branchId, SectionId = sectionId, Name = name, NameNorm = name,
            IsActive = true, CreatedById = creatorId,
        };
        _db.ExecutionCircuits.Add(circuit);
        await _db.SaveChangesAsync();
        return circuit.Id;
    }

    private async Task SetDocCircuitAsync(int docId, int? circuitId)
    {
        var doc = await _db.Documents.SingleAsync(d => d.Id == docId);
        doc.ExecutionCircuitId = circuitId;
        await _db.SaveChangesAsync();
    }

    private async Task<DocumentAppeal> CreatePendingAppealAsync(int docId, int? circuitId = null, int? creatorId = null)
    {
        if (circuitId.HasValue)
            await SetDocCircuitAsync(docId, circuitId);
        var doc = await _db.Documents.SingleAsync(d => d.Id == docId);
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        if (doc.GeneralEntitySide != GeneralEntitySideCatalog.Applicant || entities.Count == 0)
        {
            doc = await CreateApplicantDocAsync();
            if (circuitId.HasValue)
                await SetDocCircuitAsync(doc.Id, circuitId);
            entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();
        }
        var creator = creatorId ?? _lawyer1.Id;
        var dto = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) }),
            creator, "lawyer");
        return (await _db.DocumentAppeals.SingleAsync(a => a.Id == dto.Id))!;
    }

    private async Task SetDocNumberAsync(int docId, string number)
    {
        var doc = await _db.Documents.SingleAsync(d => d.Id == docId);
        doc.FileNumber = number;
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Forward_HappyPath_SetsStateAuditsAndRetargetsAlert()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var circuitId = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId, _head1.Id);
        var doc = await CreateApplicantDocAsync();
        var appeal = await CreatePendingAppealAsync(doc.Id, circuitId);

        var forwarded = await _service.ForwardAsync(appeal.Id, new ForwardAppealRequest("سبب"), sub.Id, "sub_masyaf");

        Assert.NotNull(forwarded);
        Assert.Equal(AppealForwardCatalog.ForwardedToHead, forwarded!.ForwardState);
        Assert.Equal(sectionId, forwarded.SectionId);
        Assert.Equal(circuitId, forwarded.ExecutionCircuitId);
        Assert.Contains("forward_appeal", _audit.Actions);
        var alerts = await _db.HeadAlerts.Include(a => a.Recipients).Where(a => a.AppealId == appeal.Id).ToListAsync();
        var headAlert = Assert.Single(alerts);
        Assert.Equal(_head1.Id, Assert.Single(headAlert.Recipients).UserId);
    }

    [Fact]
    public async Task Forward_ByHeadOrLawyer_Throws()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var circuitId = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId, _head1.Id);
        var doc = await CreateApplicantDocAsync();
        var appeal = await CreatePendingAppealAsync(doc.Id, circuitId);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ForwardAsync(appeal.Id, new ForwardAppealRequest(), _head1.Id, "head1"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ForwardAsync(appeal.Id, new ForwardAppealRequest(), _lawyer1.Id, "lawyer1"));
    }

    [Fact]
    public async Task Forward_WrongSectionOrAssignedOrDouble_Throws()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var otherId = await AddSectionAsync("شعبة أخرى", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var stranger = await AddSubHeadAsync("sub_other", _branch.Id, otherId);
        var circuitId = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId, _head1.Id);
        var doc = await CreateApplicantDocAsync();
        var appeal = await CreatePendingAppealAsync(doc.Id, circuitId);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ForwardAsync(appeal.Id, new ForwardAppealRequest(), stranger.Id, "stranger"));
        // الإسناد (بمالك النطاق) يغلق باب الإحالة.
        await _service.AssignAsync(appeal.Id, new AssignAppealRequest(_lawyer2.Id), sub.Id, "sub");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ForwardAsync(appeal.Id, new ForwardAppealRequest(), sub.Id, "sub"));
    }

    [Fact]
    public async Task Forward_NoDivisionHead_Throws()
    {
        var lonely = new Branch { Name = "بلا رئيس", Code = "NOR" };
        _db.Branches.Add(lonely);
        await _db.SaveChangesAsync();
        var sectionId = await AddSectionAsync("شعبة يتيمة", lonely.Id);
        var sub = await AddSubHeadAsync("sub_lonely", lonely.Id, sectionId);
        var lawyer = User(lonely.Id, "law_lonely", "محام يتيم");
        _db.Users.Add(lawyer);
        await _db.SaveChangesAsync();
        var doc = NewDoc(GeneralEntitySideCatalog.Applicant);
        doc.CreatedById = lawyer.Id;
        doc.BranchId = lonely.Id;
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _db.ApplicantPublicEntities.Add(new ApplicantPublicEntity { DocumentId = doc.Id, Name = "جهة" });
        await _db.SaveChangesAsync();
        var circuitId = await AddCircuitAsync("دائرة يتيمة", lonely.Id, sectionId, sub.Id);
        var appeal = await CreatePendingAppealAsync(doc.Id, circuitId, lawyer.Id);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ForwardAsync(appeal.Id, new ForwardAppealRequest(), sub.Id, "sub"));
    }

    [Fact]
    public async Task Recall_ByForwarderAndByHead_RestoresOwned()
    {
        foreach (var byHead in new[] { false, true })
        {
            var sectionId = await AddSectionAsync($"شعبة {Guid.NewGuid():N}"[..12], _branch.Id);
            var sub = await AddSubHeadAsync($"sub_{Guid.NewGuid():N}"[..12], _branch.Id, sectionId);
            var circuitId = await AddCircuitAsync($"دائرة {Guid.NewGuid():N}"[..12], _branch.Id, sectionId, _head1.Id);
            var doc = await CreateApplicantDocAsync();
            var appeal = await CreatePendingAppealAsync(doc.Id, circuitId);
            await _service.ForwardAsync(appeal.Id, new ForwardAppealRequest(), sub.Id, "sub");

            var callerId = byHead ? _head1.Id : sub.Id;
            var recalled = await _service.RecallForwardAsync(appeal.Id, callerId, byHead ? "head1" : "sub");

            Assert.NotNull(recalled);
            Assert.Equal(AppealForwardCatalog.Owned, recalled!.ForwardState);
            Assert.Contains("recall_forward", _audit.Actions);
        }
    }
    [Fact]
    public async Task Recall_RestoresSingleAlertForForwarder()
    {
        // التراجع بفرعيه (استرجاع المحيل / إعادة الرئيس) يجب أن يترك تنبيهًا
        // واحدًا للمحيل فقط — لا تنبيه يتيم لرئيس القسم.
        foreach (var byHead in new[] { false, true })
        {
            var sectionId = await AddSectionAsync($"شعبة {Guid.NewGuid():N}"[..12], _branch.Id);
            var sub = await AddSubHeadAsync($"sub_{Guid.NewGuid():N}"[..12], _branch.Id, sectionId);
            var circuitId = await AddCircuitAsync($"دائرة {Guid.NewGuid():N}"[..12], _branch.Id, sectionId, _head1.Id);
            var doc = await CreateApplicantDocAsync();
            var appeal = await CreatePendingAppealAsync(doc.Id, circuitId);
            await _service.ForwardAsync(appeal.Id, new ForwardAppealRequest(), sub.Id, "sub");

            var callerId = byHead ? _head1.Id : sub.Id;
            await _service.RecallForwardAsync(appeal.Id, callerId, byHead ? "head1" : "sub");

            var alerts = await _db.HeadAlerts.Include(a => a.Recipients)
                .Where(a => a.AppealId == appeal.Id).ToListAsync();
            var alert = Assert.Single(alerts);
            Assert.Equal(sub.Id, Assert.Single(alert.Recipients).UserId);
        }
    }

    [Fact]
    public async Task Decide_And_Strike_BumpVersion()
    {
        // كل تحوّل للحالة خارج «منظور» يرفع الرمز لإبطال الكتابات المتزامنة القديمة.
        var doc = await CreateApplicantDocAsync();
        await SetDocNumberAsync(doc.Id, "540");
        var appeal = await CreatePendingAppealAsync(doc.Id);
        Assert.Equal(1, (await _service.GetEntityAsync(appeal.Id))!.Version);
        await _service.AssignAsync(appeal.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");
        Assert.Equal(2, (await _service.GetEntityAsync(appeal.Id))!.Version);
        await _service.DecideAsync(appeal.Id,
            new DecideAppealRequest("قرار-1", "15/9/2026", "نص المنطوق", AppealOutcomeCatalog.InFavor),
            _lawyer2.Id, "lawyer2");
        Assert.Equal(3, (await _service.GetEntityAsync(appeal.Id))!.Version);

        var doc2 = await CreateApplicantDocAsync();
        await SetDocNumberAsync(doc2.Id, "541");
        var appeal2 = await CreatePendingAppealAsync(doc2.Id);
        await _service.AssignAsync(appeal2.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");
        await _service.StrikeAsync(appeal2.Id,
            new StrikeAppealRequest("قرار-2", "16/9/2026"),
            _lawyer2.Id, "lawyer2");
        Assert.Equal(3, (await _service.GetEntityAsync(appeal2.Id))!.Version);
    }

    [Fact]
    public async Task Recall_ByStrangerOrAfterAssign_Throws()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var circuitId = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId, _head1.Id);
        var doc = await CreateApplicantDocAsync();
        var appeal = await CreatePendingAppealAsync(doc.Id, circuitId);
        await _service.ForwardAsync(appeal.Id, new ForwardAppealRequest(), sub.Id, "sub");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RecallForwardAsync(appeal.Id, _lawyer2.Id, "lawyer2"));

        await _service.AssignAsync(appeal.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RecallForwardAsync(appeal.Id, sub.Id, "sub"));
    }

    [Fact]
    public async Task Assign_ForwardedAppeal_ByHead_Succeeds_WithException()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var circuitId = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId, _head1.Id);
        var doc = await CreateApplicantDocAsync();
        var appeal = await CreatePendingAppealAsync(doc.Id, circuitId);
        await _service.ForwardAsync(appeal.Id, new ForwardAppealRequest(), sub.Id, "sub");

        var assigned = await _service.AssignAsync(appeal.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");

        Assert.NotNull(assigned);
        Assert.Equal(_lawyer2.Id, assigned!.AssignedLawyerId);
        Assert.Equal(AppealForwardCatalog.ForwardedToHead, assigned.ForwardState);
    }

    [Fact]
    public async Task Assign_SectionFile_ByHead_WithoutForward_ThrowsOutOfScope()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var circuitId = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId, _head1.Id);
        var doc = await CreateApplicantDocAsync();
        var appeal = await CreatePendingAppealAsync(doc.Id, circuitId);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AssignAsync(appeal.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1"));
        Assert.Contains("ليست ضمن نطاقك", ex.Message);
    }

    [Fact]
    public async Task Assign_BySubHead_OwnSection_Succeeds_Forwarded_Throws()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var circuitId = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId, _head1.Id);
        var doc = await CreateApplicantDocAsync();
        var appeal = await CreatePendingAppealAsync(doc.Id, circuitId);

        var assigned = await _service.AssignAsync(appeal.Id, new AssignAppealRequest(_lawyer2.Id), sub.Id, "sub");
        Assert.NotNull(assigned);

        var doc2 = await CreateApplicantDocAsync();
        await SetDocNumberAsync(doc2.Id, "521");
        var appeal2 = await CreatePendingAppealAsync(doc2.Id, circuitId);
        await _service.ForwardAsync(appeal2.Id, new ForwardAppealRequest(), sub.Id, "sub");
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AssignAsync(appeal2.Id, new AssignAppealRequest(_lawyer2.Id), sub.Id, "sub"));
    }

    [Fact]
    public async Task Assign_StaleVersion_Conflict()
    {
        var doc = await CreateApplicantDocAsync();
        var appeal = await CreatePendingAppealAsync(doc.Id);

        // نسخة قديمة صراحةً → `409` ودية بالفحص المسبق (السباق الحقيقي يصطاده الرمز).
        await Assert.ThrowsAsync<DocumentConflictException>(() =>
            _service.AssignAsync(appeal.Id, new AssignAppealRequest(_lawyer2.Id, 9999), _head1.Id, "head1"));
    }

    [Fact]
    public async Task TransferAll_Intersection_ByScope()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var divisionCircuit = await AddCircuitAsync("دائرة القسم", _branch.Id, null, _head1.Id);
        var sectionCircuit = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId, _head1.Id);
        var docA = await CreateApplicantDocAsync();
        await SetDocCircuitAsync(docA.Id, divisionCircuit);
        var docB = await CreateApplicantDocAsync();
        await SetDocCircuitAsync(docB.Id, sectionCircuit);
        var appealA = await CreatePendingAppealAsync(docA.Id);
        var appealB = await CreatePendingAppealAsync(docB.Id);
        await _service.AssignAsync(appealA.Id, new AssignAppealRequest(_lawyer1.Id), _head1.Id, "head1");
        await _service.AssignAsync(appealB.Id, new AssignAppealRequest(_lawyer1.Id), sub.Id, "sub");

        var subCount = await _service.TransferAllAsync(
            new TransferAllAppealsRequest(_lawyer1.Id, _lawyer2.Id), _branch.Id, sectionId, "sub");
        Assert.Equal(1, subCount);
        var headCount = await _service.TransferAllAsync(
            new TransferAllAppealsRequest(_lawyer1.Id, _lawyer2.Id), _branch.Id, null, "head1");
        Assert.Equal(1, headCount);
        Assert.Equal(0, await _service.CountByAssigneeForHeadAsync(_lawyer1.Id, _branch.Id, sectionId));
    }

    [Fact]
    public async Task NotifyHeadPending_TargetedToOwnerOnly()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var circuitId = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId, _head1.Id);
        var doc = await CreateApplicantDocAsync();
        await CreatePendingAppealAsync(doc.Id, circuitId);

        var alerts = await _db.HeadAlerts.Include(a => a.Recipients).Where(a => a.AppealId != null).ToListAsync();
        var creation = Assert.Single(alerts);
        Assert.Equal(sub.Id, Assert.Single(creation.Recipients).UserId);
    }

    [Fact]
    public async Task Search_HeadExcludesSection_IncludesForwardedUntilDecided()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var divisionCircuit = await AddCircuitAsync("دائرة القسم", _branch.Id, null, _head1.Id);
        var sectionCircuit = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId, _head1.Id);
        var docDivision = await CreateApplicantDocAsync();
        await SetDocNumberAsync(docDivision.Id, "530");
        var docSection = await CreateApplicantDocAsync();
        await SetDocNumberAsync(docSection.Id, "531");
        var appealDivision = await CreatePendingAppealAsync(docDivision.Id, divisionCircuit);
        var appealSection = await CreatePendingAppealAsync(docSection.Id, sectionCircuit);

        // العزل: القسم يرى قسمه دون الشعبة، والشعبة ترى شعبته دون القسم.
        var headResults = await _service.SearchAsync(null, null, _branch.Id, null, null, 1, 20);
        Assert.Contains(headResults.Items, a => a.Id == appealDivision.Id);
        Assert.DoesNotContain(headResults.Items, a => a.Id == appealSection.Id);
        var subResults = await _service.SearchAsync(null, null, _branch.Id, null, sectionId, 1, 20);
        Assert.Contains(subResults.Items, a => a.Id == appealSection.Id);
        Assert.DoesNotContain(subResults.Items, a => a.Id == appealDivision.Id);

        // الاستثناء القرائي (22′): المحال المنظور يظهر للقسم، وبعد الحسم يختفي.
        await _service.ForwardAsync(appealSection.Id, new ForwardAppealRequest(), sub.Id, "sub");
        headResults = await _service.SearchAsync(null, null, _branch.Id, null, null, 1, 20);
        Assert.Contains(headResults.Items, a => a.Id == appealSection.Id);

        await _service.AssignAsync(appealSection.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");
        await _service.DecideAsync(appealSection.Id,
            new DecideAppealRequest("قرار-1", "15/9/2026", "نص المنطوق", AppealOutcomeCatalog.InFavor),
            _lawyer2.Id, "lawyer2");
        headResults = await _service.SearchAsync(null, null, _branch.Id, null, null, 1, 20);
        Assert.DoesNotContain(headResults.Items, a => a.Id == appealSection.Id);
        // مالك النطاق (الشعبة) يبقى يرى ملفه بعد الحسم.
        subResults = await _service.SearchAsync(null, null, _branch.Id, null, sectionId, 1, 20);
        Assert.Contains(subResults.Items, a => a.Id == appealSection.Id);
    }

    [Fact]
    public async Task TransferAll_IncludesForwardedPendingAppeal_ForHead()
    {
        // F2: المحال المنظور المسند يبقى في نطاق القسم للنقل الجملي (قرار §2.22) —
        // مرآة استثناء البحث، والمشترك الوحيد `InScope` يغطي العدّ والنقل معًا.
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var sectionCircuit = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId, _head1.Id);
        var doc = await CreateApplicantDocAsync();
        await SetDocCircuitAsync(doc.Id, sectionCircuit);
        var appeal = await CreatePendingAppealAsync(doc.Id);
        await _service.ForwardAsync(appeal.Id, new ForwardAppealRequest(), sub.Id, "sub");
        await _service.AssignAsync(appeal.Id, new AssignAppealRequest(_lawyer1.Id), _head1.Id, "head1");

        Assert.Equal(1, await _service.CountByAssigneeForHeadAsync(_lawyer1.Id, _branch.Id, null));

        var moved = await _service.TransferAllAsync(
            new TransferAllAppealsRequest(_lawyer1.Id, _lawyer2.Id), _branch.Id, null, "head1");
        Assert.Equal(1, moved);
        Assert.Equal(_lawyer2.Id, (await _service.GetAsync(appeal.Id))!.AssignedLawyerId);
    }

    [Fact]
    public async Task TransferAll_ExcludesDecidedForwardedAppeal_SubScopeUnchanged()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var sectionCircuit = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId, _head1.Id);
        var doc = await CreateApplicantDocAsync();
        await SetDocCircuitAsync(doc.Id, sectionCircuit);
        var appeal = await CreatePendingAppealAsync(doc.Id);
        await _service.ForwardAsync(appeal.Id, new ForwardAppealRequest(), sub.Id, "sub");
        await _service.AssignAsync(appeal.Id, new AssignAppealRequest(_lawyer1.Id), _head1.Id, "head1");
        await _service.DecideAsync(appeal.Id,
            new DecideAppealRequest("قرار-1", "15/9/2026", "نص المنطوق", AppealOutcomeCatalog.InFavor),
            _lawyer1.Id, "lawyer1");

        // المحسوم مستبعد من العدّ والنقل ولو كان محالًا (قيد `Pending` الأصلي).
        Assert.Equal(0, await _service.CountByAssigneeForHeadAsync(_lawyer1.Id, _branch.Id, null));
        var moved = await _service.TransferAllAsync(
            new TransferAllAppealsRequest(_lawyer1.Id, _lawyer2.Id), _branch.Id, null, "head1");
        Assert.Equal(0, moved);

        // فرع الشعبة بلا تغيير: المحال خارج شعبته لا يدخل نطاقها.
        var otherSection = await AddSectionAsync("شعبة أخرى", _branch.Id);
        Assert.Equal(0, await _service.CountByAssigneeForHeadAsync(_lawyer1.Id, _branch.Id, otherSection));
    }
}
