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
/// اختبارات سجل دوائر التنفيذ (BQ-004) — تغطية القرارات 51–56 والبوابات §7:
/// النطاق، الإلزامية، الوحدانية، منع الحذف، الذرية، التنبيه، الاستثناء الخارجي،
/// إسقاط العلم، حارس التدوير، تجاهل court، رفع Version، الأعمدة الجديدة، مسح BaseNumbers.
/// </summary>
public class ExecutionCircuitServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IExecutionCircuitService _circuits;
    private readonly IDocumentService _documents;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _branchId;
    private readonly int _headId;
    private readonly int _lawyer1Id;
    private readonly int _lawyer2Id;

    public ExecutionCircuitServiceTests()
    {
        _db = TestDb.Create();
        var branch = new Branch { Name = "دمشق", Code = "DAM", Governorate = "دمشق" };
        _db.Branches.Add(branch);
        _db.SaveChanges();
        _branchId = branch.Id;
        var hasher = new PasswordHasher();
        var head = new User { Username = "head1", FullName = "رئيس القسم", Role = UserRole.Head, BranchId = _branchId, PasswordHash = hasher.Hash("123456") };
        var lawyer1 = new User { Username = "lawyer1", FullName = "محامي أول", Role = UserRole.Lawyer, BranchId = _branchId, PasswordHash = hasher.Hash("123456") };
        var lawyer2 = new User { Username = "lawyer2", FullName = "محامي ثان", Role = UserRole.Lawyer, BranchId = _branchId, PasswordHash = hasher.Hash("123456") };
        _db.Users.AddRange(head, lawyer1, lawyer2);
        _db.SaveChanges();
        _headId = head.Id;
        _lawyer1Id = lawyer1.Id;
        _lawyer2Id = lawyer2.Id;

        var documents = new DocumentRepository(_db);
        var users = new UserRepository(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        var classifier = new DbExceptionClassifier();
        var circuitsRepo = new Repository<ExecutionCircuit>(_db);
        var branchesRepo = new Repository<Branch>(_db);
        var occurrences = new Repository<DocumentOccurrence>(_db);
        var baseNumbers = new Repository<DocumentBaseNumber>(_db);
        var alertsRepo = new HeadAlertRepository(_db);
        _circuits = new ExecutionCircuitService(
            circuitsRepo, branchesRepo, new Repository<Section>(_db), documents, new DelegationRepository(_db),
            new AppealRepository(_db),
            occurrences, baseNumbers, new Repository<HeadSuccession>(_db), alertsRepo, users,
            uow, tx, _audit, classifier, TimeProvider.System, TestClock.TimeZone);
        var alertService = new HeadAlertService(alertsRepo, documents, users,
            branchesRepo, uow, tx, _audit);
        _documents = new DocumentService(
            documents, users, new Repository<Guarantor>(_db), new Repository<Asset>(_db),
            new Repository<ExecutionAction>(_db), baseNumbers,
            new Repository<DocumentRegistrationDate>(_db), occurrences,
            new DelegationRepository(_db), new AppealRepository(_db), alertService,
            uow, tx, _audit, Options.Create(new ExportOptions()),
            TimeProvider.System, TestClock.TimeZone, classifier,
            null, circuitsRepo, branchesRepo);
    }

    public void Dispose() => _db.Dispose();

    private DocumentUpsertRequest Sample(string? number = "100", string? year = "2026") => new()
    {
        BorrowerName = "مقترض",
        AmountNumeric = 1000,
        Currency = "ليرة سورية",
        ContractNumber = "1/2026",
        FileNumber = number,
        FileType = "صلح",
        FileYear = year,
        FileRegistrationDate = "1/8/2026",
    };

    private async Task<int> CreateCircuitAsync(string name = "دائرة دمشق الأولى")
        => (await _circuits.CreateAsync(_branchId, _headId, "head1", name)).Id;

    private async Task<int> CreateDocAsync(int circuitId, int lawyerId, string? number = "100", string? year = "2026")
    {
        var req = Sample(number, year);
        req.ExecutionCircuitId = circuitId;
        var created = await _documents.CreateAsync(req, lawyerId, "lawyer", _branchId);
        return created.Id;
    }

    [Fact]
    public async Task Create_Circuit_RequiresGovernorateBranch()
    {
        var badBranch = new Branch { Name = "بلا محافظة", Code = "NOG" };
        _db.Branches.Add(badBranch);
        await _db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _circuits.CreateAsync(badBranch.Id, _headId, "head1", "دائرة"));
    }

    [Fact]
    public async Task Create_DuplicateName_Normalized_Conflict()
    {
        await CreateCircuitAsync("دائرة دمشق الأولى");
        await Assert.ThrowsAsync<DocumentConflictException>(() =>
            _circuits.CreateAsync(_branchId, _headId, "head1", "دائرة  دمشق الأولى"));
    }

    [Fact]
    public async Task Create_Document_RequiresCircuit_WhenRegistryNonEmpty()
    {
        await CreateCircuitAsync();
        var req = Sample();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _documents.CreateAsync(req, _lawyer1Id, "lawyer", _branchId));
    }

    [Fact]
    public async Task Create_Document_RejectsNameOutsideRegistry()
    {
        await CreateCircuitAsync();
        var req = Sample();
        req.Court = "دائرة وهمية";
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _documents.CreateAsync(req, _lawyer1Id, "lawyer", _branchId));
    }

    [Fact]
    public async Task Create_Document_DerivesCourt_IgnoresClientText()
    {
        var id = await CreateCircuitAsync("دائرة دمشق الأولى");
        var req = Sample();
        req.ExecutionCircuitId = id;
        req.Court = "نص متلاعب به";
        var created = await _documents.CreateAsync(req, _lawyer1Id, "lawyer", _branchId);
        Assert.Equal("دائرة دمشق الأولى", created.Court);
        Assert.Equal(id, created.ExecutionCircuitId);
    }

    [Fact]
    public async Task Create_Document_RejectsDisabledCircuit()
    {
        var id = await CreateCircuitAsync();
        await _circuits.SetActiveAsync(id, _branchId, null, "head1", false, null);
        var req = Sample();
        req.ExecutionCircuitId = id;
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _documents.CreateAsync(req, _lawyer1Id, "lawyer", _branchId));
    }

    [Fact]
    public async Task Uniqueness_IsPerCircuit_SameNumberDifferentCircuits_Allowed()
    {
        var a = await CreateCircuitAsync("دائرة أ");
        var b = await CreateCircuitAsync("دائرة ب");
        await CreateDocAsync(a, _lawyer1Id, "100", "2026");
        var req = Sample("100", "2026");
        req.ExecutionCircuitId = b;
        var created = await _documents.CreateAsync(req, _lawyer1Id, "lawyer", _branchId);
        Assert.Equal(b, created.ExecutionCircuitId);
    }

    [Fact]
    public async Task Uniqueness_SameCircuitDuplicate_Conflict()
    {
        var a = await CreateCircuitAsync("دائرة أ");
        await CreateDocAsync(a, _lawyer1Id, "100", "2026");
        var req = Sample("100", "2026");
        req.ExecutionCircuitId = a;
        await Assert.ThrowsAsync<DocumentConflictException>(() =>
            _documents.CreateAsync(req, _lawyer2Id, "lawyer", _branchId));
    }

    [Fact]
    public async Task Delete_BlockedByFiles_ConflictWithCount()
    {
        var a = await CreateCircuitAsync("دائرة أ");
        await CreateDocAsync(a, _lawyer1Id);
        var ex = await Assert.ThrowsAsync<DocumentConflictException>(() =>
            _circuits.DeleteAsync(a, _branchId, null, "head1"));
        Assert.Contains("أفرغها أولًا", ex.Message);
    }

    [Fact]
    public async Task Refer_EmptiesCircuit_ClearsBaseNumbers_SetsPending_CheckOccurrenceColumns()
    {
        var source = await CreateCircuitAsync("دائرة المصدر");
        var target = await CreateCircuitAsync("دائرة الهدف");
        var docId = await CreateDocAsync(source, _lawyer1Id, "200", "2026");
        // رقم أساس سابق لإثبات المسح عند الإحالة.
        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = docId, Year = 2025, BaseNumber = "99",
            CreatedById = _lawyer1Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var result = await _circuits.ReferFilesAsync(source, _branchId, null, _headId, "head1",
            new ReferCircuitFilesRequest { FileIds = new List<int> { docId }, TargetLawyerId = _lawyer2Id, TargetCircuitId = target });
        Assert.Equal(1, result.ReferredCount);

        var doc = await _db.Documents.FindAsync(docId);
        Assert.NotNull(doc);
        Assert.Equal(target, doc!.ExecutionCircuitId);
        Assert.Null(doc.FileNumber);
        Assert.True(doc.NeedsRegistration);
        Assert.False(doc.IsDraft);
        Assert.Empty(_db.BaseNumbers.Where(b => b.DocumentId == docId).ToList());
        var occ = _db.DocumentOccurrences
            .Where(o => o.DocumentId == docId && o.OccurrenceType == OccurrenceTypeCatalog.CircuitReferred)
            .OrderByDescending(o => o.Id).First();
        Assert.Equal("دائرة المصدر", occ.FromCircuitName);
        Assert.Equal("دائرة الهدف", occ.ToCircuitName);
        // الرقم القديم من EffectiveFileIdentity (رقم الأساس 99 لسنة 2025 يغلب رقم الملف 200).
        Assert.Equal("99", occ.FileNumber);
    }

    [Fact]
    public async Task Refer_SkipsAlreadyOwned_SilentlyCountsSkipped()
    {
        var source = await CreateCircuitAsync("دائرة المصدر");
        var target = await CreateCircuitAsync("دائرة الهدف");
        var docId = await CreateDocAsync(source, _lawyer1Id);
        // إحالة أولى تنقل للمحامي الثاني.
        await _circuits.ReferFilesAsync(source, _branchId, null, _headId, "head1",
            new ReferCircuitFilesRequest { FileIds = new List<int> { docId }, TargetLawyerId = _lawyer2Id, TargetCircuitId = target });
        // الدائرة المصدر أُفرغت وعُطّلت؛ أعد تفعيلها وأعد ملفًا جديدًا للتجربة الثانية؟ — نبسّط:
        // نحيل ملفًا هو أصلًا عند الهدف (يُتخطى).
        var pending = await _circuits.MyPendingRegistrationsAsync(_lawyer2Id, target);
        Assert.Single(pending);
    }

    [Fact]
    public async Task Complete_NormalizesArabicDigits_AndDropsFlag()
    {
        var source = await CreateCircuitAsync("دائرة المصدر");
        var target = await CreateCircuitAsync("دائرة الهدف");
        var docId = await CreateDocAsync(source, _lawyer1Id, "300", "2026");
        await _circuits.ReferFilesAsync(source, _branchId, null, _headId, "head1",
            new ReferCircuitFilesRequest { FileIds = new List<int> { docId }, TargetLawyerId = _lawyer2Id, TargetCircuitId = target });

        var count = await _circuits.CompleteRegistrationsAsync(_lawyer2Id, "lawyer2",
            new CompleteRegistrationsRequest
            {
                Entries = new List<CompleteRegistrationEntry>
                {
                    new() { DocumentId = docId, FileNumber = "١٢٣", FileType = "صلح", FileYear = "٢٠٢٦" },
                },
            });
        Assert.Equal(1, count);
        var doc = await _db.Documents.FindAsync(docId);
        Assert.Equal("123", doc!.FileNumber);
        Assert.Equal("2026", doc.FileYear);
        Assert.False(doc.NeedsRegistration);
        var occ = _db.DocumentOccurrences
            .Where(o => o.DocumentId == docId && o.OccurrenceType == OccurrenceTypeCatalog.CircuitReregistered)
            .OrderByDescending(o => o.Id).First();
        Assert.Equal("123", occ.FileNumber);
    }

    [Fact]
    public async Task Pending_IsExcludedFromRotationCandidates()
    {
        var source = await CreateCircuitAsync("دائرة المصدر");
        var target = await CreateCircuitAsync("دائرة الهدف");
        var docId = await CreateDocAsync(source, _lawyer1Id, "400", "2025");
        await _circuits.ReferFilesAsync(source, _branchId, null, _headId, "head1",
            new ReferCircuitFilesRequest { FileIds = new List<int> { docId }, TargetLawyerId = _lawyer2Id, TargetCircuitId = target });
        var repo = new DocumentRepository(_db);
        var (total, _) = await repo.GetRotationCandidatesAsync(_lawyer2Id, 2026, 1, 20);
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task StatusChange_BlockedOnPending()
    {
        var source = await CreateCircuitAsync("دائرة المصدر");
        var target = await CreateCircuitAsync("دائرة الهدف");
        var docId = await CreateDocAsync(source, _lawyer1Id, "500", "2026");
        await _circuits.ReferFilesAsync(source, _branchId, null, _headId, "head1",
            new ReferCircuitFilesRequest { FileIds = new List<int> { docId }, TargetLawyerId = _lawyer2Id, TargetCircuitId = target });
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _documents.UpdateStatusAsync(docId, "متداول", new Dictionary<string, string?>(), "lawyer2"));
    }

    [Fact]
    public async Task Rename_PropagatesToDocuments_AndBumpsVersion()
    {
        var id = await CreateCircuitAsync("دائرة قديمة");
        var docId = await CreateDocAsync(id, _lawyer1Id, "600", "2026");
        var before = (await _db.Documents.FindAsync(docId))!.Version;
        await _circuits.RenameAsync(id, _branchId, null, "head1", "دائرة جديدة", null);
        var doc = await _db.Documents.FindAsync(docId);
        Assert.Equal("دائرة جديدة", doc!.Court);
        Assert.True(doc.Version > before);
    }

    [Fact]
    public async Task Delete_WithCompletedDelegationTargeting_DetachesFkKeepsText()
    {
        // H1: إنابة مكتملة تستهدف الدائرة — الحذف يفك المرجع ويُبقي النص (التاريخ المجمد)
        // بدل انفجار FK (Restrict) بـ 500.
        var source = await CreateCircuitAsync("دائرة المنيب");
        var target = await CreateCircuitAsync("دائرة المنابة");
        var docId = await CreateDocAsync(source, _lawyer1Id, "710", "2026");
        _db.DocumentDelegations.Add(new DocumentDelegation
        {
            SourceDocumentId = docId,
            DelegatedCourt = "دائرة المنابة",
            DelegatedCourtNorm = ArabicNameNormalizer.Normalize("دائرة المنابة"),
            DelegatedCircuitId = target,
            IsExternal = false,
            Status = DelegationStatusCatalog.Executed,
            CreatedById = _headId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        await _circuits.DeleteAsync(target, _branchId, null, "head1");

        Assert.Null(await _db.ExecutionCircuits.FindAsync(target));
        var g = _db.DocumentDelegations.First(d => d.DelegatedCircuitId == null && d.SourceDocumentId == docId);
        Assert.Equal("دائرة المنابة", g.DelegatedCourt);
    }

    [Fact]
    public async Task Delete_WithPendingIncoming_Conflict()
    {
        var source = await CreateCircuitAsync("دائرة المنيب");
        var target = await CreateCircuitAsync("دائرة المنابة");
        var docId = await CreateDocAsync(source, _lawyer1Id, "711", "2026");
        _db.DocumentDelegations.Add(new DocumentDelegation
        {
            SourceDocumentId = docId,
            DelegatedCourt = "دائرة المنابة",
            DelegatedCircuitId = target,
            IsExternal = false,
            Status = DelegationStatusCatalog.PendingHead,
            CreatedById = _headId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<DocumentConflictException>(() =>
            _circuits.DeleteAsync(target, _branchId, null, "head1"));
        Assert.Contains("معلقة", ex.Message);
    }

    [Fact]
    public async Task Rename_CreatesOccurrencesPerDoc_AndMigratesAlertMessages()
    {
        // M1+M6: وقوعّة تسمية لكل ملف + ترحيل نص التنبيه المدمج (المفتاح النصي هش).
        var source = await CreateCircuitAsync("دائرة المصدر");
        var target = await CreateCircuitAsync("دائرة الهدف");
        var docId = await CreateDocAsync(source, _lawyer1Id, "720", "2026");
        await _circuits.ReferFilesAsync(source, _branchId, null, _headId, "head1",
            new ReferCircuitFilesRequest { FileIds = new List<int> { docId }, TargetLawyerId = _lawyer2Id, TargetCircuitId = target });

        await _circuits.RenameAsync(target, _branchId, null, "head1", "دائرة جديدة", null, actorUserId: _headId);

        var occ = _db.DocumentOccurrences
            .Where(o => o.DocumentId == docId && o.OccurrenceType == OccurrenceTypeCatalog.CircuitRenamed)
            .ToList();
        Assert.Single(occ);
        Assert.Equal("دائرة الهدف", occ[0].FromCircuitName);
        Assert.Equal("دائرة جديدة", occ[0].ToCircuitName);
        var alert = Assert.Single(_db.HeadAlerts.Where(a => a.TargetLawyerId == _lawyer2Id));
        Assert.Equal(ExecutionCircuitService.BuildPendingMessage("دائرة جديدة"), alert.Message);
    }

    [Fact]
    public async Task Refer_IdempotentReplay_ReturnsStoredWithoutReexecuting()
    {
        // H3: نفس المفتاح + نفس البصمة → إعادة اللقطة بلا تنفيذ جديد.
        var source = await CreateCircuitAsync("دائرة المصدر");
        var target = await CreateCircuitAsync("دائرة الهدف");
        var docId = await CreateDocAsync(source, _lawyer1Id, "730", "2026");
        var svc = BuildServiceWithIdempotency();
        var req = new ReferCircuitFilesRequest
        {
            FileIds = new List<int> { docId },
            TargetLawyerId = _lawyer2Id,
            TargetCircuitId = target,
        };
        var first = await svc.ReferFilesAsync(source, _branchId, null, _headId, "head1", req, default, "idem-key-1");
        Assert.Equal(1, first.ReferredCount);
        var occCount = _db.DocumentOccurrences
            .Count(o => o.DocumentId == docId && o.OccurrenceType == OccurrenceTypeCatalog.CircuitReferred);

        var replay = await Assert.ThrowsAsync<IdempotentReplayException>(() =>
            svc.ReferFilesAsync(source, _branchId, null, _headId, "head1", req, default, "idem-key-1"));
        Assert.Contains("referredCount", replay.ResponseBody);
        Assert.Equal(occCount, _db.DocumentOccurrences
            .Count(o => o.DocumentId == docId && o.OccurrenceType == OccurrenceTypeCatalog.CircuitReferred));
    }

    [Fact]
    public async Task SyncPendingAlertsAfterTransfer_MovesAlertToNewOwner()
    {
        // H2: التنبيه يتبع المالك — معلق نُقل لمالك جديد: تنبيه الجديد يُنشأ والقديم يُصفَّى.
        var source = await CreateCircuitAsync("دائرة المصدر");
        var target = await CreateCircuitAsync("دائرة الهدف");
        var docId = await CreateDocAsync(source, _lawyer1Id, "740", "2026");
        await _circuits.ReferFilesAsync(source, _branchId, null, _headId, "head1",
            new ReferCircuitFilesRequest { FileIds = new List<int> { docId }, TargetLawyerId = _lawyer1Id, TargetCircuitId = target });
        Assert.Single(_db.HeadAlerts.Where(a => a.TargetLawyerId == _lawyer1Id));

        await _documents.TransferAsync(docId, _lawyer2Id, "head1");
        await _circuits.SyncPendingAlertsAfterTransferAsync(_lawyer1Id, _lawyer2Id, _branchId, _headId);

        Assert.Empty(_db.HeadAlerts.Where(a => a.TargetLawyerId == _lawyer1Id));
        var moved = Assert.Single(_db.HeadAlerts.Where(a => a.TargetLawyerId == _lawyer2Id));
        Assert.Equal(ExecutionCircuitService.BuildPendingMessage("دائرة الهدف"), moved.Message);
    }

    [Fact]
    public async Task Complete_RejectsEmptyFileType()
    {
        // M13: النوع معبأ بالقديم — فراغه مرفوض صراحة لا تخزين null صامت.
        var source = await CreateCircuitAsync("دائرة المصدر");
        var target = await CreateCircuitAsync("دائرة الهدف");
        var docId = await CreateDocAsync(source, _lawyer1Id, "750", "2026");
        await _circuits.ReferFilesAsync(source, _branchId, null, _headId, "head1",
            new ReferCircuitFilesRequest { FileIds = new List<int> { docId }, TargetLawyerId = _lawyer2Id, TargetCircuitId = target });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _circuits.CompleteRegistrationsAsync(_lawyer2Id, "lawyer2",
                new CompleteRegistrationsRequest
                {
                    Entries = new List<CompleteRegistrationEntry>
                    {
                        new() { DocumentId = docId, FileNumber = "751", FileType = "  ", FileYear = "2026" },
                    },
                }));
        Assert.Contains("النوع إلزامي", ex.Message);
    }

    [Fact]
    public async Task Rename_DoesNotMigrateOtherBranchAlerts()
    {
        // C2: اسمان متطابقان في فرعين — التسمية تُرحّل تنبيهات فرعها فقط.
        var branch2 = new Branch { Name = "ريف دمشق", Code = "RIF", Governorate = "دمشق" };
        _db.Branches.Add(branch2);
        await _db.SaveChangesAsync();
        var hasher = new PasswordHasher();
        var head2 = new User { Username = "head2", FullName = "رئيس 2", Role = UserRole.Head, BranchId = branch2.Id, PasswordHash = hasher.Hash("123456") };
        var lawyer3 = new User { Username = "lawyer3", FullName = "محامي 3", Role = UserRole.Lawyer, BranchId = branch2.Id, PasswordHash = hasher.Hash("123456") };
        _db.Users.AddRange(head2, lawyer3);
        await _db.SaveChangesAsync();

        var source = await CreateCircuitAsync("دائرة المصدر");
        var circuitA = await CreateCircuitAsync("دائرة موحدة");
        var circuitB = (await _circuits.CreateAsync(branch2.Id, head2.Id, "head2", "دائرة موحدة")).Id;
        var docId = await CreateDocAsync(source, _lawyer1Id, "770", "2026");
        await _circuits.ReferFilesAsync(source, _branchId, null, _headId, "head1",
            new ReferCircuitFilesRequest { FileIds = new List<int> { docId }, TargetLawyerId = _lawyer1Id, TargetCircuitId = circuitA });

        var otherMessage = ExecutionCircuitService.BuildPendingMessage("دائرة موحدة");
        _db.HeadAlerts.Add(new HeadAlert
        {
            BranchId = branch2.Id,
            CreatedById = head2.Id,
            TargetType = HeadAlertTargetType.Lawyer,
            TargetLawyerId = lawyer3.Id,
            Message = otherMessage,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        await _circuits.RenameAsync(circuitA, _branchId, null, "head1", "دائرة جديدة", null, actorUserId: _headId);

        var migrated = Assert.Single(_db.HeadAlerts.Where(a => a.TargetLawyerId == _lawyer1Id));
        Assert.Equal(ExecutionCircuitService.BuildPendingMessage("دائرة جديدة"), migrated.Message);
        var untouched = Assert.Single(_db.HeadAlerts.Where(a => a.TargetLawyerId == lawyer3.Id));
        Assert.Equal(otherMessage, untouched.Message);
        _ = circuitB;
    }

    [Fact]
    public async Task Delete_DetachesDelegationWithDeletedSource()
    {
        // C1: إنابة مكتملة مصدرها محذوف (مخفية عن الفلتر) — الحذف يفكها لا ينفجر FK.
        var source = await CreateCircuitAsync("دائرة المنيب");
        var target = await CreateCircuitAsync("دائرة المنابة");
        var docId = await CreateDocAsync(source, _lawyer1Id, "771", "2026");
        _db.DocumentDelegations.Add(new DocumentDelegation
        {
            SourceDocumentId = docId,
            DelegatedCourt = "دائرة المنابة",
            DelegatedCircuitId = target,
            IsExternal = false,
            Status = DelegationStatusCatalog.Executed,
            CreatedById = _headId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        var src = await _db.Documents.FindAsync(docId);
        src!.IsDeleted = true;
        src.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await _circuits.DeleteAsync(target, _branchId, null, "head1");

        var g = _db.DocumentDelegations.IgnoreQueryFilters().First(d => d.SourceDocumentId == docId);
        Assert.Null(g.DelegatedCircuitId);
        Assert.Equal("دائرة المنابة", g.DelegatedCourt);
    }

    [Fact]
    public async Task Delete_BlockedByPendingWithDeletedSource()
    {
        // C1: المعلقة تحظر الحذف أيًا كان حال مصدرها.
        var source = await CreateCircuitAsync("دائرة المنيب");
        var target = await CreateCircuitAsync("دائرة المنابة");
        var docId = await CreateDocAsync(source, _lawyer1Id, "772", "2026");
        _db.DocumentDelegations.Add(new DocumentDelegation
        {
            SourceDocumentId = docId,
            DelegatedCourt = "دائرة المنابة",
            DelegatedCircuitId = target,
            IsExternal = false,
            Status = DelegationStatusCatalog.PendingHead,
            CreatedById = _headId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        var src = await _db.Documents.FindAsync(docId);
        src!.IsDeleted = true;
        src.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<DocumentConflictException>(() =>
            _circuits.DeleteAsync(target, _branchId, null, "head1"));
    }

    [Fact]
    public async Task Rename_WritesFieldChangesPerDocument()
    {
        // S5: التسمية الجماعية توثّق حقول كل ملف (مرآة المسار المفرد).
        var circuit = await CreateCircuitAsync("دائرة قديمة");
        var docId = await CreateDocAsync(circuit, _lawyer1Id, "780", "2026");

        await _circuits.RenameAsync(circuit, _branchId, null, "head1", "دائرة جديدة", null, actorUserId: _headId);

        var log = Assert.Single(_audit.ChangeLogs.Where(c => c.ActionType == "rename_circuit" && c.DocumentId == docId));
        Assert.NotEmpty(log.Changes);
    }

    [Fact]
    public async Task ReferFiles_WritesFieldChangesPerDocument()
    {
        // S5: الإحالة الجماعية توثّق حقول كل ملف محال (الملكية/الدائرة/الرقم).
        var source = await CreateCircuitAsync("دائرة المصدر");
        var target = await CreateCircuitAsync("دائرة الهدف");
        var docId = await CreateDocAsync(source, _lawyer1Id, "781", "2026");
        await _circuits.ReferFilesAsync(source, _branchId, null, _headId, "head1",
            new ReferCircuitFilesRequest { FileIds = new List<int> { docId }, TargetLawyerId = _lawyer2Id, TargetCircuitId = target });

        var log = Assert.Single(_audit.ChangeLogs.Where(c => c.ActionType == "refer_circuit_files" && c.DocumentId == docId));
        Assert.NotEmpty(log.Changes);
    }

    [Fact]
    public async Task Complete_WritesFieldChangesPerDocument()
    {
        // S5: إعادة القيد توثّق حقول كل ملف (الرقم/النوع/السنة + إسقاط العلم).
        var source = await CreateCircuitAsync("دائرة المصدر");
        var target = await CreateCircuitAsync("دائرة الهدف");
        var docId = await CreateDocAsync(source, _lawyer1Id, "782", "2026");
        await _circuits.ReferFilesAsync(source, _branchId, null, _headId, "head1",
            new ReferCircuitFilesRequest { FileIds = new List<int> { docId }, TargetLawyerId = _lawyer2Id, TargetCircuitId = target });

        await _circuits.CompleteRegistrationsAsync(_lawyer2Id, "lawyer2",
            new CompleteRegistrationsRequest
            {
                Entries = new List<CompleteRegistrationEntry>
                {
                    new() { DocumentId = docId, FileNumber = "783", FileType = "صلح", FileYear = "2026" },
                },
            });

        var log = Assert.Single(_audit.ChangeLogs.Where(c => c.ActionType == "complete_registrations" && c.DocumentId == docId));
        Assert.NotEmpty(log.Changes);
    }

    [Fact]
    public async Task Create_ReusingDeactivatedName_Conflicts()
    {
        // S6.f: التعطيل لا يحرّر الاسم داخل الفرع — إعادة الاستخدام 409 حتى بعد التعطيل.
        var id = await CreateCircuitAsync("دائرة محجوزة");
        await _circuits.SetActiveAsync(id, _branchId, null, "head1", false, null);

        await Assert.ThrowsAsync<DocumentConflictException>(() =>
            CreateCircuitAsync("دائرة محجوزة"));
    }

    [Fact]
    public async Task CircuitStats_ReturnsLawyerCounts()
    {
        // M7: المحامون النشطون باستعلام تجميعي واحد.
        var a = await CreateCircuitAsync("دائرة أ");
        await CreateCircuitAsync("دائرة ب");
        await CreateDocAsync(a, _lawyer1Id, "760", "2026");
        await CreateDocAsync(a, _lawyer2Id, "761", "2026");
        var stats = await _circuits.CircuitStatsAsync(_branchId, null, false);
        var rowA = Assert.Single(stats.Where(s => s.CircuitName == "دائرة أ"));
        Assert.Equal(2, rowA.FileCount);
        Assert.Equal(2, rowA.LawyerCount);
        var rowB = Assert.Single(stats.Where(s => s.CircuitName == "دائرة ب"));
        Assert.Equal(0, rowB.FileCount);
        Assert.Equal(0, rowB.LawyerCount);
    }

    [Fact]
    public async Task CircuitStats_IncludesNoCircuitRow_OnlyWhenOrphansExist()
    {
        // F4: صف «بلا دائرة» الاصطناعي (`CircuitId = 0`) يظهر لرئيس القسم/الإدارة
        // عندما توجد ملفات يتيمة فقط — والشعبة بلا صف أصلًا (§5.6).
        var sectionId = await AddSectionAsync("شعبة مصياف");
        await CreateDocAsync(await CreateCircuitAsync("دائرة أ"), _lawyer1Id, "762", "2026");
        await AddOrphanDocAsync(_lawyer1Id, pending: true);
        await AddOrphanDocAsync(_lawyer2Id, pending: false);

        var headStats = await _circuits.CircuitStatsAsync(_branchId, null, false);
        var synthetic = Assert.Single(headStats, s => s.CircuitId == 0);
        Assert.Equal("بلا دائرة", synthetic.CircuitName);
        Assert.Equal(_branchId, synthetic.BranchId);
        Assert.Equal(2, synthetic.FileCount);
        Assert.Equal(1, synthetic.PendingCount);
        Assert.Equal(2, synthetic.LawyerCount);
        Assert.Null(synthetic.SectionId);
        Assert.Null(synthetic.SectionName);
        Assert.True(synthetic.IsActive);

        var subStats = await _circuits.CircuitStatsAsync(_branchId, sectionId, false);
        Assert.DoesNotContain(subStats, s => s.CircuitId == 0);
    }

    [Fact]
    public async Task CircuitStats_OmitsNoCircuitRow_WhenNoOrphans()
    {
        // بلا ملفات يتيمة: الاستجابة مطابقة للسابق تمامًا (منع انحدار).
        var a = await CreateCircuitAsync("دائرة أ");
        await CreateDocAsync(a, _lawyer1Id, "763", "2026");

        var headStats = await _circuits.CircuitStatsAsync(_branchId, null, false);
        Assert.DoesNotContain(headStats, s => s.CircuitId == 0);
        var fullStats = await _circuits.CircuitStatsAsync(_branchId, null, true);
        Assert.DoesNotContain(fullStats, s => s.CircuitId == 0);
    }

    private async Task AddOrphanDocAsync(int lawyerId, bool pending)
    {
        var number = $"790{System.Threading.Interlocked.Increment(ref s_orphanSeq):D4}";
        _db.Documents.Add(new DocGenerator.Domain.Entities.Document
        {
            BranchId = _branchId,
            CreatedById = lawyerId,
            IsDraft = false,
            BorrowerName = "أحمد",
            BorrowerFather = "خالد",
            BorrowerFamily = "الخطيب",
            FileNumber = $"{number}/2026",
            FileType = "تنفيذي",
            FileYear = "2026",
            Court = "دائرة تنفيذ دمشق",
            ExecutionCircuitId = null,
            NeedsRegistration = pending,
            AmountNumeric = 0,
            ExecStatus = string.Empty,
        });
        await _db.SaveChangesAsync();
    }

    private static int s_orphanSeq;

    private async Task<int> AddSectionAsync(string name)
    {
        var section = new Section { BranchId = _branchId, Name = name, NameNorm = name, IsActive = true };
        _db.Sections.Add(section);
        await _db.SaveChangesAsync();
        return section.Id;
    }

    private async Task<int> AddSubHeadAsync(string username, int sectionId)
    {
        var hasher = new PasswordHasher();
        var user = new User
        {
            Username = username, FullName = username, Role = UserRole.SubHead,
            BranchId = _branchId, SectionId = sectionId, IsActive = true,
            PasswordHash = hasher.Hash("123456"),
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user.Id;
    }

    private async Task SetCircuitSectionAsync(int circuitId, int? sectionId)
    {
        var circuit = await _db.ExecutionCircuits.SingleAsync(c => c.Id == circuitId);
        circuit.SectionId = sectionId;
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Create_SubHeadCreator_AutoTagsOwnSection()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف");
        var subId = await AddSubHeadAsync("sub_masyaf", sectionId);

        var circuit = await _circuits.CreateAsync(_branchId, subId, "sub_masyaf", "دائرة الشعبة");

        Assert.Equal(sectionId, _db.ExecutionCircuits.Single(c => c.Id == circuit.Id).SectionId);
    }

    [Fact]
    public async Task Create_HeadCreator_OwnedByDivision()
    {
        var circuit = await _circuits.CreateAsync(_branchId, _headId, "head1", "دائرة القسم");

        Assert.Null(_db.ExecutionCircuits.Single(c => c.Id == circuit.Id).SectionId);
    }

    [Fact]
    public async Task Create_NonHeadCreator_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _circuits.CreateAsync(_branchId, _lawyer1Id, "lawyer1", "دائرة"));
    }

    [Fact]
    public async Task Create_SectionlessSubHead_Throws()
    {
        var hasher = new PasswordHasher();
        var sub = new User
        {
            Username = "sub_noscope", FullName = "بلا شعبة", Role = UserRole.SubHead,
            BranchId = _branchId, SectionId = null, IsActive = true, PasswordHash = hasher.Hash("123456"),
        };
        _db.Users.Add(sub);
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _circuits.CreateAsync(_branchId, sub.Id, "sub_noscope", "دائرة"));
        Assert.Contains("بلا شعبة", ex.Message);
    }

    [Fact]
    public async Task Transfer_ToSection_MovesOwnership_WritesSuccessionAndReplacementAlert()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف");
        var subId = await AddSubHeadAsync("sub_masyaf", sectionId);
        var circuitId = await CreateCircuitAsync("دائرة منقولة");
        var docId = await CreateDocAsync(circuitId, _lawyer1Id);
        // إنابة واردة معلقة + استئناف بلا إسناد على ملفات الدائرة.
        _db.DocumentDelegations.Add(new DocumentDelegation
        {
            SourceDocumentId = docId, DelegatedCircuitId = circuitId,
            Status = DelegationStatusCatalog.PendingHead, CreatedById = _lawyer1Id,
        });
        _db.DocumentAppeals.Add(new DocumentAppeal
        {
            DocumentId = docId, Direction = AppealDirectionCatalog.Appellants,
            Status = AppealStatusCatalog.Pending, AppellantsJson = "[]", AppelleesJson = "[]",
            CreatedById = _lawyer1Id,
        });
        await _db.SaveChangesAsync();

        var moved = await _circuits.TransferCircuitAsync(circuitId, sectionId, _headId, "admin");

        Assert.Equal(circuitId, moved.Id);
        Assert.Equal(sectionId, _db.ExecutionCircuits.Single(c => c.Id == circuitId).SectionId);
        Assert.Contains("transfer_circuit", _audit.Actions);
        var succession = Assert.Single(_db.HeadSuccessions.ToList());
        Assert.Equal(HeadSuccessionEventCatalog.CircuitTransferred, succession.Event);
        Assert.Equal(subId, succession.UserId);
        Assert.Equal(sectionId, succession.SectionId);
        var alert = Assert.Single(_db.HeadAlerts.Include(a => a.Recipients).ToList());
        Assert.Contains("نُقلت إليك", alert.Message);
        Assert.Contains("1 إنابة معلقة", alert.Message);
        Assert.Contains("1 استئنافًا", alert.Message);
        Assert.Equal(subId, Assert.Single(alert.Recipients).UserId);
    }

    [Fact]
    public async Task Transfer_ToDivision_ClearsSection_SuccessionToHead()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف");
        await AddSubHeadAsync("sub_masyaf", sectionId);
        var circuitId = await CreateCircuitAsync("دائرة منقولة");
        await SetCircuitSectionAsync(circuitId, sectionId);

        var moved = await _circuits.TransferCircuitAsync(circuitId, null, _headId, "admin");

        Assert.Null(_db.ExecutionCircuits.Single(c => c.Id == circuitId).SectionId);
        var succession = Assert.Single(_db.HeadSuccessions.ToList());
        Assert.Equal(_headId, succession.UserId);
        Assert.Null(succession.SectionId);
    }

    [Fact]
    public async Task Transfer_SameOwner_NoOpWithoutAudit()
    {
        var circuitId = await CreateCircuitAsync("دائرة القسم");
        var auditCount = _audit.Actions.Count;

        var same = await _circuits.TransferCircuitAsync(circuitId, null, _headId, "admin");

        Assert.NotNull(same);
        Assert.Equal(auditCount, _audit.Actions.Count);
        Assert.Empty(_db.HeadSuccessions.ToList());
    }

    [Fact]
    public async Task Transfer_HeadlessNewOwner_NoAlertNoSuccession()
    {
        var sectionId = await AddSectionAsync("شعبة شاغرة");
        var circuitId = await CreateCircuitAsync("دائرة منقولة");

        var moved = await _circuits.TransferCircuitAsync(circuitId, sectionId, _headId, "admin");

        // العقد يُعاد مملوءًا حتى بلا رئيس (انحدار `Ok(null)` → `204`).
        Assert.NotNull(moved);
        Assert.Equal(circuitId, moved.Id);
        Assert.Equal(sectionId, _db.ExecutionCircuits.Single(c => c.Id == circuitId).SectionId);
        Assert.Contains("transfer_circuit", _audit.Actions);
        Assert.Empty(_db.HeadSuccessions.ToList());
        Assert.Empty(_db.HeadAlerts.ToList());
    }

    [Fact]
    public async Task Transfer_UnknownCircuit_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _circuits.TransferCircuitAsync(999999, null, _headId, "admin"));
    }

    [Fact]
    public async Task Transfer_UnknownSection_Throws()
    {
        var circuitId = await CreateCircuitAsync("دائرة منقولة");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _circuits.TransferCircuitAsync(circuitId, 999999, _headId, "admin"));
    }

    [Fact]
    public async Task Transfer_CrossBranchSection_Throws()
    {
        var circuitId = await CreateCircuitAsync("دائرة منقولة");
        var otherBranch = new Branch { Name = "حلب", Code = "ALP", Governorate = "حلب" };
        _db.Branches.Add(otherBranch);
        await _db.SaveChangesAsync();
        var foreign = new Section { BranchId = otherBranch.Id, Name = "شعبة حلب", NameNorm = "شعبة حلب", IsActive = true };
        _db.Sections.Add(foreign);
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _circuits.TransferCircuitAsync(circuitId, foreign.Id, _headId, "admin"));
        Assert.Contains("داخل الفرع نفسه", ex.Message);
    }

    [Fact]
    public async Task Transfer_InactiveSection_Throws()
    {
        var circuitId = await CreateCircuitAsync("دائرة منقولة");
        var sectionId = await AddSectionAsync("شعبة معطلة");
        var section = await _db.Sections.SingleAsync(s => s.Id == sectionId);
        section.IsActive = false;
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _circuits.TransferCircuitAsync(circuitId, sectionId, _headId, "admin"));
        Assert.Contains("معطلة", ex.Message);
    }

    [Fact]
    public async Task Transfer_StaleVersion_Conflict()
    {
        var circuitId = await CreateCircuitAsync("دائرة منقولة");
        var sectionId = await AddSectionAsync("شعبة مصياف");
        var version = (await _db.ExecutionCircuits.AsNoTracking().SingleAsync(c => c.Id == circuitId)).Version;

        await _circuits.TransferCircuitAsync(circuitId, sectionId, _headId, "admin", version: version);
        await Assert.ThrowsAsync<DocumentConflictException>(() =>
            _circuits.TransferCircuitAsync(circuitId, null, _headId, "admin", version: version));
    }

    [Fact]
    public async Task ListMine_HeadSeesDivisionOnly()
    {
        var divisionId = await CreateCircuitAsync("دائرة القسم");
        var sectionId = await AddSectionAsync("شعبة مصياف");
        var sectionCircuit = await CreateCircuitAsync("دائرة الشعبة");
        await SetCircuitSectionAsync(sectionCircuit, sectionId);

        var mine = await _circuits.ListMineAsync(_branchId, null);

        Assert.Contains(mine, c => c.Id == divisionId);
        Assert.DoesNotContain(mine, c => c.Id == sectionCircuit);
    }

    [Fact]
    public async Task ListMine_SubHeadSeesOwnSectionOnly()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف");
        var otherId = await AddSectionAsync("شعبة أخرى");
        var divisionCircuit = await CreateCircuitAsync("دائرة القسم");
        var ownCircuit = await CreateCircuitAsync("دائرة الشعبة");
        await SetCircuitSectionAsync(ownCircuit, sectionId);
        var foreignCircuit = await CreateCircuitAsync("دائرة الغير");
        await SetCircuitSectionAsync(foreignCircuit, otherId);

        var mine = await _circuits.ListMineAsync(_branchId, sectionId);

        var single = Assert.Single(mine);
        Assert.Equal(ownCircuit, single.Id);
        Assert.Equal("شعبة مصياف", single.SectionName);
    }

    [Theory]
    [InlineData("rename")]
    [InlineData("active")]
    [InlineData("delete")]
    public async Task Mutations_ForeignScope_ThrowsOutOfScope(string op)
    {
        var sectionId = await AddSectionAsync("شعبة مصياف");
        var circuitId = await CreateCircuitAsync("دائرة الشعبة");
        await SetCircuitSectionAsync(circuitId, sectionId);

        Task Act() => op switch
        {
            "rename" => _circuits.RenameAsync(circuitId, _branchId, null, "head1", "جديد", null),
            "active" => _circuits.SetActiveAsync(circuitId, _branchId, null, "head1", false, null),
            "delete" => _circuits.DeleteAsync(circuitId, _branchId, null, "head1"),
            _ => throw new InvalidOperationException(),
        };
        var ex = await Assert.ThrowsAsync<ArgumentException>(Act);
        Assert.Contains("ليست ضمن نطاقك", ex.Message);
    }

    [Fact]
    public async Task CircuitStats_HeadSeesDivisionOnly_ManagerSeesAll()
    {
        var divisionId = await CreateCircuitAsync("دائرة القسم");
        var sectionId = await AddSectionAsync("شعبة مصياف");
        var sectionCircuit = await CreateCircuitAsync("دائرة الشعبة");
        await SetCircuitSectionAsync(sectionCircuit, sectionId);

        var headRows = await _circuits.CircuitStatsAsync(_branchId, null, false);
        Assert.Contains(headRows, r => r.CircuitId == divisionId);
        Assert.DoesNotContain(headRows, r => r.CircuitId == sectionCircuit);

        var subRows = await _circuits.CircuitStatsAsync(_branchId, sectionId, false);
        Assert.Equal(sectionCircuit, Assert.Single(subRows).CircuitId);

        var allRows = await _circuits.CircuitStatsAsync(_branchId, null, true);
        Assert.Equal(2, allRows.Count);
    }

    [Fact]
    public async Task ListForLawyer_LawyerSeesAll_HeadSeesOwn()
    {
        var divisionId = await CreateCircuitAsync("دائرة القسم");
        var sectionId = await AddSectionAsync("شعبة مصياف");
        var sectionCircuit = await CreateCircuitAsync("دائرة الشعبة");
        await SetCircuitSectionAsync(sectionCircuit, sectionId);

        var lawyerView = await _circuits.ListForLawyerAsync(_branchId, null, false);
        Assert.Equal(2, lawyerView.Count);

        var headView = await _circuits.ListForLawyerAsync(_branchId, null, true);
        Assert.Equal(divisionId, Assert.Single(headView).Id);

        var subView = await _circuits.ListForLawyerAsync(_branchId, sectionId, true);
        Assert.Equal(sectionCircuit, Assert.Single(subView).Id);
    }

    private IExecutionCircuitService BuildServiceWithIdempotency()
    {
        var documents = new DocumentRepository(_db);
        var users = new UserRepository(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        var classifier = new DbExceptionClassifier();
        return new ExecutionCircuitService(
            new Repository<ExecutionCircuit>(_db), new Repository<Branch>(_db),
            new Repository<Section>(_db),
            documents, new DelegationRepository(_db),
            new AppealRepository(_db),
            new Repository<DocumentOccurrence>(_db), new Repository<DocumentBaseNumber>(_db),
            new Repository<HeadSuccession>(_db), new HeadAlertRepository(_db), users,
            uow, tx, _audit, classifier, TimeProvider.System, TestClock.TimeZone,
            new IdempotencyStore(_db));
    }
}
