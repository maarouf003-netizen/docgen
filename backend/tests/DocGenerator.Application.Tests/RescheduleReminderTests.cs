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
/// نقل تذكير إجراء إلى يوم استحقاق جديد من نافذة اليوم (`RescheduleReminderAsync`
/// للملفات و`RescheduleAppealReminderAsync` للاستئنافات): المدخل `dueDate` نفسه،
/// وتُعاد حساب `ActionDate` خلفيًا مع بقاء المدة — بلا مساس ببقية الحقول.
/// </summary>
public class RescheduleDocumentReminderTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IDocumentService _service;
    private readonly FakeAuditLogger _audit = new();

    public RescheduleDocumentReminderTests()
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
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        _service = new DocumentService(
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
            new HeadAlertService(new HeadAlertRepository(_db), new DocumentRepository(_db), new UserRepository(_db), new Repository<Branch>(_db), uow, tx, _audit),
            uow, tx, _audit,
            Microsoft.Extensions.Options.Options.Create(new ExportOptions()),
            TimeProvider.System, TestClock.TimeZone, new DbExceptionClassifier());
    }

    public void Dispose() => _db.Dispose();

    private static int s_sampleSeq;

    private static DocumentUpsertRequest Sample() => new()
    {
        BorrowerName = "أحمد",
        BorrowerFather = "خالد",
        BorrowerFamily = "الخطيب",
        Court = "دمشق",
        FileNumber = $"521{System.Threading.Interlocked.Increment(ref s_sampleSeq):D4}",
        FileYear = "2024",
        FileRegistrationDate = "1/1/2024",
    };

    private async Task<(int DocumentId, ExecutionActionDto Action)> SeedActionAsync(
        string? actionDate = "1/8/2026",
        string? duration = "3 أيام",
        string? color = "أحمر",
        string type = "action")
    {
        var doc = await _service.CreateAsync(Sample(), userId: 1, actorName: "lawyer1", branchId: 1);
        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest
            {
                Type = type,
                Text = "إجراء",
                ActionDate = actionDate,
                ReminderDuration = duration,
                ReminderColor = color,
            },
            userId: 1, actorName: "lawyer1");
        return (doc.Id, action);
    }

    [Fact]
    public async Task Reschedule_MovesDueDateKeepingDuration()
    {
        var (documentId, action) = await SeedActionAsync();

        var rescheduled = await _service.RescheduleReminderAsync(documentId, action.Id, "10/8/2026", "lawyer1");

        Assert.Equal("2026-08-07", rescheduled.ActionDate);
        var (due, suspect) = ActionReminderCalculator.TryComputeDueDate(
            rescheduled.ActionDate, rescheduled.ReminderDuration, rescheduled.CreatedAt);
        Assert.Equal(new DateTime(2026, 8, 10), due);
        Assert.False(suspect);
        // بقية الحقول دون مساس.
        Assert.Equal(action.Type, rescheduled.Type);
        Assert.Equal(action.Text, rescheduled.Text);
        Assert.Equal("3 أيام", rescheduled.ReminderDuration);
        Assert.Equal("أحمر", rescheduled.ReminderColor);
        Assert.Equal(action.CreatedAt, rescheduled.CreatedAt);
        // التدقيق يوثق الوجهة والتاريخين (قديم ← جديد).
        Assert.Contains("action", _audit.Actions);
        Assert.Contains(_audit.Details,
            d => d != null && d.Contains("نقل التذكير إلى 2026-08-10") && d.Contains("2026-08-07"));
    }

    [Fact]
    public async Task Reschedule_ArabicDigits_Accepted()
    {
        var (documentId, action) = await SeedActionAsync();

        var rescheduled = await _service.RescheduleReminderAsync(documentId, action.Id, "١٠/٨/٢٠٢٦", "lawyer1");

        Assert.Equal("2026-08-07", rescheduled.ActionDate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reschedule_Empty_ThrowsRequired(string? newDueDate)
    {
        var (documentId, action) = await SeedActionAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RescheduleReminderAsync(documentId, action.Id, newDueDate, "lawyer1"));
        Assert.Equal("اليوم الجديد مطلوب — استخدم مثال: 1/8/2026", ex.Message);
    }

    [Theory]
    [InlineData("31/2/2026")]
    [InlineData("ليس تاريخًا")]
    public async Task Reschedule_Invalid_ThrowsFriendly(string newDueDate)
    {
        var (documentId, action) = await SeedActionAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RescheduleReminderAsync(documentId, action.Id, newDueDate, "lawyer1"));
        Assert.Equal("اليوم الجديد غير صالح — استخدم مثال: 1/8/2026", ex.Message);
    }

    [Fact]
    public async Task Reschedule_NoReminder_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), userId: 1, actorName: "lawyer1", branchId: 1);
        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Type = "note", Text = "ملاحظة بلا تذكير" },
            userId: 1, actorName: "lawyer1");

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RescheduleReminderAsync(doc.Id, action.Id, "10/8/2026", "lawyer1"));
        Assert.Equal("لا يوجد تذكير لإعادة جدولته", ex.Message);
    }

    [Fact]
    public async Task Reschedule_UnknownLegacyDuration_ThrowsInsteadOfSilencingSuspect()
    {
        // مدة قديمة خارج الأربع لا يمكن زرعها عبر الخدمة (مرفوضة كتابةً) — تُزرع مباشرة.
        var doc = await _service.CreateAsync(Sample(), userId: 1, actorName: "lawyer1", branchId: 1);
        var legacy = new ExecutionAction
        {
            DocumentId = doc.Id,
            Type = "action",
            Text = "إجراء بمدة قديمة",
            ActionDate = "1/8/2026",
            ReminderDuration = "سنة",
            ReminderColor = "أحمر",
            CreatedById = 1,
            CreatedAt = DateTime.UtcNow,
        };
        _db.ExecutionActions.Add(legacy);
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RescheduleReminderAsync(doc.Id, legacy.Id, "10/8/2026", "lawyer1"));
        Assert.Equal("مدة التذكير غير معروفة — عدّل الإجراء من نافذة الإجراءات", ex.Message);
        // التاريخ الأصلي لم يُمسّ.
        Assert.Equal("1/8/2026", (await _db.ExecutionActions.FindAsync(legacy.Id))!.ActionDate);
    }

    [Fact]
    public async Task Reschedule_ColorOnly_SetsActionDateToDueDate()
    {
        var (documentId, action) = await SeedActionAsync(duration: null, color: "أصفر");

        var rescheduled = await _service.RescheduleReminderAsync(documentId, action.Id, "10/8/2026", "lawyer1");

        Assert.Equal("2026-08-10", rescheduled.ActionDate);
    }

    [Fact]
    public async Task Reschedule_NoteWithoutDate_GetsExplicitDate()
    {
        var (documentId, action) = await SeedActionAsync(type: "note", actionDate: null, duration: "أسبوع");

        var rescheduled = await _service.RescheduleReminderAsync(documentId, action.Id, "15/8/2026", "lawyer1");

        Assert.Equal("2026-08-08", rescheduled.ActionDate);
        Assert.Equal("note", rescheduled.Type);
    }

    [Fact]
    public async Task Reschedule_MissingActionOrMismatchedParent_ThrowsKeyNotFound()
    {
        var (documentId, action) = await SeedActionAsync();
        var other = await _service.CreateAsync(Sample(), userId: 1, actorName: "lawyer1", branchId: 1);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.RescheduleReminderAsync(documentId, 999999, "10/8/2026", "lawyer1"));
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.RescheduleReminderAsync(other.Id, action.Id, "10/8/2026", "lawyer1"));
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.RescheduleReminderAsync(999999, action.Id, "10/8/2026", "lawyer1"));
    }
}

public class RescheduleAppealReminderTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IDocumentAppealService _service;
    private readonly FakeAuditLogger _audit = new();

    private readonly Branch _branch;
    private readonly User _lawyer1;
    private readonly User _lawyer2;
    private readonly User _head1;

    public RescheduleAppealReminderTests()
    {
        _db = TestDb.Create();

        _branch = new Branch { Name = "دمشق", Code = "DAM" };
        _db.Branches.Add(_branch);
        _db.SaveChanges();

        _lawyer1 = User(_branch.Id, "lawyer1", "محامي دمشق");
        _lawyer2 = User(_branch.Id, "lawyer2", "محامي دمشق ثانٍ");
        _head1 = User(_branch.Id, "head1", "رئيس قسم دمشق", UserRole.Head);
        _db.Users.AddRange(_lawyer1, _lawyer2, _head1);
        _db.SaveChanges();

        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        var alerts = new HeadAlertService(
            new HeadAlertRepository(_db),
            new DocumentRepository(_db),
            new UserRepository(_db),
            new Repository<Branch>(_db),
            uow, tx, _audit);

        _service = new DocumentAppealService(
            new AppealRepository(_db),
            new DocumentRepository(_db),
            new UserRepository(_db),
            uow, tx, _audit, alerts,
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

    private async Task<int> CreateAppealWithAssigneeAsync()
    {
        var doc = new Document
        {
            CreatedById = _lawyer1.Id,
            BranchId = _branch.Id,
            BranchName = _branch.Name,
            GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
            IsDraft = false,
            Court = "دمشق",
            FileNumber = "520",
            FileType = "حقوق",
            FileYear = "2024",
            DocumentType = "متداول - أحمد خالد الخطيب",
            BorrowerName = "أحمد",
            BorrowerFather = "خالد",
            BorrowerFamily = "الخطيب",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _db.ApplicantPublicEntities.AddRange(
            new ApplicantPublicEntity { DocumentId = doc.Id, Name = "المؤسسة العامة للكهرباء" },
            new ApplicantPublicEntity { DocumentId = doc.Id, Name = "مديرية الموارد المائية" });
        await _db.SaveChangesAsync();
        var entities = await _db.ApplicantPublicEntities.Where(e => e.DocumentId == doc.Id).ToListAsync();

        var created = await _service.CreateAsync(doc.Id,
            new UpsertAppealRequest(
                Direction: AppealDirectionCatalog.Appellants,
                Appellants: new List<AppealPartySelectionDto> { new("applicant-entity", entities[0].Id) },
                AppealTypeLabel: "عادي",
                AppealedDecisionText: "نص القرار المستأنف",
                AppealedDecisionSummary: "ملخص القرار",
                AppealedDecisionDate: "1/8/2026",
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
                Notes: null),
            _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(created.Id, new AssignAppealRequest(_lawyer2.Id), _head1.Id, "head1");
        return created.Id;
    }

    [Fact]
    public async Task Reschedule_MovesDueDateKeepingDuration()
    {
        var appealId = await CreateAppealWithAssigneeAsync();
        var action = await _service.AddActionAsync(appealId,
            new AddAppealActionRequest("action", "إجراء", "1/8/2026", "أسبوع", "أحمر"),
            _lawyer2.Id, "lawyer2");

        var rescheduled = await _service.RescheduleAppealReminderAsync(
            appealId, action.Id, "15/8/2026", _lawyer2.Id, "lawyer2");

        Assert.Equal("2026-08-08", rescheduled.ActionDate);
        var (due, suspect) = ActionReminderCalculator.TryComputeDueDate(
            rescheduled.ActionDate, rescheduled.ReminderDuration, rescheduled.CreatedAt);
        Assert.Equal(new DateTime(2026, 8, 15), due);
        Assert.False(suspect);
        Assert.Equal("action", rescheduled.Type);
        Assert.Equal("إجراء", rescheduled.Text);
        Assert.Equal("أسبوع", rescheduled.ReminderDuration);
        Assert.Equal("أحمر", rescheduled.ReminderColor);
        Assert.Contains("reschedule_appeal_reminder", _audit.Actions);
        Assert.Contains(_audit.Details,
            d => d != null && d.Contains("نقل تذكير إجراء على الاستئناف") && d.Contains("2026-08-08"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("31/2/2026")]
    public async Task Reschedule_EmptyOrInvalid_Throws(string? newDueDate)
    {
        var appealId = await CreateAppealWithAssigneeAsync();
        var action = await _service.AddActionAsync(appealId,
            new AddAppealActionRequest("action", "إجراء", "1/8/2026", "أسبوع", "أحمر"),
            _lawyer2.Id, "lawyer2");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RescheduleAppealReminderAsync(appealId, action.Id, newDueDate, _lawyer2.Id, "lawyer2"));
        Assert.Contains("اليوم الجديد", ex.Message);
    }

    [Fact]
    public async Task Reschedule_NoReminder_Throws()
    {
        var appealId = await CreateAppealWithAssigneeAsync();
        var note = await _service.AddActionAsync(appealId,
            new AddAppealActionRequest("note", "ملاحظة بلا تذكير", null, null, null),
            _lawyer2.Id, "lawyer2");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RescheduleAppealReminderAsync(appealId, note.Id, "10/8/2026", _lawyer2.Id, "lawyer2"));
        Assert.Equal("لا يوجد تذكير لإعادة جدولته", ex.Message);
    }

    [Fact]
    public async Task Reschedule_NonFollower_Throws()
    {
        var appealId = await CreateAppealWithAssigneeAsync();
        var action = await _service.AddActionAsync(appealId,
            new AddAppealActionRequest("action", "إجراء", "1/8/2026", "أسبوع", "أحمر"),
            _lawyer2.Id, "lawyer2");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RescheduleAppealReminderAsync(appealId, action.Id, "15/8/2026", _lawyer1.Id, "lawyer1"));
        Assert.Contains("ليس مسندًا إليك", ex.Message);
    }

    [Fact]
    public async Task Reschedule_MissingAppealOrAction_Throws()
    {
        var appealId = await CreateAppealWithAssigneeAsync();
        var action = await _service.AddActionAsync(appealId,
            new AddAppealActionRequest("action", "إجراء", "1/8/2026", "أسبوع", "أحمر"),
            _lawyer2.Id, "lawyer2");

        var missingAppeal = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RescheduleAppealReminderAsync(999999, action.Id, "15/8/2026", _lawyer2.Id, "lawyer2"));
        Assert.Equal("الاستئناف غير موجود", missingAppeal.Message);

        var missingAction = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RescheduleAppealReminderAsync(appealId, 999999, "15/8/2026", _lawyer2.Id, "lawyer2"));
        Assert.Equal("الإجراء غير موجود", missingAction.Message);
    }
}
