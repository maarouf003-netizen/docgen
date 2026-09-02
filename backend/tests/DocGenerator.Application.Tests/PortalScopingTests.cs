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
/// اختبارات عزل نطاق بوابة مندوب الجهة (المرحلة 3): قاعدة «أي تطابق طرفي
/// بقيد نهائي»، إخفاء قيود الانتظار، و404 للملفات خارج النطاق.
/// </summary>
public class PortalScopingTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IPortalService _portal;
    private readonly IPublicEntityService _entities;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _groupAId;
    private readonly int _entryAId;
    private readonly int _entryBId;
    private readonly int _entryPendingId;
    private readonly int _delegateGroupId;
    private readonly int _delegateEntryAId;
    private readonly int _managerId;

    public PortalScopingTests()
    {
        _db = TestDb.Create();

        // مستخدم مرجعي (Id=1) لملكية إنشاء القيود قبل إدراجها.
        _db.Users.Add(new User { Username = "creator", FullName = "منشئ", Role = UserRole.Admin, PasswordHash = "x" });
        _db.SaveChanges();

        var manager = new User { Username = "entity_manager", FullName = "مدير السجل", Role = UserRole.Manager, PasswordHash = "x" };
        _db.Users.Add(manager);
        _db.SaveChanges();
        _managerId = manager.Id;

        var groupA = new PublicEntityGroup { CanonicalName = "وزارة التعليم", EntityType = PublicEntityTypeCatalog.Ministry };
        groupA.Entries.Add(new PublicEntity { Governorate = "دمشق", BranchName = "الفرع الرئيسي", Status = EntityStatusCatalog.Final, CreatedById = 1 });
        groupA.Entries.Add(new PublicEntity { Governorate = "حلب", BranchName = "فرع حلب", Status = EntityStatusCatalog.Pending, CreatedById = 1 });
        var groupB = new PublicEntityGroup { CanonicalName = "مديرية النقل", EntityType = PublicEntityTypeCatalog.Administration };
        groupB.Entries.Add(new PublicEntity { Governorate = "دمشق", BranchName = "فرع النقل", Status = EntityStatusCatalog.Final, CreatedById = 1 });
        _db.PublicEntityGroups.AddRange(groupA, groupB);
        _db.SaveChanges();
        _groupAId = groupA.Id;
        _entryAId = groupA.Entries.First(e => e.Status == EntityStatusCatalog.Final).Id;
        _entryPendingId = groupA.Entries.First(e => e.Status == EntityStatusCatalog.Pending).Id;
        _entryBId = groupB.Entries.First().Id;

        var delegateGroup = new User { Username = "delegate_group", FullName = "مندوب الوزارة", Role = UserRole.EntityManager, PasswordHash = "x" };
        var delegateGroup2 = new User { Username = "delegate_group2", FullName = "مندوب الوزارة ٢", Role = UserRole.EntityManager, PasswordHash = "x", PortalGroupId = _groupAId };
        var delegateEntry = new User { Username = "delegate_entry", FullName = "مندوب القيد", Role = UserRole.EntityManager, PasswordHash = "x", PortalEntryId = _entryAId };
        _db.Users.AddRange(delegateGroup, delegateGroup2, delegateEntry);
        _db.SaveChanges();
        // مستخدم المندوب الأول يُربط بعد حفظ المجموعة:
        delegateGroup.PortalGroupId = _groupAId;
        _db.SaveChanges();
        _delegateGroupId = delegateGroup.Id;
        _delegateEntryAId = delegateEntry.Id;

        _portal = new PortalService(
            new PortalRepository(_db),
            new Repository<Document>(_db),
            new AppealRepository(_db),
            new ExcelExportService(),
            _audit,
            Options.Create(new ExportOptions { MaxRows = 10_000 }));

        _entities = new PublicEntityService(
            new PublicEntityRepository(_db),
            new Repository<Branch>(_db),
            new HeadAlertRepository(_db),
            new Repository<PublicEntityChangeEvent>(_db),
            new Repository<DocumentOccurrence>(_db),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit,
            new UserRepository(_db),
            new AppealRepository(_db));
    }

    public void Dispose() => _db.Dispose();

    /// <summary>ينشئ ملفاً بروابط أطراف محددة مباشرة في القاعدة (بمعزل عن مسار الحفظ).</summary>
    private async Task<int> SeedDocumentAsync(
        string name,
        int? applicantRegistryId = null,
        (int RegistryId, bool Legal)? executedLink = null,
        (int RegistryId, bool Legal)? executionApplicantLink = null)
    {
        var doc = new Document
        {
            CreatedById = 1,
            IsDraft = false,
            BorrowerName = name,
            AmountNumeric = 100,
            ExecStatus = string.Empty,
            GeneralEntitySide = applicantRegistryId.HasValue ? "applicant" : "executed",
        };
        if (applicantRegistryId.HasValue)
        {
            doc.ApplicantPublicEntities.Add(new ApplicantPublicEntity { Name = name, Governorate = "دمشق", RegistryId = applicantRegistryId });
            doc.ApplicantRegistryId = applicantRegistryId;
            doc.Applicant = $"{name} - محافظة دمشق";
        }
        if (executedLink is not null)
        {
            doc.ExecutedPublicEntities.Add(new ExecutedPublicEntity
            {
                EntityName = name, EntityNature = executedLink.Value.Legal ? "legal" : "public",
                RegistryId = executedLink.Value.RegistryId,
            });
        }
        if (executionApplicantLink is not null)
        {
            doc.ExecutionApplicants.Add(new ExecutionApplicant
            {
                Name = name,
                ApplicantNature = executionApplicantLink.Value.Legal ? PartyNatureCatalog.Legal : PartyNatureCatalog.Natural,
                // الطبيعي لا يحمل رقم قيد (حسب NormalizeExecutionApplicants): يُصفّر.
                RegistryId = executionApplicantLink.Value.Legal ? executionApplicantLink.Value.RegistryId : null,
            });
        }
        doc.SearchText = DocumentSearchTextBuilder.Build(doc);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        return doc.Id;
    }

    [Fact]
    public async Task GroupScope_MatchesAnySide_FinalOnly_AndExcludesUnrelated()
    {
        var docApplicant = await SeedDocumentAsync("ملف طالب", applicantRegistryId: _entryAId);
        // «أي تطابق طرفي»: ربط المنفذ على قيد من نفس الهوية يُدخل الملف النطاق أيضًا.
        var docExecuted = await SeedDocumentAsync("ملف منفذ", executedLink: (_entryAId, false));
        // ملف مرتبط بهوية أخرى لا يدخل نطاق هوية «وزارة التعليم».
        var docOtherGroup = await SeedDocumentAsync("ملف غير متعلق", executedLink: (_entryBId, false));

        var page = await _portal.ListFilesAsync(_delegateGroupId, null, null, 1, 20);

        Assert.Equal(2, page.TotalCount);
        Assert.Contains(page.Items, i => i.Id == docApplicant);
        Assert.Contains(page.Items, i => i.Id == docExecuted);
        Assert.DoesNotContain(page.Items, i => i.Id == docOtherGroup);
    }

    [Fact]
    public async Task EntryScope_MatchesExactEntryOnly()
    {
        await SeedDocumentAsync("ملف طالب", applicantRegistryId: _entryAId);
        var docOther = await SeedDocumentAsync("ملف منفذ", executedLink: (_entryBId, false));

        var page = await _portal.ListFilesAsync(_delegateEntryAId, null, null, 1, 20);

        Assert.Equal(1, page.TotalCount);
        Assert.DoesNotContain(page.Items, i => i.Id == docOther);
    }

    [Fact]
    public async Task ExecutionApplicantLink_EntersDelegateScope_LegalRegistryIdOnly()
    {
        // ملف «منفذ عليه» برابط طالب تنفيذ اعتباري على قيد هوية «وزارة التعليم»:
        // يدخل نطاق هوية المجموعة عبر رابط طلب التنفيذ (مثل أي طرف ربط آخر).
        var docLinked = await SeedDocumentAsync("ملف طالب تنفيذ اعتباري", executionApplicantLink: (_entryAId, true));
        // ربط طبيعي بلا رقم قيد (حسب NormalizeExecutionApplicants) لا يدخل نطاق الهوية.
        var docNatural = await SeedDocumentAsync("ملف طالب طبيعي", executionApplicantLink: (default, false));
        var docOther = await SeedDocumentAsync("ملف طالب هوية أخرى", executionApplicantLink: (_entryBId, true));

        var page = await _portal.ListFilesAsync(_delegateGroupId, null, null, 1, 20);

        Assert.Contains(page.Items, i => i.Id == docLinked);
        Assert.DoesNotContain(page.Items, i => i.Id == docNatural);
        Assert.DoesNotContain(page.Items, i => i.Id == docOther);
    }

    [Fact]
    public async Task PendingLinkedDocuments_AreHidden_UntilHeadApproves()
    {
        await SeedDocumentAsync("ملف بانتظار الاعتماد", applicantRegistryId: _entryPendingId);

        var before = await _portal.ListFilesAsync(_delegateGroupId, null, null, 1, 20);
        Assert.Equal(0, before.TotalCount);

        // الاعتماد يحوّل القيد إلى نهائي فيظهر الملف تلقائيًا (د4).
        var entry = await _db.PublicEntities.SingleAsync(e => e.Id == _entryPendingId);
        entry.Status = EntityStatusCatalog.Final;
        await _db.SaveChangesAsync();

        var after = await _portal.ListFilesAsync(_delegateGroupId, null, null, 1, 20);
        Assert.Equal(1, after.TotalCount);
    }

    [Fact]
    public async Task Detail_OutOfScope_ReturnsNull_WithoutViewAudit()
    {
        var docOutOfScope = await SeedDocumentAsync("ملف منفذ", executedLink: (_entryBId, false));

        var result = await _portal.GetFileAsync(_delegateEntryAId, docOutOfScope, "مندوب");

        Assert.Null(result);
        Assert.DoesNotContain("view_entity_portal_files", _audit.Actions);
    }

    [Fact]
    public async Task Detail_InScope_ReturnsFullReadModel_AndLogsSessionView()
    {
        var docId = await SeedDocumentAsync("ملف طالب", applicantRegistryId: _entryAId);

        var file = await _portal.GetFileAsync(_delegateGroupId, docId, "مندوب");

        Assert.NotNull(file);
        Assert.Equal("ملف طالب", file.BorrowerName);
        Assert.Contains("view_entity_portal_files", _audit.Actions);
    }

    [Fact]
    public async Task Appeals_OutOfScope_ReturnsNull_InScope_Lists()
    {
        var docId = await SeedDocumentAsync("ملف طالب", applicantRegistryId: _entryAId);

        Assert.Null(await _portal.ListAppealsAsync(_delegateEntryAId, 999_999));
        var inScope = await _portal.ListAppealsAsync(_delegateGroupId, docId);
        Assert.NotNull(inScope);
        Assert.Empty(inScope);
    }

    [Fact]
    public async Task StatusFilter_Executed_NarrowsWithinScope()
    {
        var normalDoc = await SeedDocumentAsync("ملف طالب متداول", applicantRegistryId: _entryAId);
        var executedDoc = new Document
        {
            CreatedById = 1, BorrowerName = "ملف طالب منفذ", AmountNumeric = 50,
            IsDraft = false, ExecStatus = ExecutionStatusCatalog.ExecutedBySettlement,
            GeneralEntitySide = "applicant",
        };
        executedDoc.ApplicantPublicEntities.Add(new ApplicantPublicEntity { Name = "وزارة التعليم", Governorate = "دمشق", RegistryId = _entryAId });
        _db.Documents.Add(executedDoc);
        await _db.SaveChangesAsync();

        var executed = await _portal.ListFilesAsync(_delegateGroupId, null, ExecutionStatusCatalog.ExecutedFilter, 1, 20);
        var all = await _portal.ListFilesAsync(_delegateGroupId, null, null, 1, 20);

        Assert.Equal(1, executed.TotalCount);
        Assert.Equal(executedDoc.Id, executed.Items[0].Id);
        Assert.Equal(2, all.TotalCount);
        Assert.Contains(normalDoc, all.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Export_ExceedingMaxRows_IsRejected_WithFriendlyCapMessage()
    {
        await SeedDocumentAsync("ملف أ", applicantRegistryId: _entryAId);
        await SeedDocumentAsync("ملف ب", applicantRegistryId: _entryAId);

        var cappedPortal = new PortalService(
            new PortalRepository(_db),
            new Repository<Document>(_db),
            new AppealRepository(_db),
            new ExcelExportService(),
            _audit,
            Options.Create(new ExportOptions { MaxRows = 1 }));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            cappedPortal.ExportWorkbookAsync(_delegateGroupId, null, null, "مندوب"));

        Assert.Contains("الحد الأقصى للتصدير", ex.Message);
    }

    [Fact]
    public async Task Export_WithinCap_ProducesWorkbook_AndLogsExportAction()
    {
        await SeedDocumentAsync("ملف تصدير", applicantRegistryId: _entryAId);

        var bytes = await _portal.ExportWorkbookAsync(_delegateGroupId, null, null, "مندوب");

        Assert.NotEmpty(bytes);
        // توقيع xlsx: ملف ZIP يبدأ بالبايتات PK.
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
        Assert.Contains("export_entity_portal_excel", _audit.Actions);
    }

    [Fact]
    public async Task DelegateWithoutBinding_SeesNothing_ButGetsEmptyScope()
    {
        var unbound = new User { Username = "delegate_none", FullName = "مندوب بلا نطاق", Role = UserRole.EntityManager, PasswordHash = "x" };
        _db.Users.Add(unbound);
        await _db.SaveChangesAsync();
        await SeedDocumentAsync("ملف طالب", applicantRegistryId: _entryAId);

        var scope = await _portal.GetMyScopeAsync(unbound.Id);
        var files = await _portal.ListFilesAsync(unbound.Id, null, null, 1, 20);

        Assert.NotNull(scope);
        Assert.Empty(files.Items);
        Assert.Equal(0, files.TotalCount);
    }

    // ── المواءمة السلوكية (§6bis): قيد بانتظار مراجعة (Status=Final + NeedsReview=true)
    //    — كما يُنشئه CreateAsync للمحامي — لا يظهر لبوات المندوبين قبل اعتماد رئيس القسم. ──

    [Fact]
    public async Task NeedsReviewEntry_IsHiddenFromPortal_UntilApproved()
    {
        // قيد Status=Final لكن NeedsReview=true (الحالة التي يُنتجها CreateAsync للمحامي).
        var pendingEntry = new PublicEntity
        {
            GroupId = _groupAId,
            Governorate = "حمص",
            BranchName = "فرع حمص",
            Status = EntityStatusCatalog.Final,
            NeedsReview = true,
            CreatedById = 1,
        };
        _db.PublicEntities.Add(pendingEntry);
        await _db.SaveChangesAsync();

        var pendingId = pendingEntry.Id;
        var delegateEntryId = new User
        {
            Username = "delegate_review", FullName = "مندوب مراجعة",
            Role = UserRole.EntityManager, PasswordHash = "x", PortalEntryId = pendingId,
        };
        _db.Users.Add(delegateEntryId);
        await _db.SaveChangesAsync();

        await SeedDocumentAsync("ملف بانتظار مراجعة", applicantRegistryId: pendingId);

        // قبل الاعتماد: لا يظهر الملف للمندوب (DoD#5) رغم أن Status=Final.
        var before = await _portal.ListFilesAsync(delegateEntryId.Id, null, null, 1, 20);
        Assert.Equal(0, before.TotalCount);

        // الاعتماد (approve-review) يقفل NeedsReview فيظهر الملف للمندوب لحظيًا.
        var entry = await _db.PublicEntities.SingleAsync(e => e.Id == pendingId);
        entry.NeedsReview = false;
        await _db.SaveChangesAsync();

        var after = await _portal.ListFilesAsync(delegateEntryId.Id, null, null, 1, 20);
        Assert.Equal(1, after.TotalCount);
    }

    [Fact]
    public async Task NeedsReviewGroupEntries_AreExcludedFromScopeResolution()
    {
        // مندوب مربوط بمجموعة: القيد (Status=Final + NeedsReview=true) لا يدخل نطاقه.
        var pendingEntry = new PublicEntity
        {
            GroupId = _groupAId,
            Governorate = "طرطوس",
            BranchName = "فرع طرطوس",
            Status = EntityStatusCatalog.Final,
            NeedsReview = true,
            CreatedById = 1,
        };
        _db.PublicEntities.Add(pendingEntry);
        await _db.SaveChangesAsync();
        var pendingId = pendingEntry.Id;

        var scope = await _portal.GetMyScopeAsync(_delegateGroupId);

        Assert.NotNull(scope);
        Assert.DoesNotContain(scope.Entries, e => e.Id == pendingId);
        Assert.Contains(scope.Entries, e => e.Id == _entryAId);
    }

    // ── المواءمة السلوكية (§8.7): عمليات مراجعة السجل (rename) لا تُظهر للمندوب
    //     الملفات المرتبطة بقيدٍ ما زال بانتظار مراجعة رئيس القسم (NeedsReview=true). ──

    [Fact]
    public async Task Rename_ProducedEntriesSurface_WhilePendingReviewEntry_StaysHiddenUntilApproval()
    {
        var doc = await SeedDocumentAsync("ملف وزارة التعليم", applicantRegistryId: _entryAId);

        // إعادة تسمية جماعية ناجحة عبر خدمة السجل (ريّن يُنتج قيودًا نهائية غير NeedsReview).
        var rename = await _entities.RenameGroupAsync(
            new RenameGroupRequest(_groupAId, "وزارة التربية", "قرار", "88", "1/8/2026"),
            new EntityRegistryActor(_managerId, "مدير السجل", UserRole.Manager, null));

        Assert.Equal("وزارة التربية", rename.NewCanonicalName);

        // بعد الريّن: ملف القيد النهائي يظهر للمندوب فورًا (لا NeedsReview).
        var afterRename = await _portal.ListFilesAsync(_delegateGroupId, null, null, 1, 20);
        Assert.Contains(afterRename.Items, i => i.Id == doc);

        // اقتراح محامٍ جديد داخل الهوية المُعاد تسميتها: قيد Final + NeedsReview=true
        // لا يُظهر ملفه للمندوب قبل اعتماد رئيس القسم (عزل §6bis/§8.7).
        var propEntry = new PublicEntity
        {
            GroupId = _groupAId,
            Governorate = "حمص",
            BranchName = "فرع حمص",
            Status = EntityStatusCatalog.Final,
            NeedsReview = true,
            CreatedById = _db.Users.Single(u => u.Username == "entity_manager").Id,
        };
        _db.PublicEntities.Add(propEntry);
        await _db.SaveChangesAsync();
        var pendingDoc = await SeedDocumentAsync("ملف مقترح بانتظار المراجعة", applicantRegistryId: propEntry.Id);

        var pendingFiles = await _portal.ListFilesAsync(_delegateGroupId, null, null, 1, 20);
        Assert.DoesNotContain(pendingFiles.Items, i => i.Id == pendingDoc);

        // الاعتماد يقفل NeedsReview فيظهر الملف فورًا.
        propEntry.NeedsReview = false;
        await _db.SaveChangesAsync();

        var afterApproval = await _portal.ListFilesAsync(_delegateGroupId, null, null, 1, 20);
        Assert.Contains(afterApproval.Items, i => i.Id == pendingDoc);
    }
}
