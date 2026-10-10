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
/// نطاق المطالعات والمراسلات لرئيس الشعبة (§10 + قرارات 12/24/28):
/// توجيه المستلم التلقائي/بالمنسدل، والرؤية والرد والعدّاد بالنطاق.
/// قاعدة كل اختبار جديدة (TestDb) — بلا تلوث متبادل.
/// </summary>
public class SubHeadReviewCorrespondenceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IReviewLetterService _letters;
    private readonly ICorrespondenceService _correspondences;
    private readonly FakeAuditLogger _audit = new();

    private readonly Branch _branch;
    private readonly Branch _otherBranch;
    private readonly User _lawyer1;
    private readonly User _lawyer2;
    private readonly User _head1;

    public SubHeadReviewCorrespondenceTests()
    {
        _db = TestDb.Create();

        _branch = new Branch { Name = "دمشق", Code = "DAM", Governorate = "دمشق" };
        _otherBranch = new Branch { Name = "حلب", Code = "ALP", Governorate = "حلب" };
        _db.Branches.AddRange(_branch, _otherBranch);
        _db.SaveChanges();

        _lawyer1 = User(_branch.Id, "lawyer1", "محامي دمشق");
        _lawyer2 = User(_branch.Id, "lawyer2", "محامي دمشق ثانٍ");
        _head1 = User(_branch.Id, "head1", "رئيس قسم دمشق", UserRole.Head);
        _db.Users.AddRange(_lawyer1, _lawyer2, _head1);
        _db.SaveChanges();

        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        _letters = new ReviewLetterService(
            new ReviewLetterRepository(_db),
            new DocumentRepository(_db),
            new BranchRepository(_db),
            new AppealRepository(_db),
            new DelegationRepository(_db),
            new HeadAlertRepository(_db),
            new DbExceptionClassifier(),
            uow,
            tx,
            _audit,
            TimeProvider.System,
            TestClock.TimeZone,
            new UserRepository(_db),
            new Repository<Section>(_db),
            new Repository<ExecutionCircuit>(_db));
        _correspondences = new CorrespondenceService(
            new CorrespondenceRepository(_db),
            new DocumentRepository(_db),
            new BranchRepository(_db),
            new UserRepository(_db),
            new AppealRepository(_db),
            new DelegationRepository(_db),
            new PortalRepository(_db),
            uow,
            tx,
            _audit,
            new DbExceptionClassifier(),
            TimeProvider.System,
            TestClock.TimeZone,
            new Repository<Section>(_db),
            new Repository<ExecutionCircuit>(_db));
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

    private async Task<int> AddSectionAsync(string name, int branchId, bool isActive = true)
    {
        var section = new Section
        {
            Name = name,
            NameNorm = ArabicNameNormalizer.Normalize(name),
            BranchId = branchId,
            IsActive = isActive,
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

    private async Task<int> AddCircuitAsync(string name, int branchId, int? sectionId)
    {
        var circuit = new ExecutionCircuit
        {
            BranchId = branchId,
            SectionId = sectionId,
            Name = name,
            NameNorm = ArabicNameNormalizer.Normalize(name),
            IsActive = true,
            CreatedById = _head1.Id,
        };
        _db.ExecutionCircuits.Add(circuit);
        await _db.SaveChangesAsync();
        return circuit.Id;
    }

    private async Task<Document> AddDocumentAsync(int ownerId, int? circuitId = null)
    {
        var number = $"770{System.Threading.Interlocked.Increment(ref s_seq):D4}";
        var doc = new Document
        {
            BranchId = _branch.Id,
            CreatedById = ownerId,
            IsDraft = false,
            BorrowerName = "أحمد",
            BorrowerFather = "محمد",
            BorrowerFamily = "العلي",
            FileNumber = $"{number}/2026",
            FileType = "تنفيذي",
            FileYear = "2026",
            Court = "دائرة تنفيذ دمشق",
            ExecutionCircuitId = circuitId,
            AmountNumeric = 0,
            ExecStatus = string.Empty,
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        return doc;
    }

    // ── المطالعات ──────────────────────────────────────────────────────

    [Fact]
    public async Task Letter_Create_LinkedFile_AutoRoutesToCircuitOwner()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sectionCircuit = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId);
        var divisionCircuit = await AddCircuitAsync("دائرة القسم", _branch.Id, null);
        var sectionDoc = await AddDocumentAsync(_lawyer1.Id, sectionCircuit);
        var divisionDoc = await AddDocumentAsync(_lawyer1.Id, divisionCircuit);

        // بلا منسدل: يُشتق من الدائرة ويُتجاهل المُرسَل (شعبة خاطئة مرسلة عمدًا).
        var sectionLetter = await _letters.CreateAsync(
            new CreateReviewLetterRequest(sectionDoc.Id, "<p>مطالعة</p>", divisionCircuit),
            _lawyer1.Id, "lawyer1", _branch.Id);
        Assert.Equal(sectionId, sectionLetter.RecipientSectionId);

        var divisionLetter = await _letters.CreateAsync(
            new CreateReviewLetterRequest(divisionDoc.Id, "<p>مطالعة</p>"),
            _lawyer1.Id, "lawyer1", _branch.Id);
        Assert.Null(divisionLetter.RecipientSectionId);
    }

    [Fact]
    public async Task Letter_Create_General_ValidatesSectionChoice()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var otherSectionId = await AddSectionAsync("شعبة حلب", _otherBranch.Id);
        var idleId = await AddSectionAsync("شعبة بلا رئيس", _branch.Id);
        var offId = await AddSectionAsync("شعبة معطلة", _branch.Id, isActive: false);
        await AddSubHeadAsync("sub_off", _branch.Id, offId);

        // الافتراضي رئيس القسم، والاختيار الصحيح يُجمَّد.
        var headLetter = await _letters.CreateAsync(
            new CreateReviewLetterRequest(null, "<p>عام</p>"), _lawyer1.Id, "lawyer1", _branch.Id);
        Assert.Null(headLetter.RecipientSectionId);
        var sectionLetter = await _letters.CreateAsync(
            new CreateReviewLetterRequest(null, "<p>عام</p>", sectionId), _lawyer1.Id, "lawyer1", _branch.Id);
        Assert.Equal(sectionId, sectionLetter.RecipientSectionId);
        Assert.Equal("شعبة مصياف", sectionLetter.RecipientSectionName);

        // فرع آخر / معطلة / بلا رئيس مفعّل / وهمية → «يجب اختيار المستلم».
        foreach (var bad in new[] { otherSectionId, offId, idleId, 999_999 })
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _letters.CreateAsync(
                new CreateReviewLetterRequest(null, "<p>عام</p>", bad), _lawyer1.Id, "lawyer1", _branch.Id));
            Assert.Contains("يجب اختيار المستلم", ex.Message);
        }
    }

    [Fact]
    public async Task Letter_Create_NumberRace_RetriesWithFreshNumberAndSucceeds()
    {
        // سباق الترقيم: أول حفظ يصطدم بالقيد الفريد فيُعاد التوليد بدل 500.
        var uow = new FailCountingUnitOfWork(new UnitOfWork(_db), failTimes: 1);
        var service = new ReviewLetterService(
            new ReviewLetterRepository(_db),
            new DocumentRepository(_db),
            new BranchRepository(_db),
            new AppealRepository(_db),
            new DelegationRepository(_db),
            new HeadAlertRepository(_db),
            new DbExceptionClassifier(),
            uow,
            new TransactionRunner(_db),
            _audit,
            TimeProvider.System,
            TestClock.TimeZone,
            new UserRepository(_db),
            new Repository<Section>(_db),
            new Repository<ExecutionCircuit>(_db));

        var letter = await service.CreateAsync(
            new CreateReviewLetterRequest(null, "<p>سباق</p>"), _lawyer1.Id, "lawyer1", _branch.Id);

        Assert.NotNull(letter);
        Assert.StartsWith("DAM-", letter.LetterNumber);
        Assert.Equal(2, uow.Calls);
    }

    /// <summary>
    /// وحدة عمل اختبارية تُفشل أول حفظ باستثناء تفرّد حقيقي من المزود —
    /// مرآة نمط اختبار سباق ترقيم المراسلات.
    /// </summary>
    private sealed class FailCountingUnitOfWork : IUnitOfWork
    {
        private readonly IUnitOfWork _inner;
        private readonly int _failTimes;
        private Exception? _failure;
        public int Calls { get; private set; }

        public FailCountingUnitOfWork(IUnitOfWork inner, int failTimes)
        {
            _inner = inner;
            _failTimes = failTimes;
        }

        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            Calls++;
            if (Calls <= _failTimes)
                throw _failure ??= BuildRealUniqueViolation();
            return await _inner.SaveChangesAsync(ct);
        }

        private static DbUpdateException BuildRealUniqueViolation()
        {
            var options = new DbContextOptionsBuilder<DocGeneratorDbContext>()
                .UseSqlite("DataSource=:memory:")
                .Options;
            using var db = new DocGeneratorDbContext(options);
            db.Database.OpenConnection();
            db.Database.EnsureCreated();
            var damascus = db.Branches.Add(new Branch { Name = "دمشق", Code = "DAM" }).Entity;
            db.SaveChanges();
            var maker = new User { Username = "conflict_maker", FullName = "صانع التعارض", Role = UserRole.Lawyer, BranchId = damascus.Id, PasswordHash = "x" };
            db.Users.Add(maker);
            db.SaveChanges();
            var duplicate = () => new ReviewLetter
            {
                BranchId = damascus.Id,
                CreatedById = maker.Id,
                LetterNumber = "CNF-2026-0001",
            };
            db.ReviewLetters.Add(duplicate());
            db.SaveChanges();
            db.ReviewLetters.Add(duplicate());
            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateException ex)
            {
                return ex;
            }
            throw new InvalidOperationException("تعذّر توليد تعارض فريد حقيقي من المزود");
        }
    }

    [Fact]
    public async Task Letter_ListByDocument_OtherBranchHeadDeniedOnBranchlessFile()
    {
        // ملف إرثي بلا فرع: رئيس فرع آخر مرفوض (لا تسريب عبر الفراغ).
        // (رئيس بلا فرع أصلًا مستحيل مخزنيًا — قيد `BranchRequiredForBranchRoles`.)
        var legacy = new Document
        {
            BranchId = null,
            CreatedById = _lawyer1.Id,
            IsDraft = false,
            BorrowerName = "قديم",
            FileNumber = "1/1990",
            FileType = "تنفيذي",
            FileYear = "1990",
            Court = "دمشق",
            AmountNumeric = 0,
            ExecStatus = string.Empty,
        };
        _db.Documents.Add(legacy);
        await _db.SaveChangesAsync();
        var otherHead = User(_otherBranch.Id, "head_alp", "رئيس حلب", UserRole.Head);
        _db.Users.Add(otherHead);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _letters.ListByDocumentAsync(legacy.Id, otherHead.Id, UserRole.Head, _otherBranch.Id));
    }

    [Fact]
    public async Task Correspondence_ListByDocument_SubDeniedOnBranchlessFile()
    {
        // رئيس شعبة فرعٍ ما × ملف إرثي بلا فرع: مرفوض (نطاق الشعبة فرعٌ ودائرة).
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var legacy = new Document
        {
            BranchId = null,
            CreatedById = _lawyer1.Id,
            IsDraft = false,
            BorrowerName = "قديم",
            FileNumber = "2/1990",
            FileType = "تنفيذي",
            FileYear = "1990",
            Court = "دمشق",
            AmountNumeric = 0,
            ExecStatus = string.Empty,
        };
        _db.Documents.Add(legacy);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _correspondences.ListByDocumentAsync(legacy.Id, sub.Id, UserRole.SubHead, _branch.Id, default, sectionId));
    }

    [Fact]
    public async Task Correspondence_SubHead_CanWriteGeneral_NotFileLinked()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var doc = await AddDocumentAsync(_lawyer1.Id);

        // عامة من رئيس الشعبة مسموحة بفرعه.
        var general = await _correspondences.CreateAsync(
            new CreateCorrespondenceRequest(null, _lawyer1.Id, "normal", "<p>من الشعبة</p>"),
            sub.Id, "sub", UserRole.SubHead, _branch.Id);
        Assert.NotNull(general);

        // مربوطة بملف مرفوضة (لا تأليف للشعبة على الملفات).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _correspondences.CreateAsync(
            new CreateCorrespondenceRequest(doc.Id, _lawyer1.Id, "normal", "<p>على ملف</p>"),
            sub.Id, "sub", UserRole.SubHead, _branch.Id));
    }

    [Fact]
    public async Task PendingScope_HeadExcludesSection_SubSeesOwnOnly()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var sectionCircuit = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId);
        var sectionDoc = await AddDocumentAsync(_lawyer1.Id, sectionCircuit);
        var divisionDoc = await AddDocumentAsync(_lawyer1.Id);
        await _letters.CreateAsync(new CreateReviewLetterRequest(sectionDoc.Id, "<p>شعبة</p>"),
            _lawyer1.Id, "lawyer1", _branch.Id);
        await _letters.CreateAsync(new CreateReviewLetterRequest(divisionDoc.Id, "<p>قسم</p>"),
            _lawyer1.Id, "lawyer1", _branch.Id);
        await _letters.CreateAsync(new CreateReviewLetterRequest(null, "<p>عام للشعبة</p>", sectionId),
            _lawyer1.Id, "lawyer1", _branch.Id);

        var headView = await _letters.SearchAsync(_head1.Id, UserRole.Head, _branch.Id, null, 1, 20, null);
        Assert.Equal(1, headView.TotalCount);
        Assert.Null(headView.Items.Single().RecipientSectionId);

        var subView = await _letters.SearchAsync(sub.Id, UserRole.SubHead, _branch.Id, null, 1, 20, null,
            ct: default, actorSectionId: sectionId);
        Assert.Equal(2, subView.TotalCount);
        Assert.All(subView.Items, i => Assert.Equal(sectionId, i.RecipientSectionId));

        // رمز بلا شعبة مرفوض.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _letters.SearchAsync(sub.Id, UserRole.SubHead, _branch.Id, null, 1, 20, null));
    }

    [Fact]
    public async Task Letter_Reply_OwnerOnly()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var sectionCircuit = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId);
        var sectionDoc = await AddDocumentAsync(_lawyer1.Id, sectionCircuit);
        var divisionDoc = await AddDocumentAsync(_lawyer1.Id);
        var sectionLetter = await _letters.CreateAsync(
            new CreateReviewLetterRequest(sectionDoc.Id, "<p>شعبة</p>"), _lawyer1.Id, "lawyer1", _branch.Id);
        var divisionLetter = await _letters.CreateAsync(
            new CreateReviewLetterRequest(divisionDoc.Id, "<p>قسم</p>"), _lawyer1.Id, "lawyer1", _branch.Id);

        // رئيس الشعبة يرد كتبها بصفته، ورئيس القسم مرفوض عليها.
        var reply = await _letters.ReplyAsync(sectionLetter.Id, new ReplyReviewLetterRequest("<p>رد</p>"),
            sub.Id, "sub", _branch.Id, default, UserRole.SubHead, sectionId);
        Assert.Equal("subhead", reply.AuthorRole);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _letters.ReplyAsync(
            sectionLetter.Id, new ReplyReviewLetterRequest("<p>رد</p>"),
            _head1.Id, "head1", _branch.Id, default, UserRole.Head, null));

        // ورئيس القسم يرد كتبه.
        var headReply = await _letters.ReplyAsync(divisionLetter.Id, new ReplyReviewLetterRequest("<p>رد</p>"),
            _head1.Id, "head1", _branch.Id);
        Assert.Equal("head", headReply.AuthorRole);

        // بلا شعبة مرفوض.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _letters.ReplyAsync(
            sectionLetter.Id, new ReplyReviewLetterRequest("<p>رد</p>"),
            sub.Id, "sub", _branch.Id, default, UserRole.SubHead, null));
    }

    [Fact]
    public async Task Letter_PendingCount_Scoped()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sectionCircuit = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId);
        var sectionDoc = await AddDocumentAsync(_lawyer1.Id, sectionCircuit);
        var divisionDoc = await AddDocumentAsync(_lawyer1.Id);
        await _letters.CreateAsync(new CreateReviewLetterRequest(sectionDoc.Id, "<p>شعبة</p>"),
            _lawyer1.Id, "lawyer1", _branch.Id);
        await _letters.CreateAsync(new CreateReviewLetterRequest(divisionDoc.Id, "<p>قسم</p>"),
            _lawyer1.Id, "lawyer1", _branch.Id);

        Assert.Equal(1, await _letters.CountPendingForHeadAsync(_branch.Id, null));
        Assert.Equal(1, await _letters.CountPendingForHeadAsync(_branch.Id, sectionId));
    }

    [Fact]
    public async Task Letter_ListByDocument_Scoped()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var sectionCircuit = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId);
        var sectionDoc = await AddDocumentAsync(_lawyer1.Id, sectionCircuit);
        var divisionDoc = await AddDocumentAsync(_lawyer1.Id);
        await _letters.CreateAsync(new CreateReviewLetterRequest(sectionDoc.Id, "<p>شعبة</p>"),
            _lawyer1.Id, "lawyer1", _branch.Id);
        await _letters.CreateAsync(new CreateReviewLetterRequest(divisionDoc.Id, "<p>قسم</p>"),
            _lawyer1.Id, "lawyer1", _branch.Id);

        // القسم يرى كتب ملف القسم دون ملف الشعبة، والشعبة عكسه.
        Assert.Single(await _letters.ListByDocumentAsync(divisionDoc.Id, _head1.Id, UserRole.Head, _branch.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _letters.ListByDocumentAsync(sectionDoc.Id, _head1.Id, UserRole.Head, _branch.Id));
        Assert.Single(await _letters.ListByDocumentAsync(
            sectionDoc.Id, sub.Id, UserRole.SubHead, _branch.Id, default, sectionId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _letters.ListByDocumentAsync(divisionDoc.Id, sub.Id, UserRole.SubHead, _branch.Id, default, sectionId));
    }

    // ── المراسلات ────────────────────────────────────────────────────

    [Fact]
    public async Task Correspondence_Create_General_ToSectionHead_FreezesTarget()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);

        var letter = await _correspondences.CreateAsync(
            new CreateCorrespondenceRequest(null, sub.Id, "normal", "<p>إلى الشعبة</p>", sectionId),
            _lawyer1.Id, "lawyer1", UserRole.Lawyer, _branch.Id);

        Assert.Equal(sub.Id, letter.TargetUserId);
        var stored = await _db.Correspondences.SingleAsync(c => c.Id == letter.Id);
        Assert.Equal(sectionId, stored.RecipientSectionId);
    }

    [Fact]
    public async Task Correspondence_Create_General_ToHeadDefault_RequiresBranchHead()
    {
        // الافتراضي رئيس قسم فرع المنشئ.
        var letter = await _correspondences.CreateAsync(
            new CreateCorrespondenceRequest(null, _head1.Id, "normal", "<p>إلى القسم</p>"),
            _lawyer1.Id, "lawyer1", UserRole.Lawyer, _branch.Id);
        var stored = await _db.Correspondences.SingleAsync(c => c.Id == letter.Id);
        Assert.Null(stored.RecipientSectionId);

        // رئيس فرع آخر بلا اختيار شعبة مرفوض، وكذا نائب بلا اختيار.
        var otherHead = User(_otherBranch.Id, "head_alp", "رئيس حلب", UserRole.Head);
        _db.Users.Add(otherHead);
        await _db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _correspondences.CreateAsync(
            new CreateCorrespondenceRequest(null, otherHead.Id, "normal", "<p>عام</p>"),
            _lawyer1.Id, "lawyer1", UserRole.Lawyer, _branch.Id));
        Assert.Contains("يجب اختيار المستلم", ex.Message);
    }

    [Fact]
    public async Task Correspondence_Create_General_ToSubHead_DerivesSectionFromAccount()
    {
        // مستلم الشعبة المسمّى بالاسم: تُشتق شعبته من حسابه (قرار 24) بلا منسدل إضافي.
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);

        var letter = await _correspondences.CreateAsync(
            new CreateCorrespondenceRequest(null, sub.Id, "normal", "<p>عام للشعبة</p>"),
            _lawyer1.Id, "lawyer1", UserRole.Lawyer, _branch.Id);
        var stored = await _db.Correspondences.SingleAsync(c => c.Id == letter.Id);
        Assert.Equal(sectionId, stored.RecipientSectionId);

        // رئيس شعبة بحساب بلا شعبة (مشوّه) لا تُشتق له — يُرفض.
        var sectionless = User(_branch.Id, "sub_nosection", "بلا شعبة", UserRole.SubHead);
        _db.Users.Add(sectionless);
        await _db.SaveChangesAsync();
        var exSub = await Assert.ThrowsAsync<ArgumentException>(() => _correspondences.CreateAsync(
            new CreateCorrespondenceRequest(null, sectionless.Id, "normal", "<p>عام</p>"),
            _lawyer1.Id, "lawyer1", UserRole.Lawyer, _branch.Id));
        Assert.Contains("يجب اختيار المستلم", exSub.Message);
    }

    [Fact]
    public async Task Correspondence_SearchTargets_General_IncludesSubHead()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);

        var targets = await _correspondences.SearchTargetsAsync(
            _lawyer1.Id, UserRole.Lawyer, _branch.Id, "sub_masyaf", null, default);

        Assert.Contains(targets, t => t.FullName == "sub_masyaf");
    }

    [Fact]
    public async Task Correspondence_Create_General_ToLawyer_WithSection_Throws()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _correspondences.CreateAsync(
            new CreateCorrespondenceRequest(null, _lawyer2.Id, "normal", "<p>عام</p>", sectionId),
            _lawyer1.Id, "lawyer1", UserRole.Lawyer, _branch.Id));
        Assert.Contains("للمستلم الرئيس فقط", ex.Message);
    }

    [Fact]
    public async Task Correspondence_Search_SubHead_Scope()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);

        // عامة للشعبة (مستلمها الرئيس)، وعامة للقسم، وخاصة بين محامين.
        await _correspondences.CreateAsync(
            new CreateCorrespondenceRequest(null, sub.Id, "normal", "<p>للشعبة</p>", sectionId),
            _lawyer1.Id, "lawyer1", UserRole.Lawyer, _branch.Id);
        await _correspondences.CreateAsync(
            new CreateCorrespondenceRequest(null, _head1.Id, "normal", "<p>للقسم</p>"),
            _lawyer1.Id, "lawyer1", UserRole.Lawyer, _branch.Id);
        await _correspondences.CreateAsync(
            new CreateCorrespondenceRequest(null, _lawyer2.Id, "normal", "<p>خاصة</p>"),
            _lawyer1.Id, "lawyer1", UserRole.Lawyer, _branch.Id);

        var subView = await _correspondences.SearchAsync(
            sub.Id, UserRole.SubHead, _branch.Id, null, null, null, 1, 20, default, sectionId);
        Assert.Equal(1, subView.TotalCount);
        Assert.Equal("sub_masyaf", subView.Items.Single().TargetName);

        await Assert.ThrowsAsync<ArgumentException>(() => _correspondences.SearchAsync(
            sub.Id, UserRole.SubHead, _branch.Id, null, null, null, 1, 20, default, null));
    }

    [Fact]
    public async Task Correspondence_GetById_SubHead_Scope()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);

        var sectionLetter = await _correspondences.CreateAsync(
            new CreateCorrespondenceRequest(null, sub.Id, "normal", "<p>للشعبة</p>", sectionId),
            _lawyer1.Id, "lawyer1", UserRole.Lawyer, _branch.Id);
        var headLetter = await _correspondences.CreateAsync(
            new CreateCorrespondenceRequest(null, _head1.Id, "normal", "<p>للقسم</p>"),
            _lawyer1.Id, "lawyer1", UserRole.Lawyer, _branch.Id);

        var seen = await _correspondences.GetByIdAsync(
            sectionLetter.Id, sub.Id, UserRole.SubHead, _branch.Id, default, sectionId);
        Assert.Equal(sectionLetter.Id, seen.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _correspondences.GetByIdAsync(
            headLetter.Id, sub.Id, UserRole.SubHead, _branch.Id, default, sectionId));
    }

    [Fact]
    public async Task Correspondence_ListByDocument_SubHead_Scope()
    {
        var sectionId = await AddSectionAsync("شعبة مصياف", _branch.Id);
        var sub = await AddSubHeadAsync("sub_masyaf", _branch.Id, sectionId);
        var sectionCircuit = await AddCircuitAsync("دائرة الشعبة", _branch.Id, sectionId);
        var sectionDoc = await AddDocumentAsync(_lawyer1.Id, sectionCircuit);
        var divisionDoc = await AddDocumentAsync(_lawyer1.Id);

        // مراسلة الملف لمندوب: تحتاج نطاق جهة — تُزرع مباشرة لاختبار الرؤية فقط.
        // (الأهلية مغطاة باختبارات المراسلات القائمة.)
        _db.Correspondences.Add(new Correspondence
        {
            BranchId = _branch.Id,
            Governorate = "دمشق",
            CreatedById = _lawyer1.Id,
            TargetUserId = _lawyer2.Id,
            DocumentId = sectionDoc.Id,
            CorrespondenceNumber = "SEC-2026-00001",
            CorrespondenceDate = DateTime.UtcNow,
            Importance = "normal",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        _db.Correspondences.Add(new Correspondence
        {
            BranchId = _branch.Id,
            Governorate = "دمشق",
            CreatedById = _lawyer1.Id,
            TargetUserId = _lawyer2.Id,
            DocumentId = divisionDoc.Id,
            CorrespondenceNumber = "SEC-2026-00002",
            CorrespondenceDate = DateTime.UtcNow,
            Importance = "normal",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        Assert.Single(await _correspondences.ListByDocumentAsync(
            sectionDoc.Id, sub.Id, UserRole.SubHead, _branch.Id, default, sectionId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _correspondences.ListByDocumentAsync(divisionDoc.Id, sub.Id, UserRole.SubHead, _branch.Id, default, sectionId));
    }
}
