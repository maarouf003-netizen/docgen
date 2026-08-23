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
            _alertService);
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

        var headAlerts = await _alertService.ListForHeadAsync(_branch.Id);
        Assert.Contains(headAlerts, a => a.TargetType == "head" && a.Message.Contains("يرجى اختيار محامي"));
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
        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1");

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
            _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head2.Id, _otherBranch.Id, "head2"));
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

        var assigned = await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1");

        Assert.NotNull(assigned);
        Assert.Equal(_lawyer2.Id, assigned!.AssignedLawyerId);
        // تنبيه الرئيس المعلّق صُفّي، ووصل المحامي تنبيه الإحالة.
        var headAlerts = await _alertService.ListForHeadAsync(_branch.Id);
        Assert.DoesNotContain(headAlerts, a => a.Message.Contains("يرجى اختيار محامي"));
        var lawyerAlerts = await _alertService.ListForLawyerAsync(_lawyer2.Id);
        Assert.Contains(lawyerAlerts, a => a.Message.Contains("أحال إليك رئيس القسم استئناف"));

        // إسناد مكرر مرفوض.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1"));
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
        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1");

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
        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1");

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
        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1");

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
        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1");

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
        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1");

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
        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1");

        var action = await _service.AddActionAsync(created.Id,
            new AddAppealActionRequest("action", "إيداع موجبات الاستئناف", "3/8/2026", "أسبوع", "أحمر"),
            _lawyer2.Id, "lawyer2");
        Assert.True(action.Id > 0);

        var updatedAction = await _service.UpdateActionAsync(created.Id, action.Id,
            new UpdateAppealActionRequest("action", "نص معدّل", null, "شهر", "أخضر"),
            _lawyer2.Id, "lawyer2");
        Assert.Equal("نص معدّل", updatedAction!.Text);

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
        await _service.AssignAsync(first.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1");
        var second = await _service.CreateAsync(doc.Id,
            Request(AppealDirectionCatalog.Appellants,
                new List<AppealPartySelectionDto> { new("applicant-entity", entities[1].Id) }),
            _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(second.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1");

        // نقل فردي: من lawyer2 إلى lawyer1.
        var transferred = await _service.TransferAsync(first.Id,
            new TransferAppealRequest(_lawyer1.Id), _head1.Id, _branch.Id, "head1");
        Assert.NotNull(transferred);
        Assert.Equal(_lawyer1.Id, transferred!.AssignedLawyerId);

        // نقل جملة ضمن الفرع: كل استئنافات lawyer2 تصير لـ lawyer1 (الاستئناف الثاني).
        var count = await _service.TransferAllAsync(
            new TransferAllAppealsRequest(_lawyer2.Id, _lawyer1.Id), _branch.Id, "head1");
        Assert.Equal(1, count);

        // نقل جملة من رئيس فرع آخر مرفوض (نطاق الفرع).
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.TransferAllAsync(new TransferAllAppealsRequest(_lawyer1.Id, _lawyer2.Id), _otherBranch.Id, "head2"));
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
        await _service.AssignAsync(first.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1");

        Assert.Equal(1, await _service.CountByAssigneeForHeadAsync(_lawyer2.Id, _branch.Id));
        Assert.Equal(0, await _service.CountByAssigneeForHeadAsync(_lawyer1.Id, _branch.Id));

        // رئيس قسم بلا فرع يُرفض بدل تسريب العدّادات.
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CountByAssigneeForHeadAsync(_lawyer2.Id, null));
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
        await _service.AssignAsync(first.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1");

        // بحث بالاسم المستأنف (من اللقطة).
        var byName = await _service.SearchAsync("المؤسسة العامة للكهرباء", null, null, null, 1, 20);
        Assert.Single(byName.Items);

        // نطاق المحامي المسند إليه يرى الاستئناف، والمنشئ أيضًا.
        var followerScope = await _service.SearchAsync(null, null, null, _lawyer2.Id, 1, 20);
        Assert.Single(followerScope.Items);
        var creatorScope = await _service.SearchAsync(null, null, null, _lawyer1.Id, 1, 20);
        Assert.Single(creatorScope.Items);

        // فلتر الحالة.
        var pendingOnly = await _service.SearchAsync(null, AppealStatusCatalog.Pending, null, null, 1, 20);
        Assert.Single(pendingOnly.Items);
    }
}
