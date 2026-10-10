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
/// قلب قاعدة اعتماد الإنابات (§7.1–§7.7): الاعتماد لمالك الدائرة المنابة لا
/// المنيب — داخلية بدائرة لرئيس شعبته (وقسمها لرئيس قسمه)، وخارجية لرئيس قسم
/// الفرع المناب (أو شعبته بعد التوجيه)، مع الرفض للتصحيح والذرية.
/// قاعدة كل اختبار جديدة (TestDb) — بلا تلوث متبادل.
/// </summary>
public class SubHeadDelegationTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IDocumentDelegationService _service;
    private readonly FakeAuditLogger _audit = new();

    private readonly Branch _branch;
    private readonly Branch _otherBranch;
    private readonly User _lawyer1;
    private readonly User _lawyer2;
    private readonly User _head1;
    private readonly User _otherHead;
    private readonly User _otherLawyer;

    public SubHeadDelegationTests()
    {
        _db = TestDb.Create();

        _branch = new Branch { Name = "دمشق", Code = "DAM", Governorate = "دمشق" };
        _otherBranch = new Branch { Name = "اللاذقية", Code = "LAT", Governorate = "دمشق" };
        _db.Branches.AddRange(_branch, _otherBranch);
        _db.SaveChanges();

        _lawyer1 = User(_branch.Id, "lawyer1", "محامي دمشق");
        _lawyer2 = User(_branch.Id, "lawyer2", "محامي دمشق ثانٍ");
        _head1 = User(_branch.Id, "head1", "رئيس قسم دمشق", UserRole.Head);
        _otherHead = User(_otherBranch.Id, "head_lat", "رئيس قسم اللاذقية", UserRole.Head);
        _otherLawyer = User(_otherBranch.Id, "lawyer_lat", "محامي اللاذقية");
        _db.Users.AddRange(_lawyer1, _lawyer2, _head1, _otherHead, _otherLawyer);
        _db.SaveChanges();

        var documents = new DocumentRepository(_db);
        var users = new UserRepository(_db);
        var branches = new Repository<Branch>(_db);
        var registrationDates = new Repository<DocumentRegistrationDate>(_db);
        var occurrences = new Repository<DocumentOccurrence>(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        var headAlerts = new HeadAlertService(
            new HeadAlertRepository(_db),
            documents,
            users,
            branches,
            uow,
            tx,
            _audit);

        _service = new DocumentDelegationService(
            new DelegationRepository(_db),
            new DelegationReservationRepository(_db),
            new DbExceptionClassifier(),
            documents,
            users,
            branches,
            registrationDates,
            occurrences,
            uow,
            tx,
            _audit,
            headAlerts,
            TimeProvider.System,
            TestClock.TimeZone,
            circuits: new Repository<ExecutionCircuit>(_db),
            sections: new Repository<Section>(_db));
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

    private static int s_seq;

    private async Task<int> AddSectionAsync(string name, int branchId)
    {
        var section = new Section
        {
            Name = name,
            NameNorm = ArabicNameNormalizer.Normalize(name),
            BranchId = branchId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
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
            BranchId = branchId,
            SectionId = sectionId,
            Name = name,
            NameNorm = ArabicNameNormalizer.Normalize(name),
            IsActive = true,
            CreatedById = creatorId,
        };
        _db.ExecutionCircuits.Add(circuit);
        await _db.SaveChangesAsync();
        return circuit.Id;
    }

    private async Task<(Document Doc, int AssetId)> CreateSourceAsync()
    {
        var number = $"540{System.Threading.Interlocked.Increment(ref s_seq):D4}";
        var doc = new Document
        {
            CreatedById = _lawyer1.Id,
            BranchId = _branch.Id,
            BranchName = _branch.Name,
            GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
            IsDraft = false,
            BorrowerName = "أحمد",
            BorrowerFather = "خالد",
            BorrowerFamily = "الخطيب",
            AmountNumeric = 1_000_000,
            Currency = "ليرة سورية",
            ContractType = "عقد قرض",
            ContractNumber = "12/2024",
            Court = "دمشق",
            Applicant = "المدعي",
            FileNumber = number,
            FileYear = "2024",
            DocumentType = "متداول - أحمد خالد الخطيب",
            SearchText = $"أحمد الخطيب المدعي {number}",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        var asset = new Asset
        {
            DocumentId = doc.Id,
            AssetKind = AssetKindCatalog.RealEstate,
            PropertyNumber = "77",
            PropertyDistrict = "المزة",
            SeizureDate = new DateTime(2026, 8, 1),
        };
        _db.Assets.Add(asset);
        await _db.SaveChangesAsync();
        return (doc, asset.Id);
    }

    private static UpsertDelegationRequest CircuitRequest(int circuitId, int assetId) => new(
        DelegatedCourt: null,
        IsExternal: false,
        ExternalBranchId: null,
        DelegationDate: "1/8/2026",
        DelegationText: "الإنابة على العقار المذكور",
        DepositBookNumber: "كتاب-1",
        DepositBookDate: "2/8/2026",
        AssetIds: new List<int> { assetId },
        DelegatedCircuitId: circuitId);

    [Fact]
    public async Task Assign_SectionCircuit_BySubHead_Succeeds_HeadThrows()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var circuitId = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId, _head1.Id);
        var (doc, assetId) = await CreateSourceAsync();
        var created = await _service.CreateAsync(doc.Id, CircuitRequest(circuitId, assetId), _lawyer1.Id, "lawyer1");

        // رئيس القسم لا يملك اعتماد دائرة الشعبة.
        var headEx = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1"));
        Assert.Contains("نطاقك", headEx.Message);

        // رئيس الشعبة يعتمد بمحامٍ من فرعه، والمناب بفرع الدائرة ودائرتها.
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id), sub.Id, _branch.Id, "sub");
        Assert.NotNull(assigned);
        Assert.Equal(DelegationStatusCatalog.Assigned, assigned!.Status);
        var target = await _db.Documents.SingleAsync(d => d.Id == assigned.TargetDocumentId!.Value);
        Assert.Equal(_branch.Id, target.BranchId);
        Assert.Equal(circuitId, target.ExecutionCircuitId);
        Assert.Contains("assign_delegation", _audit.Actions);
    }

    [Fact]
    public async Task Assign_DivisionCircuit_ByHead_Succeeds_SubThrows()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var circuitId = await AddCircuitAsync("دائرة القسم", _branch.Id, null, _head1.Id);
        var (doc, assetId) = await CreateSourceAsync();
        var created = await _service.CreateAsync(doc.Id, CircuitRequest(circuitId, assetId), _lawyer1.Id, "lawyer1");

        var subEx = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id), sub.Id, _branch.Id, "sub"));
        Assert.Contains("نطاقك", subEx.Message);

        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1");
        Assert.NotNull(assigned);
        Assert.Equal(circuitId, (await _db.Documents.SingleAsync(d => d.Id == assigned!.TargetDocumentId!.Value)).ExecutionCircuitId);
    }

    [Fact]
    public async Task Assign_CrossBranchCircuit_ByCircuitBranchHead_TargetFollowsCircuit()
    {
        // دائرة فرع آخر بنفس المحافظة: الاعتماد لمالك الدائرة والمحامي من فرعها.
        var circuitId = await AddCircuitAsync("دائرة اللاذقية", _otherBranch.Id, null, _otherHead.Id);
        var (doc, assetId) = await CreateSourceAsync();
        var created = await _service.CreateAsync(doc.Id, CircuitRequest(circuitId, assetId), _lawyer1.Id, "lawyer1");

        // رئيس فرع المنيب لا يملكها رغم أن الملف له.
        var sourceHeadEx = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1"));
        Assert.Contains("فرعك", sourceHeadEx.Message);
        // ومحامي فرع المنيب مرفوض (قاعدة 375-378): المختص من فرع الدائرة.
        var lawyerEx = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id), _otherHead.Id, _otherBranch.Id, "head2"));
        Assert.Contains("الدائرة المنابة", lawyerEx.Message);

        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_otherLawyer.Id), _otherHead.Id, _otherBranch.Id, "head2");
        Assert.NotNull(assigned);
        var target = await _db.Documents.SingleAsync(d => d.Id == assigned!.TargetDocumentId!.Value);
        Assert.Equal(_otherBranch.Id, target.BranchId);
        Assert.Equal("اللاذقية", target.BranchName);
        Assert.Equal(circuitId, target.ExecutionCircuitId);
    }

    [Fact]
    public async Task Assign_LegacyNoCircuit_BySourceHead_Preserved()
    {
        // صف قديم بلا دائرة مرجعية (قبل السجل/مفكوك): قاعدته القديمة — رئيس فرع المنيب.
        var (doc, assetId) = await CreateSourceAsync();
        var delegation = new DocumentDelegation
        {
            SourceDocumentId = doc.Id,
            DelegatedCourt = "دائرة قديمة",
            DelegatedCourtNorm = ArabicNameNormalizer.Normalize("دائرة قديمة"),
            DelegatedCircuitId = null,
            IsExternal = false,
            Status = DelegationStatusCatalog.PendingHead,
            CreatedById = _lawyer1.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.DocumentDelegations.Add(delegation);
        await _db.SaveChangesAsync();

        var assigned = await _service.AssignAsync(delegation.Id, new AssignDelegationRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1");
        Assert.NotNull(assigned);
        var target = await _db.Documents.SingleAsync(d => d.Id == assigned!.TargetDocumentId!.Value);
        Assert.Equal(_branch.Id, target.BranchId);
        Assert.Null(target.ExecutionCircuitId);
    }

    [Fact]
    public async Task Assign_StaleVersion_ThrowsConflict()
    {
        var circuitId = await AddCircuitAsync("دائرة القسم", _branch.Id, null, _head1.Id);
        var (doc, assetId) = await CreateSourceAsync();
        var created = await _service.CreateAsync(doc.Id, CircuitRequest(circuitId, assetId), _lawyer1.Id, "lawyer1");

        await Assert.ThrowsAsync<DocumentConflictException>(() =>
            _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id, 9999), _head1.Id, _branch.Id, "head1"));
    }

    [Fact]
    public async Task Redirect_External_ToSection_SubAssigns_RecallRestores()
    {
        var sectionId = await AddSectionAsync("شعبة اللاذقية", _otherBranch.Id);
        var sub = await AddSubHeadAsync("sub_lat", _otherBranch.Id, sectionId);
        var (doc, assetId) = await CreateSourceAsync();
        var created = await _service.CreateAsync(doc.Id,
            new UpsertDelegationRequest("دائرة خارجية", true, _otherBranch.Id, "1/8/2026", "نص", "كتاب-1", "2/8/2026", new List<int> { assetId }),
            _lawyer1.Id, "lawyer1");

        // قبل التوجيه: رئيس القسم يراها والشعبة لا.
        Assert.Contains(await _service.ListPendingForHeadAsync(_otherBranch.Id), d => d.Id == created.Id);
        Assert.DoesNotContain(await _service.ListPendingForHeadAsync(_otherBranch.Id, sectionId), d => d.Id == created.Id);

        // التوجيه لرئيس الشعبة حصرًا بيد رئيس القسم.
        var subRedirectEx = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RedirectToSectionAsync(created.Id, new RedirectDelegationRequest(sectionId), sub.Id, "sub"));
        Assert.Contains("رئيس قسم", subRedirectEx.Message);
        var redirected = await _service.RedirectToSectionAsync(created.Id, new RedirectDelegationRequest(sectionId), _otherHead.Id, "head2");
        Assert.NotNull(redirected);
        Assert.Equal(sectionId, redirected!.RedirectedToSectionId);
        Assert.Contains("redirect_delegation", _audit.Actions);

        // بعد التوجيه: الشعبة تراها والقسم لا، والاعتماد لرئيس الشعبة.
        Assert.DoesNotContain(await _service.ListPendingForHeadAsync(_otherBranch.Id), d => d.Id == created.Id);
        Assert.Contains(await _service.ListPendingForHeadAsync(_otherBranch.Id, sectionId), d => d.Id == created.Id);
        var headAssignEx = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AssignAsync(created.Id, new AssignDelegationRequest(_otherLawyer.Id), _otherHead.Id, _otherBranch.Id, "head2"));
        Assert.Contains("نطاقك", headAssignEx.Message);
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_otherLawyer.Id), sub.Id, _otherBranch.Id, "sub");
        Assert.NotNull(assigned);
        Assert.Equal(_otherBranch.Id, (await _db.Documents.SingleAsync(d => d.Id == assigned!.TargetDocumentId!.Value)).BranchId);
    }

    [Fact]
    public async Task RecallRedirect_RestoresHeadScope()
    {
        var sectionId = await AddSectionAsync("شعبة اللاذقية", _otherBranch.Id);
        await AddSubHeadAsync("sub_lat", _otherBranch.Id, sectionId);
        var (doc, assetId) = await CreateSourceAsync();
        var created = await _service.CreateAsync(doc.Id,
            new UpsertDelegationRequest("دائرة خارجية", true, _otherBranch.Id, "1/8/2026", "نص", "كتاب-1", "2/8/2026", new List<int> { assetId }),
            _lawyer1.Id, "lawyer1");
        await _service.RedirectToSectionAsync(created.Id, new RedirectDelegationRequest(sectionId), _otherHead.Id, "head2");

        var recalled = await _service.RecallRedirectAsync(created.Id, _otherHead.Id, "head2");
        Assert.NotNull(recalled);
        Assert.Null(recalled!.RedirectedToSectionId);
        Assert.Contains(await _service.ListPendingForHeadAsync(_otherBranch.Id), d => d.Id == created.Id);
        Assert.DoesNotContain(await _service.ListPendingForHeadAsync(_otherBranch.Id, sectionId), d => d.Id == created.Id);
    }

    [Fact]
    public async Task Reject_WrongCircuit_BlocksAssign_LawyerCorrectionReleases()
    {
        var circuitId = await AddCircuitAsync("دائرة القسم", _branch.Id, null, _head1.Id);
        var (doc, assetId) = await CreateSourceAsync();
        var created = await _service.CreateAsync(doc.Id, CircuitRequest(circuitId, assetId), _lawyer1.Id, "lawyer1");

        // السبب إلزامي.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RejectAsync(created.Id, new RejectDelegationRequest(null), _head1.Id, "head1"));
        var rejected = await _service.RejectAsync(created.Id, new RejectDelegationRequest("الدائرة غير مختصة مكانيًا"), _head1.Id, "head1");
        Assert.NotNull(rejected);
        Assert.Equal("الدائرة غير مختصة مكانيًا", rejected!.RejectReason);

        // المرفوضة خارج الاعتماد الافتراضي، حاضرة بفلترها، والاعتماد مرفوض.
        Assert.DoesNotContain(await _service.ListPendingForHeadAsync(_branch.Id), d => d.Id == created.Id);
        var rejectedOnly = await _service.ListPendingForHeadAsync(_branch.Id, null, true);
        Assert.Contains(rejectedOnly, d => d.Id == created.Id);
        Assert.Equal(1, await _service.CountPendingForHeadAsync(_branch.Id, null, true));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1"));

        // بطاقة الملف تُظهر السبب للمحامي، وتصحيحه يحرّرها للاعتماد.
        var card = await _service.ListForDocumentAsync(doc.Id);
        Assert.Equal("الدائرة غير مختصة مكانيًا", Assert.Single(card).RejectReason);
        var updated = await _service.UpdateAsync(created.Id, CircuitRequest(circuitId, assetId), _lawyer1.Id, "lawyer1");
        Assert.Null(updated!.RejectReason);
        Assert.Contains(await _service.ListPendingForHeadAsync(_branch.Id), d => d.Id == created.Id);
    }
    [Fact]
    public async Task Create_CrossBranchCircuit_AlertTargetsCircuitBranchOwner()
    {
        // تنبيه التسطير العابر للفروع يُنشأ في فرع الدائرة (لا المنيب) لمالكه.
        var circuitId = await AddCircuitAsync("دائرة اللاذقية", _otherBranch.Id, null, _otherHead.Id);
        var (doc, assetId) = await CreateSourceAsync();
        var created = await _service.CreateAsync(doc.Id, CircuitRequest(circuitId, assetId), _lawyer1.Id, "lawyer1");

        var alert = await _db.HeadAlerts.Include(a => a.Recipients)
            .SingleAsync(a => a.DelegationId == created.Id);
        Assert.Equal(_otherBranch.Id, alert.BranchId);
        Assert.Equal(_otherHead.Id, Assert.Single(alert.Recipients).UserId);
    }

    [Fact]
    public async Task RecallRedirect_TargetsRecallingHead()
    {
        var sectionId = await AddSectionAsync("شعبة اللاذقية", _otherBranch.Id);
        await AddSubHeadAsync("sub_lat", _otherBranch.Id, sectionId);
        var (doc, assetId) = await CreateSourceAsync();
        var created = await _service.CreateAsync(doc.Id,
            new UpsertDelegationRequest("دائرة خارجية", true, _otherBranch.Id, "1/8/2026", "نص", "كتاب-1", "2/8/2026", new List<int> { assetId }),
            _lawyer1.Id, "lawyer1");
        await _service.RedirectToSectionAsync(created.Id, new RedirectDelegationRequest(sectionId), _otherHead.Id, "head2");
        await _service.RecallRedirectAsync(created.Id, _otherHead.Id, "head2");

        var alert = await _db.HeadAlerts.Include(a => a.Recipients)
            .SingleAsync(a => a.DelegationId == created.Id);
        Assert.Equal(_otherHead.Id, Assert.Single(alert.Recipients).UserId);
    }

    [Fact]
    public async Task Update_AfterRedirect_RebuildsHeadAlert()
    {
        // تصحيح المحامي بعد التوجيه: يُمحى التوجيه ويُعاد بناء التنبيه لرئيس
        // القسم — لا يبقى تنبيه الشعبة ولا يُحدَّث نص تنبيه آخر.
        var sectionId = await AddSectionAsync("شعبة اللاذقية", _otherBranch.Id);
        await AddSubHeadAsync("sub_lat", _otherBranch.Id, sectionId);
        var (doc, assetId) = await CreateSourceAsync();
        var created = await _service.CreateAsync(doc.Id,
            new UpsertDelegationRequest("دائرة خارجية", true, _otherBranch.Id, "1/8/2026", "نص", "كتاب-1", "2/8/2026", new List<int> { assetId }),
            _lawyer1.Id, "lawyer1");
        await _service.RedirectToSectionAsync(created.Id, new RedirectDelegationRequest(sectionId), _otherHead.Id, "head2");

        var updated = await _service.UpdateAsync(created.Id,
            new UpsertDelegationRequest("دائرة خارجية", true, _otherBranch.Id, "1/8/2026", "نص معدّل", "كتاب-1", "2/8/2026", new List<int> { assetId }),
            _lawyer1.Id, "lawyer1");

        Assert.Null(updated!.RedirectedToSectionId);
        var alert = await _db.HeadAlerts.Include(a => a.Recipients)
            .SingleAsync(a => a.DelegationId == created.Id);
        Assert.Equal(_otherHead.Id, Assert.Single(alert.Recipients).UserId);
        Assert.Contains("بانتظار اعتماد الإنابة", alert.Message);
    }

    [Fact]
    public async Task Update_AfterReject_ClearsReasonWithoutCorruptingAlerts()
    {
        var circuitId = await AddCircuitAsync("دائرة القسم", _branch.Id, null, _head1.Id);
        var (doc, assetId) = await CreateSourceAsync();
        var created = await _service.CreateAsync(doc.Id, CircuitRequest(circuitId, assetId), _lawyer1.Id, "lawyer1");
        await _service.RejectAsync(created.Id, new RejectDelegationRequest("الدائرة غير مختصة"), _head1.Id, "head1");

        var updated = await _service.UpdateAsync(created.Id, CircuitRequest(circuitId, assetId), _lawyer1.Id, "lawyer1");

        Assert.Null(updated!.RejectReason);
        var alerts = await _db.HeadAlerts.Include(a => a.Recipients)
            .Where(a => a.DelegationId == created.Id).ToListAsync();
        var alert = Assert.Single(alerts);
        Assert.Equal(HeadAlertTargetType.Head, alert.TargetType);
        Assert.Contains("بانتظار اعتماد الإنابة", alert.Message);
        Assert.DoesNotContain("غير مختصة", alert.Message);
    }

    [Fact]
    public async Task Reject_Twice_Throws_And_RecallOnRejected_Throws()
    {
        var circuitId = await AddCircuitAsync("دائرة القسم", _branch.Id, null, _head1.Id);
        var (doc, assetId) = await CreateSourceAsync();
        var created = await _service.CreateAsync(doc.Id, CircuitRequest(circuitId, assetId), _lawyer1.Id, "lawyer1");
        await _service.RejectAsync(created.Id, new RejectDelegationRequest("سبب أول"), _head1.Id, "head1");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RejectAsync(created.Id, new RejectDelegationRequest("سبب ثانٍ"), _head1.Id, "head1"));
    }

    [Fact]
    public async Task RedirectReject_StaleVersion_ThrowsConflict()
    {
        var sectionId = await AddSectionAsync("شعبة اللاذقية", _otherBranch.Id);
        await AddSubHeadAsync("sub_lat", _otherBranch.Id, sectionId);
        var (doc, assetId) = await CreateSourceAsync();
        var created = await _service.CreateAsync(doc.Id,
            new UpsertDelegationRequest("دائرة خارجية", true, _otherBranch.Id, "1/8/2026", "نص", "كتاب-1", "2/8/2026", new List<int> { assetId }),
            _lawyer1.Id, "lawyer1");

        await Assert.ThrowsAsync<DocumentConflictException>(() =>
            _service.RedirectToSectionAsync(created.Id, new RedirectDelegationRequest(sectionId, 9999), _otherHead.Id, "head2"));
        await Assert.ThrowsAsync<DocumentConflictException>(() =>
            _service.RejectAsync(created.Id, new RejectDelegationRequest("سبب", 9999), _otherHead.Id, "head2"));
    }

    [Fact]
    public async Task Mutations_BumpVersion()
    {
        // كل كتابة ترفع الرمز — حماية ثنائية الاتجاه للسباق.
        var circuitId = await AddCircuitAsync("دائرة القسم", _branch.Id, null, _head1.Id);
        var (doc, assetId) = await CreateSourceAsync();
        var created = await _service.CreateAsync(doc.Id, CircuitRequest(circuitId, assetId), _lawyer1.Id, "lawyer1");

        var updated = await _service.UpdateAsync(created.Id, CircuitRequest(circuitId, assetId), _lawyer1.Id, "lawyer1");
        Assert.Equal(created.Version + 1, updated!.Version);
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1");
        Assert.Equal(updated.Version + 1, assigned!.Version);
    }

    [Fact]
    public async Task PendingScope_HeadExcludesSection_SubSeesOwnOnly()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var divisionCircuit = await AddCircuitAsync("دائرة القسم", _branch.Id, null, _head1.Id);
        var sectionCircuit = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId, _head1.Id);
        var (docA, assetA) = await CreateSourceAsync();
        var (docB, assetB) = await CreateSourceAsync();
        var appealA = await _service.CreateAsync(docA.Id, CircuitRequest(divisionCircuit, assetA), _lawyer1.Id, "lawyer1");
        var appealB = await _service.CreateAsync(docB.Id, CircuitRequest(sectionCircuit, assetB), _lawyer1.Id, "lawyer1");

        var headPending = await _service.ListPendingForHeadAsync(_branch.Id);
        Assert.Contains(headPending, d => d.Id == appealA.Id);
        Assert.DoesNotContain(headPending, d => d.Id == appealB.Id);

        var subPending = await _service.ListPendingForHeadAsync(_branch.Id, sectionId);
        Assert.Contains(subPending, d => d.Id == appealB.Id);
        Assert.DoesNotContain(subPending, d => d.Id == appealA.Id);
        Assert.Equal(1, await _service.CountPendingForHeadAsync(_branch.Id, sectionId));
        Assert.Equal(0, await _service.CountPendingForHeadAsync(_branch.Id, sectionId, true));
    }
}
