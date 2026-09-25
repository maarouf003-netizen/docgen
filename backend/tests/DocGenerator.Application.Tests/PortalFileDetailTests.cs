using System.IO.Compression;
using System.Text;
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
/// تفاصيل ملف البوابة القرائية (المهمة الحالية): الإجراءات التنفيذية (نوع action فقط،
/// الأحدث أولًا، باسم المُسجِّل)، تشعبات الملف، تفاصيل الاستئنافات مع إخفاء رأي المحامي
/// (ق10) وإبقاء اسمه (ق9)، وتاريخ أرقام الأساس — وعزل النطاق (404) لكل نقطة، وعدم
/// تسريب الملاحظات الداخلية إلى عمود «الإجراءات والملاحظات» في تصدير Excel (ف12).
/// </summary>
public class PortalFileDetailTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly FakeAuditLogger _audit = new();
    private readonly IPortalService _portal;
    private readonly IDocumentAppealService _appealService;
    private readonly int _entryAId;
    private readonly int _delegateGroupId;
    private readonly int _lawyerId;

    public PortalFileDetailTests()
    {
        _db = TestDb.Create();

        _db.Users.Add(new User { Username = "creator", FullName = "منشئ", Role = UserRole.Admin, PasswordHash = "x" });
        _db.SaveChanges();

        var lawyer = new User { Username = "lawyer1", FullName = "محامي دمشق", Role = UserRole.Lawyer, PasswordHash = "x" };
        _db.Users.Add(lawyer);
        _db.SaveChanges();
        _lawyerId = lawyer.Id;

        var groupA = new PublicEntityGroup { CanonicalName = "وزارة التعليم", EntityType = PublicEntityTypeCatalog.Ministry };
        groupA.Entries.Add(new PublicEntity { Governorate = "دمشق", BranchName = "الفرع الرئيسي", Status = EntityStatusCatalog.Final, CreatedById = 1 });
        _db.PublicEntityGroups.Add(groupA);
        _db.SaveChanges();
        _entryAId = groupA.Entries.First().Id;

        var delegateGroup = new User { Username = "delegate_group", FullName = "مندوب الوزارة", Role = UserRole.EntityManager, PortalGroupId = groupA.Id, PasswordHash = "x" };
        _db.Users.Add(delegateGroup);
        _db.SaveChanges();
        _delegateGroupId = delegateGroup.Id;

        _portal = PortalServiceFactory.Create(_db, _audit);

        var documents = new DocumentRepository(_db);
        var users = new UserRepository(_db);
        var branches = new Repository<Branch>(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        var headAlerts = new HeadAlertService(
            new HeadAlertRepository(_db), documents, users, branches, uow, tx, _audit);
        _appealService = new DocumentAppealService(
            new AppealRepository(_db), documents, users, uow, tx, _audit, headAlerts,
            TimeProvider.System, TestClock.TimeZone);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>ملف «طالبة تنفيذ» مرتبط بقيد من نطاق المندوب ومملوك للمحامي.</summary>
    private async Task<Document> SeedInScopeDocAsync(string name)
    {
        var doc = new Document
        {
            CreatedById = _lawyerId,
            IsDraft = false,
            BorrowerName = name,
            BorrowerFather = "خالد",
            BorrowerFamily = "الخطيب",
            AmountNumeric = 100,
            ExecStatus = string.Empty,
            GeneralEntitySide = "applicant",
        };
        doc.ApplicantPublicEntities.Add(new ApplicantPublicEntity { Name = name, Governorate = "دمشق", RegistryId = _entryAId });
        doc.ApplicantRegistryId = _entryAId;
        doc.Applicant = $"{name} - محافظة دمشق";
        doc.SearchText = DocumentSearchTextBuilder.Build(doc);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        return doc;
    }

    /// <summary>ملف بلا أي رابط هوية — خارج نطاق أي مندوب.</summary>
    private async Task<Document> SeedOutOfScopeDocAsync(string name)
    {
        var doc = new Document
        {
            CreatedById = _lawyerId,
            IsDraft = false,
            BorrowerName = name,
            AmountNumeric = 100,
            ExecStatus = string.Empty,
            GeneralEntitySide = "applicant",
        };
        doc.SearchText = DocumentSearchTextBuilder.Build(doc);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        return doc;
    }

    private async Task AddExecutionActionAsync(int documentId, string type, string text, DateTime createdAt, string? actionDate = null)
    {
        _db.ExecutionActions.Add(new ExecutionAction
        {
            DocumentId = documentId,
            Type = type,
            Text = text,
            ActionDate = actionDate,
            CreatedById = _lawyerId,
            CreatedAt = createdAt,
        });
        await _db.SaveChangesAsync();
    }

    private async Task AddBaseNumbersAsync(int documentId, params (int Year, string Number, DateTime CreatedAt)[] rows)
    {
        foreach (var (year, number, createdAt) in rows)
            _db.BaseNumbers.Add(new DocumentBaseNumber
            {
                DocumentId = documentId,
                Year = year,
                BaseNumber = number,
                CreatedById = _lawyerId,
                CreatedAt = createdAt,
            });
        await _db.SaveChangesAsync();
    }

    // ── الإجراءات التنفيذية ──────────────────────────────────────────────

    [Fact]
    public async Task ListExecutionActions_ReturnsOnlyActions_NewestFirst_WithCreatorName()
    {
        var doc = await SeedInScopeDocAsync("ملف الإجراءات");
        // ملاحظة أحدث حتى من الإجراءات — يجب ألا تظهر (ق7).
        await AddExecutionActionAsync(doc.Id, "note", "ملاحظة داخلية سرية",
            new DateTime(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc));
        await AddExecutionActionAsync(doc.Id, "action", "إجراء أقدم",
            new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc), actionDate: "1/8/2026");
        await AddExecutionActionAsync(doc.Id, "action", "الإجراء الأحدث",
            new DateTime(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc), actionDate: "2/8/2026");

        var actions = await _portal.ListExecutionActionsAsync(_delegateGroupId, doc.Id);

        Assert.NotNull(actions);
        var list = Assert.IsAssignableFrom<IReadOnlyList<PortalExecutionActionDto>>(actions);
        Assert.Equal(2, list.Count);
        // الأحدث أولًا (CreatedAt تنازليًا).
        Assert.Equal("الإجراء الأحدث", list[0].Text);
        Assert.Equal("إجراء أقدم", list[1].Text);
        Assert.Equal("2/8/2026", list[0].ActionDate);
        Assert.Equal("1/8/2026", list[1].ActionDate);
        Assert.Equal("محامي دمشق", list[0].CreatedByName);
        Assert.DoesNotContain(list, a => a.Text.Contains("ملاحظة"));
    }

    [Fact]
    public async Task ListExecutionActions_OutOfScope_ReturnsNull()
    {
        var doc = await SeedOutOfScopeDocAsync("ملف خارج النطاق");

        var actions = await _portal.ListExecutionActionsAsync(_delegateGroupId, doc.Id);

        Assert.Null(actions);
    }

    [Fact]
    public async Task ListExecutionActions_BreaksCreatedAtTieByNewerId()
    {
        var doc = await SeedInScopeDocAsync("ملف التعادل الزمني");
        // نفس اللحظة الزمنية بالضبط — الترتيب يجب أن يكون حتميًا (ف11: CreatedAt ثم Id).
        var sameInstant = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        await AddExecutionActionAsync(doc.Id, "action", "الإجراء الأول", sameInstant);
        await AddExecutionActionAsync(doc.Id, "action", "الإجراء الثاني", sameInstant);

        var actions = await _portal.ListExecutionActionsAsync(_delegateGroupId, doc.Id);

        Assert.NotNull(actions);
        var list = Assert.IsAssignableFrom<IReadOnlyList<PortalExecutionActionDto>>(actions);
        Assert.Equal(2, list.Count);
        // كسر التعادل: الأحدث Id يتقدم عند تساوي CreatedAt — ترتيب ثابت لا اعتباطي.
        Assert.Equal("الإجراء الثاني", list[0].Text);
        Assert.Equal("الإجراء الأول", list[1].Text);
    }

    // ── تفاصيل الملف: التقليم السلكي وعقد المشطوب ─────────────────────────

    [Fact]
    public async Task GetFile_WireTrimmed_InternalNotesAndNoteActionsDoNotLeave()
    {
        var doc = await SeedInScopeDocAsync("ملف التقليم السلكي");
        await AddExecutionActionAsync(doc.Id, "action", "إجراء علني للمندوب",
            new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc));
        await AddExecutionActionAsync(doc.Id, "note", "ملاحظة داخلية لا تعبر الشبكة",
            new DateTime(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc));

        var entity = _db.Documents.First(d => d.Id == doc.Id);
        entity.Notes = "ملاحظة سرية على المستند";
        entity.ImmediateActions = "أمر فوري داخلي";
        await _db.SaveChangesAsync();

        // بيانات تذكير داخلية على الإجراء العلني نفسه — يجب تجريدها سلكيًا (ق7: بلا تذكير).
        var actionEntity = _db.ExecutionActions.First(a => a.DocumentId == doc.Id && a.Type == "action");
        actionEntity.ReminderDuration = "7 أيام";
        actionEntity.ReminderColor = "red";
        await _db.SaveChangesAsync();

        var response = await _portal.GetFileAsync(_delegateGroupId, doc.Id, "مندوب");

        // ق7 سلكيًا: الملاحظات تُصفَّر وإجراءات note تُرشَّح — الواجهة ليست خط دفاع وحيد.
        Assert.NotNull(response);
        Assert.Null(response.Notes);
        Assert.Null(response.ImmediateActions);
        var action = Assert.Single(response.ExecutionActions);
        Assert.Equal("action", action.Type);
        Assert.Equal("إجراء علني للمندوب", action.Text);
        Assert.Null(action.ReminderDuration);
        Assert.Null(action.ReminderColor);
        Assert.DoesNotContain(response.ExecutionActions, a => a.Text.Contains("ملاحظة"));
    }

    [Fact]
    public async Task GetFile_StruckOffInScope_ExcludedFromList_StillReturnsDetail()
    {
        var normal = await SeedInScopeDocAsync("ملف متداول شاهد");
        var struck = await SeedInScopeDocAsync("ملف مشطوب داخل النطاق");
        var entity = _db.Documents.First(d => d.Id == struck.Id);
        // الشطب الحقيقي الذي تستبعده القائمة هو حالة `ExecStatus` لا مجرد تاريخ الوقعة.
        entity.ExecStatus = ExecutionStatusCatalog.StateStruckOff;
        entity.StruckOffDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        await _db.SaveChangesAsync();

        // القائمة تستبعد المشطوب فعلًا (والملف الشاهد يثبت أن الاستبعاد سببه الشطب لا النطاق).
        var list = await _portal.ListFilesAsync(_delegateGroupId, null, null, 1, 20);
        Assert.DoesNotContain(list.Items, i => i.Id == struck.Id);
        Assert.Contains(list.Items, i => i.Id == normal.Id);

        var response = await _portal.GetFileAsync(_delegateGroupId, struck.Id, "مندوب");

        // عقد موثق (ف13): القائمة تستبعد المشطوب، لكن الوصول المباشر للمشطوب داخل النطاق
        // مقصود مرآةً — المرآة تعرض الشطب أصلًا عبر «وقوعات الملف». ثبّته هنا كقرار منتج.
        Assert.NotNull(response);
        Assert.Equal(ExecutionStatusCatalog.StateStruckOff, response.ExecStatus);
        Assert.NotNull(response.StruckOffDate);
        Assert.Null(response.Notes);
    }

    // ── تشعبات الملف (إنابة) ──────────────────────────────────────────────

    [Fact]
    public async Task ListDelegations_InScope_ReturnsScopedList()
    {
        var doc = await SeedInScopeDocAsync("ملف التشعبات");

        var delegations = await _portal.ListDelegationsAsync(_delegateGroupId, doc.Id);

        Assert.NotNull(delegations);
        Assert.Empty(delegations);
    }

    [Fact]
    public async Task ListDelegations_OutOfScope_ReturnsNull()
    {
        var doc = await SeedOutOfScopeDocAsync("ملف خارج النطاق");

        var delegations = await _portal.ListDelegationsAsync(_delegateGroupId, doc.Id);

        Assert.Null(delegations);
    }

    // ── تفاصيل الاستئنافات ───────────────────────────────────────────────

    [Fact]
    public async Task ListAppealDetails_HidesDefenseOpinionAndNotes_KeepsLawyerAndData()
    {
        var doc = await SeedInScopeDocAsync("ملف الاستئناف");
        var entityId = await _db.ApplicantPublicEntities
            .Where(e => e.DocumentId == doc.Id).Select(e => e.Id).SingleAsync();

        await _appealService.CreateAsync(doc.Id, Request(entityId), _lawyerId, "محامي دمشق");

        var appeals = await _portal.ListAppealDetailsAsync(_delegateGroupId, doc.Id);

        Assert.NotNull(appeals);
        var appeal = Assert.Single(appeals);
        // ق10 الموسّعة: رأي المحامي الداخلي وملاحظاته الحرة لا يصلان إلى البوابة إطلاقًا —
        // حقلان حرّان من إدخاله بلا ضمان بنيوي بأنهما وقائع قضية.
        Assert.Null(appeal.DefenseOpinion);
        Assert.Null(appeal.Notes);
        // ق9: اسم المحامي وبقية المحتوى يبقى ظاهرًا.
        Assert.Equal("محامي دمشق", appeal.CreatedByName);
        Assert.Equal(AppealDirectionCatalog.Appellants, appeal.Direction);
        Assert.Equal("عادي", appeal.AppealTypeLabel);
        Assert.NotEmpty(appeal.Appellants);
    }

    [Fact]
    public async Task ListAppealDetails_OutOfScope_ReturnsNull()
    {
        var doc = await SeedOutOfScopeDocAsync("ملف خارج النطاق");

        var appeals = await _portal.ListAppealDetailsAsync(_delegateGroupId, doc.Id);

        Assert.Null(appeals);
    }

    // ── أرقام الأساس ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListBaseNumbers_ReturnsHistory_OrderedByYearThenCreatedAtDesc()
    {
        var doc = await SeedInScopeDocAsync("ملف الأرقام");
        // سنتان، داخل الثانية سجلان — الأحدث CreatedAt صاحب الصف الأول كما في صفحة المحامي.
        await AddBaseNumbersAsync(doc.Id,
            (2024, "أ/2024", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            (2025, "ب/2025", new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            (2025, "ج/2025", new DateTime(2025, 7, 1, 0, 0, 0, DateTimeKind.Utc)));

        var history = await _portal.ListBaseNumbersAsync(_delegateGroupId, doc.Id);

        Assert.NotNull(history);
        var list = Assert.IsAssignableFrom<IReadOnlyList<BaseNumberHistoryDto>>(history);
        Assert.Equal(3, list.Count);
        // ترتيب السنة تنازليًا ثم CreatedAt تنازليًا داخل السنة (مطابقة GetBaseNumberHistoryAsync).
        Assert.Equal(2025, list[0].Year);
        Assert.Equal("ج/2025", list[0].BaseNumber);
        Assert.Equal("ب/2025", list[1].BaseNumber);
        Assert.Equal(2024, list[2].Year);
        Assert.Equal("أ/2024", list[2].BaseNumber);
    }

    [Fact]
    public async Task ListBaseNumbers_OutOfScope_ReturnsNull()
    {
        var doc = await SeedOutOfScopeDocAsync("ملف خارج النطاق");

        var history = await _portal.ListBaseNumbersAsync(_delegateGroupId, doc.Id);

        Assert.Null(history);
    }

    // ── تصدير Excel: لا تسريب للملاحظات (ف12) ─────────────────────────────

    [Fact]
    public async Task Export_DoesNotLeakInternalNotes_IntoExcelColumn()
    {
        var doc = await SeedInScopeDocAsync("ملف التصدير");
        // الإجراء الأقدم أولًا، ثم ملاحظة أحدث منه — كانت الملاحظة ستصدر أحيانًا في العمود
        // (الأحدث CreatedAt يتصدر القائمة) قبل ترشيح النوع action (ف12).
        await AddExecutionActionAsync(doc.Id, "action", "نص إجراء تنفيذي علني",
            new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc));
        await AddExecutionActionAsync(doc.Id, "note", "ملاحظة داخلية سرية لا تُصدَّر",
            new DateTime(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc));

        var bytes = await _portal.ExportWorkbookAsync(_delegateGroupId, null, null, "مندوب");

        var sheetXml = ReadFirstSheetXml(bytes);
        Assert.Contains("نص إجراء تنفيذي علني", sheetXml);
        Assert.DoesNotContain("ملاحظة داخلية سرية لا تُصدَّر", sheetXml);
    }

    [Fact]
    public async Task Export_ScrubsInternalBranchName_FromBranchColumn()
    {
        // A: عمود «الفرع» في تصدير البوابة يُقرأ من BranchName الداخلي —
        // ومسار التفاصيل يحجبه، فيجب أن يحجبه التصدير أيضًا (سياسة حجب واحدة
        // يمرّ بها كل مسار، لا تعليق يُنسخ).
        var doc = await SeedInScopeDocAsync("ملف فرع التصدير");
        doc.BranchName = "فرع داخلي سري";
        await _db.SaveChangesAsync();

        var bytes = await _portal.ExportWorkbookAsync(_delegateGroupId, null, null, "مندوب");

        var sheetXml = ReadFirstSheetXml(bytes);
        Assert.DoesNotContain("فرع داخلي سري", sheetXml);
    }

    [Fact]
    public async Task Export_TieBreakById_MatchesDetailCardOrder()
    {
        var doc = await SeedInScopeDocAsync("ملف التعادل في التصدير");
        // اللحظة نفسها بالضبط — عمود التصدير يجب أن يتصدره الأحدث Id كما في بطاقة التفاصيل.
        var sameInstant = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        await AddExecutionActionAsync(doc.Id, "action", "إجراء التعادل الأقدم", sameInstant);
        await AddExecutionActionAsync(doc.Id, "action", "إجراء التعادل الأحدث", sameInstant);

        var bytes = await _portal.ExportWorkbookAsync(_delegateGroupId, null, null, "مندوب");

        // عمود التصدير خلية واحدة (الإجراء الأول) — كسر التعادل يختار الأحدث Id كما في البطاقة.
        var sheetXml = ReadFirstSheetXml(bytes);
        Assert.Contains("إجراء التعادل الأحدث", sheetXml);
        Assert.DoesNotContain("إجراء التعادل الأقدم", sheetXml);
    }

    [Fact]
    public async Task Export_PortalHeadersMatchDeclaredAllowlist()
    {
        // G2: أي عمود جديد في تصدير البوابة يُجبر صاحبه على قرار صريح —
        // تُقارَن العناوين بقائمة حرفية كاملة لا بالاحتواء فقط، وبالرايات
        // المعطلة للبوابة (بلا فرع إدارة/محامٍ مختص/عدادات).
        var doc = await SeedInScopeDocAsync("ملف عناوين التصدير");

        var bytes = await _portal.ExportWorkbookAsync(_delegateGroupId, null, null, "مندوب");

        var headers = ReadFirstRowTexts(ReadFirstSheetXml(bytes));
        Assert.Equal(
            new[]
            {
                "الحالة", "طالب التنفيذ", "الفرع", "المنفذ عليه", "دائرة التنفيذ",
                "رقم الملف", "لعام", "ملحق العقد", "الإجراءات والملاحظات",
            },
            headers);
    }

    private static List<string> ReadFirstRowTexts(string sheetXml)
    {
        // تحليل بالاسم المحلي لا بالبادئة — لا يعتمد على شكل الـnamespace
        // الذي يكتبه مولّد OpenXML.
        var xdoc = System.Xml.Linq.XDocument.Parse(sheetXml);
        return xdoc.Descendants()
            .Where(e => e.Name.LocalName == "row").First()
            .Descendants()
            .Where(e => e.Name.LocalName == "t")
            .Select(t => t.Value)
            .ToList();
    }

    private static string ReadFirstSheetXml(byte[] xlsx)
    {
        using var stream = new MemoryStream(xlsx);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.Entries.First(e => e.FullName.EndsWith("sheet1.xml", StringComparison.OrdinalIgnoreCase));
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static UpsertAppealRequest Request(int entityId) => new(
        Direction: AppealDirectionCatalog.Appellants,
        Appellants: new List<AppealPartySelectionDto> { new("applicant-entity", entityId) },
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
        DefenseOpinion: "رأي سري للمحامي بأسباب الاستئناف",
        Notes: "ملاحظة حرة للمحامي لا تعبر البوابة");
}