using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

public class PublicEntityServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IPublicEntityService _service;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _damascusId;
    private readonly int _aleppoId;
    private readonly int _lawyerId;

    public PublicEntityServiceTests()
    {
        _db = TestDb.Create();
        _db.Branches.AddRange(
            new Branch { Name = "الفرع الرئيسي - دمشق", Code = "DAM", Governorate = "دمشق" },
            new Branch { Name = "فرع حلب", Code = "ALP", Governorate = "حلب" });
        _db.Users.AddRange(
            new User { Username = "head_dam", FullName = "رئيس قسم دمشق", Role = UserRole.Head, PasswordHash = "x" },
            new User { Username = "mgr", FullName = "المدير", Role = UserRole.Manager, PasswordHash = "x" });
        _db.SaveChanges();

        _damascusId = _db.Branches.Single(b => b.Code == "DAM").Id;
        _aleppoId = _db.Branches.Single(b => b.Code == "ALP").Id;
        var headDamascus = _db.Users.Single(u => u.Username == "head_dam");
        _db.Users.Add(new User { Username = "head_alp", FullName = "رئيس قسم حلب", Role = UserRole.Head, BranchId = _aleppoId, PasswordHash = "x" });
        var lawyer = new User { Username = "lawyer1", FullName = "محامي دمشق", Role = UserRole.Lawyer, BranchId = _damascusId, PasswordHash = "x" };
        _db.Users.Add(lawyer);
        _db.SaveChanges();
        _lawyerId = lawyer.Id;
        headDamascus.BranchId = _damascusId;
        _db.SaveChanges();

        _service = new PublicEntityService(
            new PublicEntityRepository(_db),
            new Repository<Branch>(_db),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit);
    }

    public void Dispose() => _db.Dispose();

    private EntityRegistryActor ManagerActor() =>
        new(UserId(), "المدير", UserRole.Manager, null);

    private int UserId() => _db.Users.Single(u => u.Username == "mgr").Id;

    private int HeadDamascusUserId() => _db.Users.Single(u => u.Username == "head_dam").Id;

    private int HeadAleppoUserId() => _db.Users.Single(u => u.Username == "head_alp").Id;

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
            Status = EntityStatusCatalog.Pending, CreatedById = UserId(),
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
    public async Task Update_RenameByHead_OfOtherGovernorate_IsForbidden()
    {
        var entry = await _service.CreateAsync(new CreatePublicEntityRequest("وزارة النقل", "ministry", "حلب", "فرع حلب"), ManagerActor());
        var headDamascus = new EntityRegistryActor(HeadDamascusUserId(), "رئيس قسم دمشق", UserRole.Head, _damascusId);

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
        var headDamascus = new EntityRegistryActor(HeadDamascusUserId(), "رئيس قسم دمشق", UserRole.Head, _damascusId);

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

    [Fact]
    public async Task Proposal_EntersPending_AndIsHiddenFromFinalSearch()
    {
        var proposal = await _service.CreateProposalAsync(new CreatePublicEntityProposalRequest(
            "هيئة التفتيش", "authority", "دمشق", "الفرع الرئيسي", CitationFormulaCatalog.AddToJob),
            _lawyerId, "محامي دمشق");

        Assert.Equal(ProposalStatus.Pending.ToString().ToLowerInvariant(), proposal.Status);
        Assert.Contains("propose_public_entity", _audit.Actions);

        // بوابة المندوبين تقرأ Final فقط؛ الاقتراح لا يظهر فيها قبل الاعتماد (د4).
        var portalView = await _service.ListAsync(new EntityRegistryListQuery("هيئة التفتيش", null, null, IncludePending: false, 1, 20));
        Assert.Equal(0, portalView.TotalCount);
    }

    [Fact]
    public async Task Proposal_ForAlreadyRegisteredEntity_Throws()
    {
        await _service.CreateAsync(new CreatePublicEntityRequest("هيئة المعايير", "authority", "دمشق", "الفرع الرئيسي"), ManagerActor());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateProposalAsync(new CreatePublicEntityProposalRequest(
                "هيئه المعايير", "authority", "دمشق", "الفرع الرئيسي", CitationFormulaCatalog.AddToJob),
                _lawyerId, "محامي"));
    }

    [Fact]
    public async Task Approve_CreatesFinalLinkedEntry_VisibleAfterwards()
    {
        var proposal = await _service.CreateProposalAsync(new CreatePublicEntityProposalRequest(
            "هيئة التخطيط", "authority", "دمشق", "الفرع الرئيسي", CitationFormulaCatalog.AddToPosition),
            _lawyerId, "محامي دمشق");
        var headDamascus = new EntityRegistryActor(HeadDamascusUserId(), "رئيس قسم دمشق", UserRole.Head, _damascusId);

        var approved = await _service.ApproveProposalAsync(proposal.Id, headDamascus);

        Assert.NotNull(approved);
        Assert.Equal(ProposalStatus.Approved.ToString().ToLowerInvariant(), approved.Status);
        Assert.True(approved.CreatedPublicEntityId > 0);

        var stored = await _db.PublicEntities.AsNoTracking().SingleAsync(e => e.Id == approved.CreatedPublicEntityId);
        Assert.Equal(EntityStatusCatalog.Final, stored.Status);
        Assert.Equal(CitationFormulaCatalog.AddToPosition, stored.CitationFormula);

        var visibleNow = await _service.ListAsync(new EntityRegistryListQuery("هيئة التخطيط", null, null, IncludePending: false, 1, 20));
        Assert.Equal(1, visibleNow.TotalCount);
        Assert.Contains("approve_entity_proposal", _audit.Actions);
    }

    [Fact]
    public async Task Approve_ByHeadOfOtherGovernorate_IsForbidden()
    {
        var proposal = await _service.CreateProposalAsync(new CreatePublicEntityProposalRequest(
            "هيئة الري", "authority", "حلب", "فرع حلب", CitationFormulaCatalog.AddToJob),
            _lawyerId, "محامي دمشق");
        var headDamascus = new EntityRegistryActor(HeadDamascusUserId(), "رئيس قسم دمشق", UserRole.Head, _damascusId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ApproveProposalAsync(proposal.Id, headDamascus));
    }

    [Fact]
    public async Task Reject_RequiresReason_AndStoresIt()
    {
        var proposal = await _service.CreateProposalAsync(new CreatePublicEntityProposalRequest(
            "هيئة السوق", "authority", "دمشق", "الفرع الرئيسي", CitationFormulaCatalog.AddToJob),
            _lawyerId, "محامي دمشق");
        var headDamascus = new EntityRegistryActor(HeadDamascusUserId(), "رئيس قسم دمشق", UserRole.Head, _damascusId);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RejectProposalAsync(proposal.Id, new RejectPublicEntityProposalRequest(" "), headDamascus));

        var rejected = await _service.RejectProposalAsync(proposal.Id, new RejectPublicEntityProposalRequest("جهة مكررة"), headDamascus);

        Assert.Equal(ProposalStatus.Rejected.ToString().ToLowerInvariant(), rejected.Status);
        Assert.Equal("جهة مكررة", rejected.RejectionReason);
        Assert.Null(rejected.CreatedPublicEntityId);
    }

    [Fact]
    public async Task PendingQueue_HeadSeesOnlyHisGovernorate()
    {
        await _service.CreateProposalAsync(new CreatePublicEntityProposalRequest(
            "هيئة أ", "authority", "دمشق", "الفرع الرئيسي", CitationFormulaCatalog.AddToJob), _lawyerId, "محامي");
        await _service.CreateProposalAsync(new CreatePublicEntityProposalRequest(
            "هيئة ب", "authority", "حلب", "فرع حلب", CitationFormulaCatalog.AddToJob), _lawyerId, "محامي");

        var headDamascus = new EntityRegistryActor(HeadDamascusUserId(), "رئيس قسم دمشق", UserRole.Head, _damascusId);
        var headAleppo = new EntityRegistryActor(HeadAleppoUserId(), "رئيس قسم حلب", UserRole.Head, _aleppoId);

        var forDamascus = await _service.ListPendingProposalsAsync(headDamascus);
        var forAleppo = await _service.ListPendingProposalsAsync(headAleppo);

        Assert.Single(forDamascus);
        Assert.Equal("هيئة أ", forDamascus[0].ProposedName);
        Assert.Single(forAleppo);
        Assert.Equal("هيئة ب", forAleppo[0].ProposedName);
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
        Assert.Equal("مديرية النقل", item.SuggestedCanonicalName); // الكتابة الأكثر تكرارًا (2 مقابل 1)
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
}
