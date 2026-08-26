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
/// اختبارات خدمة السجل المرجعي للجهات العامة وفق نموذج الحوكمة الجديد:
/// ما يُدخله المحامي يُعتمد فورًا ويبقى «بحاجة مراجعة» مع تنبيه رئيس محافظته،
/// والاعتماد يقفل المراجعة بصمت، والتعديل — وتغيير التسمية تحديدًا — يبلّغ المُدخِل.
/// </summary>
public class PublicEntityServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IPublicEntityService _service;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _damascusId;
    private readonly int _aleppoId;
    private readonly int _managerId;
    private readonly int _headDamascusId;
    private readonly int _headAleppoId;
    private readonly int _lawyerId;

    public PublicEntityServiceTests()
    {
        _db = TestDb.Create();
        _db.Branches.AddRange(
            new Branch { Name = "الفرع الرئيسي - دمشق", Code = "DAM", Governorate = "دمشق" },
            new Branch { Name = "فرع حلب", Code = "ALP", Governorate = "حلب" });
        _db.SaveChanges();

        _damascusId = _db.Branches.Single(b => b.Code == "DAM").Id;
        _aleppoId = _db.Branches.Single(b => b.Code == "ALP").Id;

        var mgr = new User { Username = "mgr", FullName = "المدير", Role = UserRole.Manager, PasswordHash = "x" };
        var headDam = new User { Username = "head_dam", FullName = "رئيس قسم دمشق", Role = UserRole.Head, BranchId = _damascusId, PasswordHash = "x" };
        var headAlp = new User { Username = "head_alp", FullName = "رئيس قسم حلب", Role = UserRole.Head, BranchId = _aleppoId, PasswordHash = "x" };
        var lawyer = new User { Username = "lawyer1", FullName = "محامي دمشق", Role = UserRole.Lawyer, BranchId = _damascusId, PasswordHash = "x" };
        _db.Users.AddRange(mgr, headDam, headAlp, lawyer);
        _db.SaveChanges();
        _managerId = mgr.Id;
        _headDamascusId = headDam.Id;
        _headAleppoId = headAlp.Id;
        _lawyerId = lawyer.Id;

        _service = new PublicEntityService(
            new PublicEntityRepository(_db),
            new Repository<Branch>(_db),
            new Repository<HeadAlert>(_db),
            new Repository<PublicEntityChangeEvent>(_db),
            new Repository<DocumentOccurrence>(_db),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit);
    }

    public void Dispose() => _db.Dispose();

    private EntityRegistryActor ManagerActor() => new(_managerId, "المدير", UserRole.Manager, null);

    private int UserId() => _managerId;

    private EntityRegistryActor LawyerActor() => new(_lawyerId, "محامي دمشق", UserRole.Lawyer, _damascusId);

    private EntityRegistryActor HeadDamascusActor() =>
        new(_headDamascusId, "رئيس قسم دمشق", UserRole.Head, _damascusId);

    private async Task<Document> SeedApplicantDocumentAsync(string entityName, string governorate, string generalSide = "applicant")
    {
        var doc = new Document
        {
            BranchId = _damascusId,
            CreatedById = _lawyerId,
            IsDraft = false,
            BorrowerName = "شركة المباني",
            AmountNumeric = 0,
            ExecStatus = string.Empty,
            GeneralEntitySide = generalSide,
            ApplicantPublicEntities =
            {
                new ApplicantPublicEntity { Name = entityName, Governorate = governorate, Branch = "فرع الجهة" },
            },
        };
        doc.Applicant = $"{entityName} - محافظة {governorate}";
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        return doc;
    }

    [Fact]
    public async Task Create_BuildsGroupAndFinalEntry_WithAliasesAndAudit()
    {
        var dto = await _service.CreateAsync(new CreatePublicEntityRequest(
            "وزارة التعليم", "ministry", "دمشق", "الفرع الرئيسي",
            CitationFormulaCatalog.AddToPosition, new[] { "وزارة التعليم العالي" }), ManagerActor());

        Assert.True(dto.Id > 0);
        Assert.Equal("final", dto.Status);
        Assert.Equal("ministry", dto.EntityType);
        Assert.Equal(CitationFormulaCatalog.AddToPosition, dto.CitationFormula);
        Assert.Contains("وزارة التعليم العالي", dto.Aliases);
        Assert.Contains("create_public_entity", _audit.Actions);

        var stored = await _db.PublicEntities.Include(e => e.Group).SingleAsync(e => e.Id == dto.Id);
        Assert.Equal("وزارة التعليم", stored.Group.CanonicalName);
    }

    [Fact]
    public async Task Create_EmptyName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreatePublicEntityRequest(" ", "ministry", "دمشق", "الفرع الرئيسي"), ManagerActor()));
    }

    [Fact]
    public async Task Create_InvalidEntityType_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreatePublicEntityRequest("هيئة جديدة", "union", "دمشق", "الفرع الرئيسي"), ManagerActor()));
    }

    [Fact]
    public async Task Create_DuplicateSameGovernorateAndBranch_Throws()
    {
        await _service.CreateAsync(new CreatePublicEntityRequest("وزارة التعليم", "ministry", "دمشق", "الفرع الرئيسي"), ManagerActor());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreatePublicEntityRequest("وزارة التعليم", "ministry", "دمشق", "الفرع الرئيسي"), ManagerActor()));
    }

    [Fact]
    public async Task Create_AllowsSameEntity_InDifferentGovernorates()
    {
        await _service.CreateAsync(new CreatePublicEntityRequest("وزارة التعليم", "ministry", "دمشق", "الفرع الرئيسي"), ManagerActor());
        var second = await _service.CreateAsync(new CreatePublicEntityRequest("وزارة التعليم", "ministry", "حلب", "فرع حلب"), ManagerActor());

        Assert.True(second.Id > 0);
    }

    [Fact]
    public async Task Create_DefaultsBranchName_WhenMissing()
    {
        var dto = await _service.CreateAsync(new CreatePublicEntityRequest("مديرية النقل", "administration", "حمص", ""), ManagerActor());

        Assert.Equal("الفرع الرئيسي", dto.BranchName);
    }

    [Fact]
    public async Task List_HidesPending_ByDefault_AndFiltersByGovernorate()
    {
        var final = await _service.CreateAsync(new CreatePublicEntityRequest("وزارة التعليم", "ministry", "دمشق", "الفرع الرئيسي"), ManagerActor());
        var pendingEntry = new PublicEntity
        {
            GroupId = final.GroupId, Governorate = "حلب", BranchName = "فرع حلب",
            Status = EntityStatusCatalog.Pending, CreatedById = _managerId,
        };
        _db.PublicEntities.Add(pendingEntry);
        await _db.SaveChangesAsync();

        var hidden = await _service.ListAsync(new EntityRegistryListQuery(null, null, null, IncludePending: false, 1, 20));
        var visible = await _service.ListAsync(new EntityRegistryListQuery(null, "دمشق", null, IncludePending: true, 1, 20));
        var pendingOnly = await _service.ListAsync(new EntityRegistryListQuery(null, null, "pending", IncludePending: true, 1, 20));

        Assert.Equal(1, hidden.TotalCount);
        Assert.Equal(1, visible.TotalCount);
        Assert.Equal(1, pendingOnly.TotalCount);
        Assert.Equal(EntityStatusCatalog.Pending, pendingOnly.Items[0].Status);
    }

    [Fact]
    public async Task Search_MatchesNormalizedArabicSpelling()
    {
        await _service.CreateAsync(new CreatePublicEntityRequest("الإدارة العامة للكتابة", "administration", "دمشق", "الفرع الرئيسي"), ManagerActor());

        // «الاداره» بلا همزات وتاء مربوطة تطابق «الإدارة العامة» بعد التطبيع.
        var result = await _service.ListAsync(new EntityRegistryListQuery("الاداره العامه", null, null, IncludePending: false, 1, 20));

        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_ExcludesInactiveEntries_SoTheyCannotBeLinkedOrBound()
    {
        var entry = await _service.CreateAsync(new CreatePublicEntityRequest("هيئة معايير قديمة", "authority", "دمشق", "الفرع الرئيسي"), ManagerActor());
        await _service.UpdateAsync(entry.Id,
            new UpdatePublicEntityRequest(null, null, null, null, null, null, IsActive: false), ManagerActor());

        var pickerView = await _service.ListAsync(new EntityRegistryListQuery(null, null, null, IncludePending: true, 1, 50, IncludeInactive: false));
        var managementView = await _service.ListAsync(new EntityRegistryListQuery(null, null, null, IncludePending: true, 1, 50));

        Assert.Equal(0, pickerView.TotalCount);      // نافذة الاختيار وربط المندوبين
        Assert.Equal(1, managementView.TotalCount);  // شاشة الإدارة ترى الموقوف أيضًا
    }

    [Fact]
    public async Task Update_RenameByManager_SyncsBothSides_AndLogsFieldChanges()
    {
        var applicantDoc = await SeedApplicantDocumentAsync("وزارة التعليم", "دمشق", "applicant");
        var executedDoc = new Document
        {
            BranchId = _damascusId,
            CreatedById = _lawyerId,
            BorrowerName = "شركة الأمل",
            AmountNumeric = 0,
            ExecStatus = string.Empty,
            GeneralEntitySide = "executed",
        };
        executedDoc.ExecutedPublicEntities.Add(new ExecutedPublicEntity
        {
            EntityName = "وزارة التعليم", EntityBranch = "فرع التنفيذ", EntityNature = "public",
        });
        _db.Documents.Add(executedDoc);
        await _db.SaveChangesAsync();

        var entry = await _service.CreateAsync(new CreatePublicEntityRequest("وزارة التعليم", "ministry", "دمشق", "الفرع الرئيسي"), ManagerActor());
        _audit.Actions.Clear();

        var updated = await _service.UpdateAsync(entry.Id,
            new UpdatePublicEntityRequest("وزارة التربية", null, null, null, null, null, null), ManagerActor());

        Assert.NotNull(updated);
        Assert.Equal("وزارة التربية", updated.CanonicalName);

        var applicantRow = await _db.ApplicantPublicEntities.AsNoTracking().SingleAsync(a => a.DocumentId == applicantDoc.Id);
        Assert.Equal("وزارة التربية", applicantRow.Name);

        var applicantAfter = await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == applicantDoc.Id);
        Assert.Equal("وزارة التربية - محافظة دمشق", applicantAfter.Applicant);
        Assert.Contains("وزارة التربية", applicantAfter.SearchText);

        var executedRow = await _db.ExecutedPublicEntities.AsNoTracking().SingleAsync(e => e.DocumentId == executedDoc.Id);
        Assert.Equal("وزارة التربية", executedRow.EntityName);

        var syncedLogs = _audit.ChangeLogs.Where(c => c.ActionType == "rename_public_entity_sync").ToList();
        Assert.Equal(2, syncedLogs.Count);
        var applicantLog = syncedLogs.Single(c => c.DocumentId == applicantDoc.Id);
        Assert.Contains(applicantLog.Changes, ch => ch.FieldKey == nameof(Document.Applicant)
            && ch.OldValue == "وزارة التعليم - محافظة دمشق"
            && ch.NewValue == "وزارة التربية - محافظة دمشق");
        var executedLog = syncedLogs.Single(c => c.DocumentId == executedDoc.Id);
        Assert.Contains(executedLog.Changes, ch => ch.FieldKey == "__Col_ExecutedPublicEntities"
            && ch.OldValue!.Contains("وزارة التعليم")
            && ch.NewValue!.Contains("وزارة التربية"));
        Assert.Contains("rename_public_entity", _audit.Actions);
    }

    [Fact]
    public async Task Update_Rename_PreservesUnrelatedSearchTokens_AndRebuildsOncePerDocument()
    {
        var doc = new Document
        {
            BranchId = _damascusId,
            CreatedById = _lawyerId,
            IsDraft = false,
            BorrowerName = "شركة المباني",
            AmountNumeric = 0,
            ExecStatus = string.Empty,
            GeneralEntitySide = "executed",
        };
        doc.ApplicantPublicEntities.Add(new ApplicantPublicEntity { Name = "وزارة التعليم", Governorate = "دمشق" });
        doc.Guarantors.Add(new Guarantor { GuarantorNumber = 1, GuarantorName = "كفيل مستقل", GuarantorFamily = "عنابي" });
        doc.Heirs.Add(new Heir { HeirName = "وريث مستقل" });
        doc.ExecutedPublicEntities.Add(new ExecutedPublicEntity { EntityName = "وزارة التعليم", EntityNature = "public" });
        doc.ExecutedPublicEntities.Add(new ExecutedPublicEntity { EntityName = "هيئة أخرى", EntityNature = "public" });
        doc.Applicant = "وزارة التعليم - محافظة دمشق";
        // DocumentService يبني نص البحث عند الإنشاء؛ نمثّل حالته الابتدائية الغنية.
        doc.SearchText = DocumentSearchTextBuilder.Build(doc);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        Assert.Contains("كفيل مستقل", doc.SearchText);
        Assert.Contains("وريث مستقل", doc.SearchText);
        Assert.Contains("هيئة أخرى", doc.SearchText);

        var entry = await _service.CreateAsync(new CreatePublicEntityRequest("وزارة التعليم", "ministry", "دمشق", "الفرع الرئيسي"), ManagerActor());
        _audit.Actions.Clear();

        await _service.UpdateAsync(entry.Id,
            new UpdatePublicEntityRequest("وزارة التربية", null, null, null, null, null, null), ManagerActor());

        var after = await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == doc.Id);
        Assert.Contains("وزارة التربية", after.SearchText);
        Assert.Contains("كفيل مستقل", after.SearchText);
        Assert.Contains("وريث مستقل", after.SearchText);
        Assert.Contains("هيئة أخرى", after.SearchText);
        Assert.DoesNotContain("وزارة التعليم", after.SearchText);

        // إدخال تدقيق واحد للملف يجمع تغيّرَي الطالب والمنفذ المطابق.
        var syncLog = Assert.Single(_audit.ChangeLogs.Where(c => c.DocumentId == doc.Id));
        Assert.Contains(syncLog.Changes, c => c.FieldKey == nameof(Document.Applicant));
        Assert.Contains(syncLog.Changes, c => c.FieldKey == "__Col_ExecutedPublicEntities");
    }

    [Fact]
    public async Task Update_RenameByHead_OfOtherGovernorate_IsForbidden()
    {
        var entry = await _service.CreateAsync(new CreatePublicEntityRequest("وزارة النقل", "ministry", "حلب", "فرع حلب"), ManagerActor());
        var headDamascus = new EntityRegistryActor(_headDamascusId, "رئيس قسم دمشق", UserRole.Head, _damascusId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.UpdateAsync(entry.Id,
                new UpdatePublicEntityRequest("وزارة المواصلات", null, null, null, null, null, null), headDamascus));

        var unchanged = await _db.PublicEntityGroups.AsNoTracking().SingleAsync();
        Assert.Equal("وزارة النقل", unchanged.CanonicalName);
    }

    [Fact]
    public async Task Update_RenameByHeadWithinHisGovernorate_Succeeds()
    {
        var entry = await _service.CreateAsync(new CreatePublicEntityRequest("وزارة النقل", "ministry", "دمشق", "الفرع الرئيسي"), ManagerActor());
        var headDamascus = new EntityRegistryActor(_headDamascusId, "رئيس قسم دمشق", UserRole.Head, _damascusId);

        var updated = await _service.UpdateAsync(entry.Id,
            new UpdatePublicEntityRequest("وزارة المواصلات", null, null, null, null, null, null), headDamascus);

        Assert.Equal("وزارة المواصلات", updated.CanonicalName);
    }

    [Fact]
    public async Task Update_RenameToExistingCanonical_Throws()
    {
        await _service.CreateAsync(new CreatePublicEntityRequest("وزارة الداخلية", "ministry", "دمشق", "الفرع الرئيسي"), ManagerActor());
        var other = await _service.CreateAsync(new CreatePublicEntityRequest("وزارة الخارجية", "ministry", "دمشق", "الفرع الرئيسي"), ManagerActor());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateAsync(other.Id,
                new UpdatePublicEntityRequest("وزارة الداخلية", null, null, null, null, null, null), ManagerActor()));
    }

    [Fact]
    public async Task AddAlias_RejectsNormalizedDuplicate_ButAcceptsNew()
    {
        var entry = await _service.CreateAsync(new CreatePublicEntityRequest("وزارة التعليم", "ministry", "دمشق", "الفرع الرئيسي"), ManagerActor());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddAliasAsync(entry.Id, new AddPublicEntityAliasRequest("وزاره التعليم"), ManagerActor()));

        var updated = await _service.AddAliasAsync(entry.Id, new AddPublicEntityAliasRequest("التعليم العالي"), ManagerActor());

        Assert.Contains("التعليم العالي", updated.Aliases);
    }

    // ── نموذج الحوكمة الجديد: دخول فوري + مراجعة لاحقة ──

    [Fact]
    public async Task Create_ByLawyer_IsFinalImmediately_NeedsReview_AndNotifiesGovernorateHead()
    {
        var dto = await _service.CreateAsync(new CreatePublicEntityRequest(
            "هيئة التفتيش", "authority", "دمشق", "الفرع الرئيسي", CitationFormulaCatalog.AddToJob),
            LawyerActor());

        // تُعتمد أصوليًا نهائية فورًا وتظهر للربط والبوابة لحظيًا.
        Assert.Equal(EntityStatusCatalog.Final, dto.Status);
        Assert.True(dto.NeedsReview);

        var stored = await _db.PublicEntities.AsNoTracking().SingleAsync(e => e.Id == dto.Id);
        Assert.True(stored.NeedsReview);

        // تنبيه رئيس قسم دمشق فقط (محافظة القيد)، ولا تنبيه لرئيس حلب.
        var alerts = await _db.HeadAlerts.AsNoTracking().Include(a => a.Recipients).ToListAsync();
        var alert = Assert.Single(alerts);
        Assert.Equal(_headDamascusId, alert.Recipients.Single().UserId);
        Assert.Contains("محامي دمشق", alert.Message);
        Assert.Contains("هيئة التفتيش", alert.Message);
        Assert.Contains("يرجى مراجعتها", alert.Message);
    }

    [Fact]
    public async Task ApproveReview_ClearsFlagSilently_NoAlertToCreator()
    {
        var dto = await _service.CreateAsync(new CreatePublicEntityRequest(
            "هيئة التخطيط", "authority", "دمشق", "الفرع الرئيسي", CitationFormulaCatalog.AddToJob),
            LawyerActor());
        var headDamascus = new EntityRegistryActor(_headDamascusId, "رئيس قسم دمشق", UserRole.Head, _damascusId);

        var approved = await _service.ApproveReviewAsync(dto.Id, headDamascus);

        Assert.NotNull(approved);
        Assert.False(approved.NeedsReview);
        var stored = await _db.PublicEntities.AsNoTracking().SingleAsync(e => e.Id == dto.Id);
        Assert.False(stored.NeedsReview);
        Assert.NotNull(stored.ReviewedAtUtc);
        Assert.Equal(_headDamascusId, stored.ReviewedById);
        // الاعتماد كما هو بلا أي تعديل: لا إشعار للمُدخِل (حسب القرار).
        Assert.Equal(1, await _db.HeadAlerts.CountAsync(a => a.TargetType == HeadAlertTargetType.Branch));
        Assert.Equal(0, await _db.HeadAlerts.CountAsync(a => a.TargetType == HeadAlertTargetType.Lawyer));
        Assert.Contains("approve_entity_review", _audit.Actions);
    }

    [Fact]
    public async Task ApproveReview_ByHeadOfOtherGovernorate_IsForbidden()
    {
        var dto = await _service.CreateAsync(new CreatePublicEntityRequest(
            "هيئة الري", "authority", "حلب", "فرع حلب", CitationFormulaCatalog.AddToJob),
            LawyerActor());
        var headDamascus = new EntityRegistryActor(_headDamascusId, "رئيس قسم دمشق", UserRole.Head, _damascusId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ApproveReviewAsync(dto.Id, headDamascus));
    }

    [Fact]
    public async Task RenameDuringReview_ByHead_NotifiesCreatorLawyer_WithOldAndNewNames_AndClosesReview()
    {
        var dto = await _service.CreateAsync(new CreatePublicEntityRequest(
            "هيئة السوق", "authority", "دمشق", "الفرع الرئيسي", CitationFormulaCatalog.AddToJob),
            LawyerActor());
        var headDamascus = new EntityRegistryActor(_headDamascusId, "رئيس قسم دمشق", UserRole.Head, _damascusId);
        var alertsBefore = await _db.HeadAlerts.CountAsync();

        var edited = await _service.UpdateAsync(dto.Id,
            new UpdatePublicEntityRequest("هيئة السوق المركزية", null, null, null, null, null, null), headDamascus);

        Assert.Equal("هيئة السوق المركزية", edited.CanonicalName);
        Assert.False(edited.NeedsReview);

        var stored = await _db.PublicEntities.AsNoTracking().SingleAsync(e => e.Id == dto.Id);
        Assert.False(stored.NeedsReview);
        Assert.NotNull(stored.ReviewedAtUtc);

        // تنبيهان: تنبيه المراجعة الأول لرئيس القسم + إشعار تعديل الاسم للمحامي.
        Assert.Equal(alertsBefore + 1, await _db.HeadAlerts.CountAsync());
        var notice = await _db.HeadAlerts.AsNoTracking()
            .Include(a => a.Recipients)
            .SingleAsync(a => a.TargetType == HeadAlertTargetType.Lawyer);
        Assert.Equal(_lawyerId, notice.Recipients.Single().UserId);
        Assert.Contains("هيئة السوق", notice.Message);
        Assert.Contains("هيئة السوق المركزية", notice.Message);
    }

    [Fact]
    public async Task ReviewQueue_HeadSeesOnlyHisGovernorate_ManagerSeesAll()
    {
        await _service.CreateAsync(new CreatePublicEntityRequest("هيئة أ", "authority", "دمشق", "الفرع الرئيسي", CitationFormulaCatalog.AddToJob),
            LawyerActor());
        await _service.CreateAsync(new CreatePublicEntityRequest("هيئة ب", "authority", "حلب", "فرع حلب", CitationFormulaCatalog.AddToJob),
            LawyerActor());

        var headDamascus = new EntityRegistryActor(_headDamascusId, "رئيس قسم دمشق", UserRole.Head, _damascusId);
        var forDamascus = await _service.ListNeedsReviewAsync(headDamascus);
        var forManager = await _service.ListNeedsReviewAsync(ManagerActor());

        Assert.Single(forDamascus);
        Assert.Equal("هيئة أ", forDamascus[0].CanonicalName);
        Assert.Equal(2, forManager.Count);
        // مُدخلها ظاهر في بطاقة المراجعة.
        Assert.All(forManager, i => Assert.Equal("محامي دمشق", i.CreatedByName));
    }

    [Fact]
    public async Task Create_ByManager_DoesNotNeedReview()
    {
        var dto = await _service.CreateAsync(new CreatePublicEntityRequest("هيئة إدارية", "authority", "دمشق", "الفرع الرئيسي"),
            ManagerActor());

        Assert.False(dto.NeedsReview);
        Assert.Equal(0, await _db.HeadAlerts.CountAsync());
    }

    [Fact]
    public async Task ImportPreview_GroupsNormalizedVariants_WithCounts()
    {
        await SeedApplicantDocumentAsync("مديرية النقل", "دمشق");
        await SeedApplicantDocumentAsync("مديريه النقل", "دمشق");
        var executedDoc = new Document
        {
            BranchId = _damascusId, CreatedById = _lawyerId, BorrowerName = "س",
            AmountNumeric = 0, ExecStatus = string.Empty, GeneralEntitySide = "executed",
        };
        executedDoc.ExecutedPublicEntities.Add(new ExecutedPublicEntity
        { EntityName = "مديرية النقل", Governorate = "حلب", EntityNature = "public" });
        _db.Documents.Add(executedDoc);
        await _db.SaveChangesAsync();

        var preview = await _service.PreviewImportAsync();

        var item = Assert.Single(preview.Items);
        Assert.Equal(ArabicNameNormalizer.Normalize("مديرية النقل"), item.NormalizedName);
        Assert.Equal("مديرية النقل", item.SuggestedCanonicalName);
        Assert.Equal(3, item.TotalDocuments);
        Assert.Equal(new[] { "دمشق", "حلب" }, item.Governorates);
        Assert.Equal(3, item.Variants.Count);
    }

    [Fact]
    public async Task ImportCommit_CreatesFinalEntriesWithAliases_AndSkipsReCommit()
    {
        await SeedApplicantDocumentAsync("مديرية الاقتصاد الوطني", "دمشق");
        await SeedApplicantDocumentAsync("مديريه الاقتصاد الوطني", "دمشق");
        var preview = await _service.PreviewImportAsync();
        var candidate = preview.Items[0];

        // الاسم المعتمد المختار يخالف تطبيع الكتابات الأصلية، فتُسجَّل الكتابات أسماءً بديلة.
        var result = await _service.CommitImportAsync(new ImportCommitRequest(new[]
        {
            new ImportCommitItemRequest(candidate.NormalizedName, "مديرية النقل والاقتصاد", "administration",
                "دمشق", "الفرع الرئيسي", AddVariantsAsAliases: true),
        }), UserId(), "المدير");

        Assert.Equal(1, result.GroupsCreated);
        Assert.Equal(1, result.EntriesCreated);
        // الكتابتان تتطابقان بعد التطبيع (ة/ه)، فتُختزن كتابة واحدة بديلة لكل تطبيع مميز.
        Assert.Equal(1, result.AliasesAdded);

        var entry = await _db.PublicEntities
            .Include(e => e.Group).Include(e => e.Aliases)
            .AsNoTracking().SingleAsync();
        Assert.Equal(EntityStatusCatalog.Final, entry.Status);
        Assert.Equal("مديرية النقل والاقتصاد", entry.Group.CanonicalName);
        var alias = Assert.Single(entry.Aliases);
        Assert.Equal("مديرية الاقتصاد الوطني", alias.AliasText);
        Assert.Contains("import_entity_registry", _audit.Actions);

        // إعادة الاعتماد لنفس البند لا يكرر القيد (سلوك اتصالي آمن).
        var again = await _service.CommitImportAsync(new ImportCommitRequest(new[]
        {
            new ImportCommitItemRequest(candidate.NormalizedName, "مديرية النقل والاقتصاد", "administration",
                "دمشق", "الفرع الرئيسي"),
        }), UserId(), "المدير");
        Assert.Equal(0, again.EntriesCreated);
        Assert.Equal(1, await _db.PublicEntities.CountAsync());
    }

    [Fact]
    public async Task ImportCommit_UnknownNormalizedName_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CommitImportAsync(new ImportCommitRequest(new[]
            {
                new ImportCommitItemRequest("غير موجود", "غير موجود", "ministry", "دمشق", "الفرع الرئيسي"),
            }), UserId(), "المدير"));
    }

    // ── MoveEntry tests ──

    private async Task<(int groupId1, int entryId1, int groupId2, int entryId2)> SeedTwoGroupsForMoveAsync()
    {
        var dto1 = await _service.CreateAsync(new CreatePublicEntityRequest(
            "وزارة التعليم", "ministry", "دمشق", "الفرع الرئيسي"), ManagerActor());
        var dto2 = await _service.CreateAsync(new CreatePublicEntityRequest(
            "هيئة سكن", "authority", "دمشق", "فرع التجهيز"), ManagerActor());
        return (dto1.GroupId, dto1.Id, dto2.GroupId, dto2.Id);
    }

    private async Task<(int groupId1, int entryId1, int groupId2, int entryId2)> SeedTwoGroupsSameBranchForFoldAsync()
    {
        var dto1 = await _service.CreateAsync(new CreatePublicEntityRequest(
            "وزارة التعليم", "ministry", "دمشق", "الفرع الرئيسي"), ManagerActor());
        var dto2 = await _service.CreateAsync(new CreatePublicEntityRequest(
            "هيئة سكن", "authority", "دمشق", "الفرع الرئيسي"), ManagerActor());
        return (dto1.GroupId, dto1.Id, dto2.GroupId, dto2.Id);
    }

    private async Task SeedConflictEntryAsync(int groupId, string name, string governorate, string branch)
    {
        var group = await _db.PublicEntityGroups.FindAsync(groupId);
        var conflictEntry = new PublicEntity
        {
            GroupId = groupId,
            Group = group!,
            Governorate = governorate,
            BranchName = branch,
            Status = "final",
            IsActive = true,
            CreatedById = _managerId,
        };
        _db.PublicEntities.Add(conflictEntry);
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task MoveEntry_ModeA_ReassignsGroupId()
    {
        var (g1, e1, g2, _) = await SeedTwoGroupsForMoveAsync();
        var result = await _service.MoveEntryAsync(e1, new MoveEntryRequest(g2, null, null, null, null, null), ManagerActor());

        Assert.Equal(g2, result.ToGroupId);
        Assert.Equal(g1, result.FromGroupId);

        var entry = await _db.PublicEntities.FindAsync(e1);
        Assert.Equal(g2, entry!.GroupId);
        Assert.Contains("move_entity_registry", _audit.Actions);

        var changeEvent = await _db.PublicEntityChangeEvents.SingleAsync();
        Assert.Equal(ActionKindCatalog.Move, changeEvent.ActionKind);
        Assert.Equal(e1, changeEvent.EntryId);
    }

    [Fact]
    public async Task MoveEntry_FoldInto_MigratesRegistryLinksAndDeactivatesSource()
    {
        var (g1, e1, g2, e2) = await SeedTwoGroupsSameBranchForFoldAsync();
        var doc = await SeedApplicantDocumentAsync("وزارة التعليم", "دمشق");
        var row = await _db.ApplicantPublicEntities.SingleAsync(a => a.DocumentId == doc.Id);
        row.RegistryId = e1;
        await _db.SaveChangesAsync();

        var result = await _service.MoveEntryAsync(e1, new MoveEntryRequest(null, e2, null, null, null, null), ManagerActor());

        Assert.Equal(g2, result.ToGroupId);
        Assert.True(result.AffectedDocuments > 0);

        var sourceEntry = await _db.PublicEntities.FindAsync(e1);
        Assert.False(sourceEntry!.IsActive);

        var targetEntry = await _db.PublicEntities.Include(e => e.Aliases).SingleAsync(e => e.Id == e2);
        Assert.Contains(targetEntry.Aliases, a => a.AliasText == "وزارة التعليم");

        var updatedRow = await _db.ApplicantPublicEntities.FindAsync(row.Id);
        Assert.Equal(e2, updatedRow!.RegistryId);

        Assert.Contains("move_entity_registry", _audit.Actions);
    }

    [Fact]
    public async Task MoveEntry_NeedsReview_Throws()
    {
        var (g1, e1, g2, _) = await SeedTwoGroupsForMoveAsync();
        var entry = await _db.PublicEntities.FindAsync(e1);
        entry!.NeedsReview = true;
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MoveEntryAsync(e1, new MoveEntryRequest(g2, null, null, null, null, null), ManagerActor()));
    }

    [Fact]
    public async Task MoveEntry_ConflictInTargetGroup_Throws()
    {
        var (g1, e1, g2, _) = await SeedTwoGroupsForMoveAsync();
        await SeedConflictEntryAsync(g2, "هيئة سكن", "دمشق", "الفرع الرئيسي");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MoveEntryAsync(e1, new MoveEntryRequest(g2, null, null, null, null, null), ManagerActor()));
    }

    [Fact]
    public async Task MoveEntry_NoTarget_Throws()
    {
        var (g1, e1, g2, _) = await SeedTwoGroupsForMoveAsync();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MoveEntryAsync(e1, new MoveEntryRequest(null, null, null, null, null, null), ManagerActor()));
    }

    [Fact]
    public async Task MoveEntry_SendsHeadAlert()
    {
        var (g1, e1, g2, _) = await SeedTwoGroupsForMoveAsync();
        await _service.MoveEntryAsync(e1, new MoveEntryRequest(g2, null, null, null, null, null), ManagerActor());

        var alert = await _db.HeadAlerts.Include(h => h.Recipients).SingleAsync();
        Assert.Contains("أُلحق بقيدكم فرع", alert.Message);
        Assert.Contains(alert.Recipients, r => r.UserId == _headDamascusId);
    }

    [Fact]
    public async Task MoveAllEntries_ModeA_TransfersAllEntries()
    {
        var (g1, e1, g2, e2) = await SeedTwoGroupsForMoveAsync();
        var result = await _service.MoveAllEntriesAsync(
            new MoveAllEntriesRequest(g1, g2, null, null, null, null), ManagerActor());

        Assert.Equal(1, result.EntriesMoved);
        Assert.Equal(g2, result.TargetGroupId);

        var movedEntry = await _db.PublicEntities.FindAsync(e1);
        Assert.Equal(g2, movedEntry!.GroupId);

        Assert.Contains("move_all_entity_registry", _audit.Actions);
    }

    [Fact]
    public async Task MoveAllEntries_ConflictInTarget_Throws()
    {
        var (g1, e1, g2, _) = await SeedTwoGroupsForMoveAsync();
        await SeedConflictEntryAsync(g2, "هيئة سكن", "دمشق", "الفرع الرئيسي");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MoveAllEntriesAsync(new MoveAllEntriesRequest(g1, g2, null, null, null, null), ManagerActor()));
    }

    [Fact]
    public async Task MoveEntry_SelfMoveFold_Throws()
    {
        var (g1, e1, _, _) = await SeedTwoGroupsForMoveAsync();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MoveEntryAsync(e1, new MoveEntryRequest(null, e1, null, null, null, null), ManagerActor()));
    }

    [Fact]
    public async Task MoveEntry_NonExistentEntry_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MoveEntryAsync(9999, new MoveEntryRequest(1, null, null, null, null, null), ManagerActor()));
    }

    [Fact]
    public async Task MoveEntry_SameGroup_Throws()
    {
        var (g1, e1, _, _) = await SeedTwoGroupsForMoveAsync();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MoveEntryAsync(e1, new MoveEntryRequest(g1, null, null, null, null, null), ManagerActor()));
    }

    [Fact]
    public async Task MoveAllEntries_SameGroup_Throws()
    {
        var (g1, _, _, _) = await SeedTwoGroupsForMoveAsync();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MoveAllEntriesAsync(new MoveAllEntriesRequest(g1, g1, null, null, null, null), ManagerActor()));
    }

    [Fact]
    public async Task Create_WithCoverageLabel_StoresLabel()
    {
        var dto = await _service.CreateAsync(new CreatePublicEntityRequest(
            "وزارة الصحة", "ministry", "دمشق", "الفرع الرئيسي", CoverageLabel: "تغطية دمشق"), ManagerActor());

        Assert.Equal("تغطية دمشق", dto.CoverageLabel);
        var stored = await _db.PublicEntities.FindAsync(dto.Id);
        Assert.Equal("تغطية دمشق", stored!.CoverageLabel);
    }

    [Fact]
    public async Task Create_CoverageLabelExceeds150_Throws()
    {
        var longLabel = new string('أ', 151);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreatePublicEntityRequest(
                "هيئة جديدة", "authority", "دمشق", "الفرع الرئيسي", CoverageLabel: longLabel), ManagerActor()));
    }

    [Fact]
    public async Task Create_CoverageLabelMatchesGovernorate_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreatePublicEntityRequest(
                "هيئة جديدة", "authority", "دمشق", "الفرع الرئيسي", CoverageLabel: "دمشق"), ManagerActor()));
    }

    [Fact]
    public async Task Update_CoverageLabel_StoresLabel()
    {
        var dto = await _service.CreateAsync(new CreatePublicEntityRequest(
            "وزارة الصحة", "ministry", "دمشق", "الفرع الرئيسي"), ManagerActor());
        var updated = await _service.UpdateAsync(dto.Id, new UpdatePublicEntityRequest(
            null, null, null, null, null, null, null, CoverageLabel: "تغطية دمشق"), ManagerActor());
        Assert.Equal("تغطية دمشق", updated!.CoverageLabel);
    }

    [Fact]
    public async Task Update_CoverageLabelMatchesGovernorate_Throws()
    {
        var dto = await _service.CreateAsync(new CreatePublicEntityRequest(
            "هيئة جديدة", "authority", "دمشق", "الفرع الرئيسي"), ManagerActor());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateAsync(dto.Id, new UpdatePublicEntityRequest(
                null, null, null, null, null, null, null, CoverageLabel: "دمشق"), ManagerActor()));
    }

    [Fact]
    public async Task Create_CoverageLabelWithArabicDigits_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreatePublicEntityRequest(
                "هيئة جديدة", "authority", "دمشق", "الفرع الرئيسي", CoverageLabel: "تغطية ١٢٣"), ManagerActor()));
    }

    [Fact]
    public async Task Create_CoverageLabelTrimmed()
    {
        var dto = await _service.CreateAsync(new CreatePublicEntityRequest(
            "وزارة الثقافة", "ministry", "دمشق", "الفرع الرئيسي", CoverageLabel: "  تغطية ثقافة  "), ManagerActor());
        Assert.Equal("تغطية ثقافة", dto.CoverageLabel);
    }

    [Fact]
    public async Task MoveEntry_Fold_CreatesDocumentOccurrence()
    {
        var doc = await SeedApplicantDocumentAsync("وزارة التعليم", "دمشق");
        var (g1, e1, g2, e2) = await SeedTwoGroupsSameBranchForFoldAsync();
        var entry1 = await _db.PublicEntities.FindAsync(e1);
        doc.ApplicantPublicEntities.First().RegistryId = e1;
        await _db.SaveChangesAsync();

        await _service.MoveEntryAsync(e1, new MoveEntryRequest(g2, e2, null, null, null, null), ManagerActor());

        var occ = await _db.DocumentOccurrences.SingleAsync(o => o.DocumentId == doc.Id);
        Assert.Equal(OccurrenceTypeCatalog.EntityChange, occ.OccurrenceType);
        Assert.Contains("نقل", occ.Details!);
    }

    [Fact]
    public async Task MoveEntry_ModeA_CreatesDocumentOccurrence()
    {
        var doc = await SeedApplicantDocumentAsync("وزارة التعليم", "دمشق");
        var (g1, e1, g2, _) = await SeedTwoGroupsForMoveAsync();
        doc.ApplicantPublicEntities.First().RegistryId = e1;
        await _db.SaveChangesAsync();

        await _service.MoveEntryAsync(e1, new MoveEntryRequest(g2, null, null, null, null, null), ManagerActor());

        var occ = await _db.DocumentOccurrences.SingleAsync(o => o.DocumentId == doc.Id);
        Assert.Equal(OccurrenceTypeCatalog.EntityChange, occ.OccurrenceType);
        Assert.Contains("نقل", occ.Details!);
    }

    [Fact]
    public async Task MoveEntry_CreatesChangeEvent()
    {
        var (g1, e1, g2, _) = await SeedTwoGroupsForMoveAsync();
        await _service.MoveEntryAsync(e1, new MoveEntryRequest(g2, null, "admin_decision", "123", "2026/1/1", "ملاحظة"), ManagerActor());

        var evt = await _db.PublicEntityChangeEvents.SingleAsync();
        Assert.Equal(e1, evt.EntryId);
        Assert.Equal(ActionKindCatalog.Move, evt.ActionKind);
        Assert.Equal("admin_decision", evt.DecreeKind);
        Assert.Equal("123", evt.DecreeNumber);
        var payload = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(evt.PayloadJson);
        Assert.Equal("ملاحظة", payload.GetProperty("note").GetString());
    }

    [Fact]
    public async Task MoveAllEntries_CreatesChangeEvent()
    {
        var (g1, _, g2, _) = await SeedTwoGroupsForMoveAsync();
        await _service.MoveAllEntriesAsync(new MoveAllEntriesRequest(g1, g2, null, null, null, "نقل جماعي"), ManagerActor());

        var evt = await _db.PublicEntityChangeEvents.SingleAsync();
        Assert.Equal(ActionKindCatalog.Move, evt.ActionKind);
        var payload = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(evt.PayloadJson);
        Assert.Equal("نقل جماعي", payload.GetProperty("note").GetString());
    }
}
