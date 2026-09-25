using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

public class CorrespondenceServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly ICorrespondenceService _service;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _branchId;
    private readonly int _otherBranchId;
    private readonly int _entryId;
    private readonly int _groupId;
    private readonly int _outEntryId;
    private readonly User _head;
    private readonly User _otherBranchHead;
    private readonly User _lawyer1;
    private readonly User _lawyer2;
    private readonly User _lawyer3;
    private readonly User _manager;
    private readonly User _delegate;
    private readonly User _delegate2;
    private readonly User _groupBound;
    private readonly User _outDelegate;

    public CorrespondenceServiceTests()
    {
        _db = TestDb.Create();
        var branch = new Branch { Name = "دمشق", Code = "DAM", Governorate = "دمشق" };
        var otherBranch = new Branch { Name = "حلب", Code = "ALP", Governorate = "حلب" };
        _db.Branches.AddRange(branch, otherBranch);

        // نطاق جهة المندوب: قيد نهائي نشط بمحافظة دمشق.
        _db.Users.Add(new User { Username = "creator", FullName = "منشئ", Role = UserRole.Admin, PasswordHash = "x" });
        _db.SaveChanges();
        var group = new PublicEntityGroup { CanonicalName = "وزارة التعليم", EntityType = PublicEntityTypeCatalog.Ministry };
        group.Entries.Add(new PublicEntity { Governorate = "دمشق", BranchName = "الفرع الرئيسي", Status = EntityStatusCatalog.Final, CreatedById = 1 });
        _db.PublicEntityGroups.Add(group);
        var outGroup = new PublicEntityGroup { CanonicalName = "وزارة الصحة", EntityType = PublicEntityTypeCatalog.Ministry };
        outGroup.Entries.Add(new PublicEntity { Governorate = "حلب", BranchName = "الفرع الشرقي", Status = EntityStatusCatalog.Final, CreatedById = 1 });
        _db.PublicEntityGroups.Add(outGroup);
        _db.SaveChanges();
        _branchId = branch.Id;
        _otherBranchId = otherBranch.Id;
        _entryId = group.Entries.First().Id;
        _groupId = group.Id;
        _outEntryId = outGroup.Entries.First().Id;

        _head = NewUser("corr_head", "رئيس القسم", UserRole.Head, _branchId);
        _otherBranchHead = NewUser("corr_head_alp", "رئيس حلب", UserRole.Head, _otherBranchId);
        _lawyer1 = NewUser("corr_law1", "المحامي الأول", UserRole.Lawyer, _branchId);
        _lawyer2 = NewUser("corr_law2", "المحامي الثاني", UserRole.Lawyer, _branchId);
        _lawyer3 = NewUser("corr_law3", "المحامي الثالث", UserRole.Lawyer, _branchId);
        _manager = NewUser("corr_mgr", "المدير", UserRole.Manager, null);
        _delegate = NewUser("corr_del1", "مندوب الجهة", UserRole.EntityManager, null);
        _delegate2 = NewUser("corr_del2", "مندوب الجهة الثاني", UserRole.EntityManager, null);
        _groupBound = NewUser("corr_gdel", "مندوب الهوية الأم", UserRole.EntityManager, null);
        _outDelegate = NewUser("corr_outdel", "مندوب حلب", UserRole.EntityManager, null);
        _db.Users.AddRange(_head, _otherBranchHead, _lawyer1, _lawyer2, _lawyer3, _manager,
            _delegate, _delegate2, _groupBound, _outDelegate);
        _db.SaveChanges();
        _delegate.PortalEntryId = _entryId;
        _delegate2.PortalEntryId = _entryId;
        _groupBound.PortalGroupId = _groupId;
        _outDelegate.PortalEntryId = _outEntryId;
        _db.SaveChanges();

        _service = BuildService();
    }

    public void Dispose() => _db.Dispose();

    private CorrespondenceService BuildService()
    {
        return new CorrespondenceService(
            new CorrespondenceRepository(_db),
            new DocumentRepository(_db),
            new BranchRepository(_db),
            new UserRepository(_db),
            new AppealRepository(_db),
            new DelegationRepository(_db),
            new PortalRepository(_db),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit,
            new DbExceptionClassifier(),
            TimeProvider.System, TestClock.TimeZone);
    }

    private static User NewUser(string username, string fullName, UserRole role, int? branchId)
        => new()
        {
            Username = username,
            FullName = fullName,
            Role = role,
            BranchId = branchId,
            PasswordHash = new PasswordHasher().Hash("123456"),
        };

    private async Task<Document> AddDocumentAsync(User owner)
    {
        var doc = new Document
        {
            BranchId = _branchId,
            CreatedById = owner.Id,
            IsDraft = false,
            BorrowerName = "أحمد",
            BorrowerFather = "محمد",
            BorrowerFamily = "العلي",
            FileNumber = "77/2026",
            FileType = "تنفيذي",
            FileYear = "2026",
            Court = "دائرة تنفيذ دمشق",
            AmountNumeric = 0,
            ExecStatus = string.Empty,
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        return doc;
    }

    /// <summary>ربط الملف بقيد نطاق — شرط أهلية المراسلات المربوطة بالملف.</summary>
    private async Task LinkDocumentToEntryAsync(Document doc, int entryId)
    {
        doc.ExecutionApplicants.Add(new ExecutionApplicant
        {
            Name = "وزارة التعليم",
            ApplicantNature = PartyNatureCatalog.Legal,
            RegistryId = entryId,
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>إسناد محامٍ بإنابة على الملف ليصبح «متابعًا» (أحد «محامي الملف»).</summary>
    private async Task MakeDelegationFollowerAsync(Document doc, int lawyerId)
    {
        _db.DocumentDelegations.Add(new DocumentDelegation
        {
            SourceDocumentId = doc.Id,
            AssignedLawyerId = lawyerId,
            CreatedById = _lawyer1.Id,
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Create_GeneralFromLawyer_GeneratesNumberAndOriginalMessage()
    {
        var letter = await _service.CreateAsync(
            new CreateCorrespondenceRequest(null, _delegate.Id, Correspondence.ImportanceNormal, "<p>نطلب موافاتنا بالبيانات</p>"),
            _lawyer1.Id, "المحامي الأول", UserRole.Lawyer, _branchId);

        Assert.StartsWith("DAM-", letter.CorrespondenceNumber);
        Assert.Equal("دمشق", letter.Governorate);
        Assert.Equal(_branchId, letter.BranchId);
        Assert.Equal("normal", letter.Importance);
        Assert.Equal(_delegate.Id, letter.TargetUserId);
        Assert.Single(letter.Messages);
        Assert.Equal("letter", letter.Messages[0].Kind);
        Assert.False(letter.SeenByMe);
        Assert.Empty(letter.Receipts);
    }

    [Fact]
    public async Task Create_FileLinked_UsesDocumentBranchAndFileContext()
    {
        var doc = await AddDocumentAsync(_lawyer1);
        // الملف المربوط يقبل مناديب نطاقه فقط: نربطه بقيد المندوب المستلم.
        await LinkDocumentToEntryAsync(doc, _entryId);

        var letter = await _service.CreateAsync(
            new CreateCorrespondenceRequest(doc.Id, _delegate.Id, Correspondence.ImportanceUrgent, "<p>عاجل: زودونا بالمستندات</p>"),
            _lawyer1.Id, "المحامي الأول", UserRole.Lawyer, _branchId);

        Assert.StartsWith("DAM-", letter.CorrespondenceNumber);
        Assert.Equal(doc.Id, letter.DocumentId);
        Assert.NotNull(letter.FileContext);
        Assert.Contains("أحمد", letter.FileContext.ExecutedName);
        Assert.Equal("urgent", letter.Importance);
    }

    [Fact]
    public async Task Create_InvalidImportance_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(
            new CreateCorrespondenceRequest(null, _delegate.Id, "سوبر", "<p>نص</p>"),
            _lawyer1.Id, "المحامي الأول", UserRole.Lawyer, _branchId));
        Assert.Contains("الأهمية", ex.Message);
    }

    [Fact]
    public async Task Create_ToSelf_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(
            new CreateCorrespondenceRequest(null, _lawyer1.Id, "normal", "<p>نص</p>"),
            _lawyer1.Id, "المحامي الأول", UserRole.Lawyer, _branchId));
        Assert.Contains("لنفسك", ex.Message);
    }

    [Fact]
    public async Task Create_ToManager_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(
            new CreateCorrespondenceRequest(null, _manager.Id, "normal", "<p>نص</p>"),
            _lawyer1.Id, "المحامي الأول", UserRole.Lawyer, _branchId));
        Assert.Contains("المستلم", ex.Message);
    }

    [Fact]
    public async Task Create_ManagerRole_ThrowsUnauthorized()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateAsync(
            new CreateCorrespondenceRequest(null, _delegate.Id, "normal", "<p>نص</p>"),
            _manager.Id, "المدير", UserRole.Manager, null));
    }

    [Fact]
    public async Task Create_FileLinked_ByNonOwnerLawyer_Throws()
    {
        var doc = await AddDocumentAsync(_lawyer1);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateAsync(
            new CreateCorrespondenceRequest(doc.Id, _delegate.Id, "normal", "<p>نص</p>"),
            _lawyer2.Id, "المحامي الثاني", UserRole.Lawyer, _branchId));
    }

    [Fact]
    public async Task Create_GeneralFromDelegate_HasNullBranchAndEntityGovernorate()
    {
        var letter = await _service.CreateAsync(
            new CreateCorrespondenceRequest(null, _lawyer1.Id, "important", "<p>رد الجهة على طلبكم</p>"),
            _delegate.Id, "مندوب الجهة", UserRole.EntityManager, null);

        Assert.Null(letter.BranchId);
        Assert.Equal("دمشق", letter.Governorate);
        Assert.StartsWith("دمشق-", letter.CorrespondenceNumber);
        Assert.Equal("important", letter.Importance);
    }

    [Fact]
    public async Task Create_DelegateWithoutScope_Throws()
    {
        var orphan = NewUser("corr_orphan", "مندوب بلا نطاق", UserRole.EntityManager, null);
        _db.Users.Add(orphan);
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(
            new CreateCorrespondenceRequest(null, _lawyer1.Id, "normal", "<p>نص</p>"),
            orphan.Id, "مندوب بلا نطاق", UserRole.EntityManager, null));
        Assert.Contains("نطاق", ex.Message);
    }

    [Fact]
    public async Task Head_SeesBranchCorrespondence_ButNotOtherGovernorate()
    {
        await _service.CreateAsync(
            new CreateCorrespondenceRequest(null, _delegate.Id, "normal", "<p>للعلم</p>"),
            _lawyer1.Id, "المحامي الأول", UserRole.Lawyer, _branchId);

        var damascus = await _service.SearchAsync(
            _head.Id, UserRole.Head, _branchId, null, null, null, 1, 20);
        Assert.Equal(1, damascus.TotalCount);

        var aleppo = await _service.SearchAsync(
            _otherBranchHead.Id, UserRole.Head, _otherBranchId, null, null, null, 1, 20);
        Assert.Equal(0, aleppo.TotalCount);
    }

    [Fact]
    public async Task Manager_SearchRequiresGovernorate()
    {
        await _service.CreateAsync(
            new CreateCorrespondenceRequest(null, _delegate.Id, "normal", "<p>للعلم</p>"),
            _lawyer1.Id, "المحامي الأول", UserRole.Lawyer, _branchId);

        var blocked = await _service.SearchAsync(
            _manager.Id, UserRole.Manager, null, null, null, null, 1, 20);
        Assert.Equal(0, blocked.TotalCount);

        var allowed = await _service.SearchAsync(
            _manager.Id, UserRole.Manager, null, null, "دمشق", null, 1, 20);
        Assert.Equal(1, allowed.TotalCount);
    }

    [Fact]
    public async Task Addendum_OnlyCreator_AndReply_OnlyTarget()
    {
        var letter = await _service.CreateAsync(
            new CreateCorrespondenceRequest(null, _delegate.Id, "normal", "<p>الأصل</p>"),
            _lawyer1.Id, "المحامي الأول", UserRole.Lawyer, _branchId);

        var addendum = await _service.AddAddendumAsync(letter.Id,
            new AddCorrespondenceAddendumRequest("<p>لاحق المنشئ</p>"),
            _lawyer1.Id, "المحامي الأول");
        Assert.Equal("addendum", addendum.Kind);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.AddAddendumAsync(
            letter.Id, new AddCorrespondenceAddendumRequest("<p>لاحق الدخيل</p>"),
            _delegate.Id, "مندوب الجهة"));

        var reply = await _service.ReplyAsync(letter.Id,
            new ReplyCorrespondenceRequest("<p>رد المستلم</p>"),
            _delegate.Id, "مندوب الجهة");
        Assert.Equal("reply", reply.Kind);

        // رئيس القسم ليس طرفًا — لا يحق له الرد.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.ReplyAsync(
            letter.Id, new ReplyCorrespondenceRequest("<p>رد الرئيس</p>"),
            _head.Id, "رئيس القسم"));
    }

    [Fact]
    public async Task MarkSeen_DocumentsViewerOnce()
    {
        var letter = await _service.CreateAsync(
            new CreateCorrespondenceRequest(null, _delegate.Id, "urgent", "<p>عاجل</p>"),
            _lawyer1.Id, "المحامي الأول", UserRole.Lawyer, _branchId);

        var first = await _service.MarkSeenAsync(
            letter.Id, _delegate.Id, "مندوب الجهة", UserRole.EntityManager, null);
        var second = await _service.MarkSeenAsync(
            letter.Id, _delegate.Id, "مندوب الجهة", UserRole.EntityManager, null);

        Assert.Equal(first.SeenAt, second.SeenAt);

        var stored = await _service.GetByIdAsync(
            letter.Id, _delegate.Id, UserRole.EntityManager, null);
        Assert.True(stored.SeenByMe);
        Assert.Single(stored.Receipts);
        Assert.Equal("مندوب الجهة", stored.Receipts[0].UserName);
    }

    [Fact]
    public async Task UrgentBell_CountsOnlyUrgentUnseen_ForTargetOnly()
    {
        var urgent = await _service.CreateAsync(
            new CreateCorrespondenceRequest(null, _delegate.Id, "urgent", "<p>عاجل</p>"),
            _lawyer1.Id, "المحامي الأول", UserRole.Lawyer, _branchId);
        await _service.CreateAsync(
            new CreateCorrespondenceRequest(null, _delegate.Id, "normal", "<p>عادي</p>"),
            _lawyer1.Id, "المحامي الأول", UserRole.Lawyer, _branchId);

        Assert.Equal(1, await _service.CountUrgentUnseenAsync(_delegate.Id));
        // المنشئ يعرف محتواه — لا جرس له على ما سطّره.
        Assert.Equal(0, await _service.CountUrgentUnseenAsync(_lawyer1.Id));

        await _service.MarkSeenAsync(
            urgent.Id, _delegate.Id, "مندوب الجهة", UserRole.EntityManager, null);
        Assert.Equal(0, await _service.CountUrgentUnseenAsync(_delegate.Id));
    }

    [Fact]
    public async Task SearchTargets_FindsActivePartyMembersWithoutSelf()
    {
        var targets = await _service.SearchTargetsAsync(
            _lawyer1.Id, UserRole.Lawyer, _branchId, "مندوب الجهة", null, default);

        Assert.Equal(2, targets.Count);
        Assert.All(targets, t => Assert.NotEqual(_lawyer1.Id, t.UserId));
        var names = targets.Select(t => t.FullName).ToList();
        Assert.Contains("مندوب الجهة", names);
        Assert.Contains("مندوب الجهة الثاني", names);
    }

    [Fact]
    public async Task Delegate_SeesOnlyOwnDocumentCorrespondences()
    {
        var doc = await AddDocumentAsync(_lawyer1);
        // المراسلات المربوطة بملف تقتضي مستلمًا ضمن نطاق الملف: نربط الملف بقيد نطاق المندوبين أولًا.
        await LinkDocumentToEntryAsync(doc, _entryId);
        await _service.CreateAsync(
            new CreateCorrespondenceRequest(doc.Id, _delegate.Id, "normal", "<p>للأول</p>"),
            _lawyer1.Id, "المحامي الأول", UserRole.Lawyer, _branchId);
        await _service.CreateAsync(
            new CreateCorrespondenceRequest(doc.Id, _delegate2.Id, "normal", "<p>للثاني</p>"),
            _lawyer1.Id, "المحامي الأول", UserRole.Lawyer, _branchId);

        var mine = await _service.ListByDocumentAsync(
            doc.Id, _delegate.Id, UserRole.EntityManager, null);
        Assert.Single(mine);
        Assert.Equal("للأول", mine[0].Snippet);

        var headView = await _service.ListByDocumentAsync(
            doc.Id, _head.Id, UserRole.Head, _branchId);
        Assert.Equal(2, headView.Count);
    }

    [Fact]
    public async Task Create_FileLinkedOnBranchlessDoc_FallsBackToActorBranch()
    {
        var doc = await AddDocumentAsync(_lawyer1);
        doc.BranchId = null;
        await _db.SaveChangesAsync();
        // الملف المربوط يقبل مناديب نطاقه فقط: نربطه بقيد المندوب المستلم.
        await LinkDocumentToEntryAsync(doc, _entryId);

        var letter = await _service.CreateAsync(
            new CreateCorrespondenceRequest(doc.Id, _delegate.Id, "normal", "<p>على ملف قديم</p>"),
            _lawyer1.Id, "المحامي الأول", UserRole.Lawyer, _branchId);

        Assert.Equal(_branchId, letter.BranchId);
        Assert.Equal("دمشق", letter.Governorate);
        Assert.StartsWith("DAM-", letter.CorrespondenceNumber);
    }

    [Fact]
    public async Task Numbers_AreUniqueAcrossLetters()
    {
        var first = await _service.CreateAsync(
            new CreateCorrespondenceRequest(null, _delegate.Id, "normal", "<p>أول</p>"),
            _lawyer1.Id, "المحامي الأول", UserRole.Lawyer, _branchId);
        var second = await _service.CreateAsync(
            new CreateCorrespondenceRequest(null, _delegate.Id, "normal", "<p>ثانٍ</p>"),
            _lawyer1.Id, "المحامي الأول", UserRole.Lawyer, _branchId);

        Assert.NotEqual(first.CorrespondenceNumber, second.CorrespondenceNumber);
    }

    [Fact]
    public async Task SearchTargets_CapsResultsAtTwenty()
    {
        for (var i = 0; i < 25; i++)
            _db.Users.Add(NewUser($"corr_cap{i:00}", $"مرشح تجريبي {i:00}", UserRole.Lawyer, _branchId));
        await _db.SaveChangesAsync();

        var targets = await _service.SearchTargetsAsync(
            _lawyer1.Id, UserRole.Lawyer, _branchId, "مرشح تجريبي", null, default);

        Assert.Equal(20, targets.Count);
        Assert.All(targets, t => Assert.Contains("مرشح تجريبي", t.FullName));
    }

    [Fact]
    public async Task SearchTargets_General_RemainsOpenAcrossRoles()
    {
        var targets = await _service.SearchTargetsAsync(
            _lawyer1.Id, UserRole.Lawyer, _branchId, "قسم", null, default);

        var names = targets.Select(t => t.FullName).ToList();
        Assert.Contains("رئيس القسم", names);
    }

    [Fact]
    public async Task SearchTargets_DelegateOnFileLinked_ReturnsDocumentLawyersOnly()
    {
        var doc = await AddDocumentAsync(_lawyer1);
        await LinkDocumentToEntryAsync(doc, _entryId);

        var targets = await _service.SearchTargetsAsync(
            _delegate.Id, UserRole.EntityManager, null, "المحامي", doc.Id, default);

        var names = targets.Select(t => t.FullName).ToList();
        // مالك الملف فقط هو المؤهل؛ زميل غير منشئ وغير مسند (المحامي الثالث) مستبعد.
        Assert.Single(targets);
        Assert.Contains("المحامي الأول", names);
        Assert.DoesNotContain("المحامي الثالث", names);
        Assert.DoesNotContain("مندوب الجهة", names);
    }

    [Fact]
    public async Task SearchTargets_DelegateOnFileLinked_IncludesDelegationFollower()
    {
        var doc = await AddDocumentAsync(_lawyer1);
        await LinkDocumentToEntryAsync(doc, _entryId);
        await MakeDelegationFollowerAsync(doc, _lawyer2.Id);

        var targets = await _service.SearchTargetsAsync(
            _delegate.Id, UserRole.EntityManager, null, "المحامي", doc.Id, default);

var names = targets.Select(t => t.FullName).ToList();
        Assert.Contains("المحامي الأول", names);
        Assert.Contains("المحامي الثاني", names);
    }

    [Fact]
    public async Task SearchTargets_LawyerOnFileLinked_ReturnsScopeDelegatesOnly()
    {
        var doc = await AddDocumentAsync(_lawyer1);
        await LinkDocumentToEntryAsync(doc, _entryId);

        var targets = await _service.SearchTargetsAsync(
            _lawyer1.Id, UserRole.Lawyer, _branchId, "مندوب", doc.Id, default);

        var names = targets.Select(t => t.FullName).ToList();
        Assert.Equal(3, targets.Count);
        Assert.Contains("مندوب الجهة", names);
        Assert.Contains("مندوب الجهة الثاني", names);
        Assert.Contains("مندوب الهوية الأم", names);
        // مندوب خارج قيد الملف/مجموعته مستبعد، والرؤساء خارج النطاق مستبعدون.
        Assert.DoesNotContain("مندوب حلب", names);
        Assert.DoesNotContain("رئيس القسم", names);
    }

    [Fact]
    public async Task SearchTargets_FileLinked_OutOfScopeActor_Throws()
    {
        var doc = await AddDocumentAsync(_lawyer1);
        await LinkDocumentToEntryAsync(doc, _entryId);
        // المندوب مرتبط بقيد حلب — خارج نطاق ملف دمشق.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.SearchTargetsAsync(
            _outDelegate.Id, UserRole.EntityManager, null, "المحامي", doc.Id, default));
        // محامٍ غير منشئ وغير مسند لا يستطيع البحث عن مستلمي الملف.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.SearchTargetsAsync(
            _lawyer3.Id, UserRole.Lawyer, _branchId, "مندوب", doc.Id, default));
    }

    [Fact]
    public async Task SearchTargets_FileLinked_HeadBehavesLikeLawyer()
    {
        var doc = await AddDocumentAsync(_lawyer1);
        await LinkDocumentToEntryAsync(doc, _entryId);

        var targets = await _service.SearchTargetsAsync(
            _head.Id, UserRole.Head, _branchId, "مندوب", doc.Id, default);

        var names = targets.Select(t => t.FullName).ToList();
        Assert.Equal(3, targets.Count);
        Assert.Contains("مندوب الجهة", names);
        Assert.DoesNotContain("مندوب حلب", names);
    }

    [Fact]
    public async Task Create_FileLinkedByDelegate_ToUninvolvedLawyer_Throws()
    {
        var doc = await AddDocumentAsync(_lawyer1);
        await LinkDocumentToEntryAsync(doc, _entryId);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(
            new CreateCorrespondenceRequest(doc.Id, _lawyer3.Id, "normal", "<p>نص</p>"),
            _delegate.Id, "مندوب الجهة", UserRole.EntityManager, null));
        Assert.Contains("محامي الملف", ex.Message);
    }

    [Fact]
    public async Task Create_FileLinkedByLawyer_ToOutOfScopeDelegate_Throws()
    {
        var doc = await AddDocumentAsync(_lawyer1);
        await LinkDocumentToEntryAsync(doc, _entryId);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(
            new CreateCorrespondenceRequest(doc.Id, _outDelegate.Id, "normal", "<p>نص</p>"),
            _lawyer1.Id, "المحامي الأول", UserRole.Lawyer, _branchId));
        Assert.Contains("مندوب جهة", ex.Message);
    }

    [Fact]
    public async Task Create_FileLinkedByLawyer_ToHead_Throws()
    {
        var doc = await AddDocumentAsync(_lawyer1);
        await LinkDocumentToEntryAsync(doc, _entryId);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(
            new CreateCorrespondenceRequest(doc.Id, _head.Id, "normal", "<p>نص</p>"),
            _lawyer1.Id, "المحامي الأول", UserRole.Lawyer, _branchId));
        Assert.Contains("مندوب جهة", ex.Message);
    }

    [Fact]
    public async Task Create_FileLinkedByDelegate_ToDocumentOwner_Succeeds()
    {
        var doc = await AddDocumentAsync(_lawyer1);
        await LinkDocumentToEntryAsync(doc, _entryId);

        var letter = await _service.CreateAsync(
            new CreateCorrespondenceRequest(doc.Id, _lawyer1.Id, "normal", "<p>استفسار عن الملف</p>"),
            _delegate.Id, "مندوب الجهة", UserRole.EntityManager, null);

        Assert.Equal(doc.Id, letter.DocumentId);
        Assert.Equal(_lawyer1.Id, letter.TargetUserId);
        Assert.StartsWith("DAM-", letter.CorrespondenceNumber);
    }

    [Fact]
    public async Task SearchTargets_GeneralActors_ByQueryAndSelfExclusion_SameAsBefore()
    {
        // عام (بلا ملف): مندوب الجهة يبحث بحرية بين المحامين، والمحامون بين المناديب.
        var byDelegate = await _service.SearchTargetsAsync(
            _delegate.Id, UserRole.EntityManager, null, "المحامي", null, default);
        Assert.All(byDelegate, t => Assert.NotEqual(_delegate.Id, t.UserId));
        Assert.Contains(byDelegate, t => t.FullName == "المحامي الأول");
        Assert.DoesNotContain(byDelegate, t => t.FullName == "مندوب الجهة");
    }
}
