using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

public class ReviewLetterServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IReviewLetterService _service;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _branchId;
    private readonly int _otherBranchId;
    private readonly User _head;
    private readonly User _lawyer1;
    private readonly User _lawyer2;
    private readonly User _otherBranchHead;

    public ReviewLetterServiceTests()
    {
        _db = TestDb.Create();
        var branch = new Branch { Name = "دمشق", Code = "DAM" };
        var otherBranch = new Branch { Name = "حلب", Code = "ALP" };
        _db.Branches.AddRange(branch, otherBranch);
        _db.SaveChanges();
        _branchId = branch.Id;
        _otherBranchId = otherBranch.Id;

        _head = NewUser("head_x", "رئيس القسم", UserRole.Head, _branchId);
        _otherBranchHead = NewUser("head_y", "رئيس حلب", UserRole.Head, _otherBranchId);
        _lawyer1 = NewUser("law1", "المحامي الأول", UserRole.Lawyer, _branchId);
        _lawyer2 = NewUser("law2", "المحامي الثاني", UserRole.Lawyer, _branchId);
        _db.Users.AddRange(_head, _otherBranchHead, _lawyer1, _lawyer2);
        _db.SaveChanges();

        _service = BuildService();
    }

    public void Dispose() => _db.Dispose();

    private ReviewLetterService BuildService()
    {
        var letters = new ReviewLetterRepository(_db);
        var documents = new DocumentRepository(_db);
        var branches = new BranchRepository(_db);
        var appeals = new AppealRepository(_db);
        var delegations = new DelegationRepository(_db);
        var headAlerts = new HeadAlertRepository(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        return new ReviewLetterService(
            letters, documents, branches, appeals, delegations, headAlerts, uow, tx, _audit);
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

    [Fact]
    public async Task Create_GeneralLetter_GeneratesNumberDateAndOriginalMessage()
    {
        var letter = await _service.CreateAsync(
            new CreateReviewLetterRequest(null, "<p>نطلب التوجيه في الأمر</p>"),
            _lawyer1.Id, "المحامي الأول", _branchId);

        Assert.StartsWith("DAM-", letter.LetterNumber);
        Assert.Matches($"^DAM-{DateTime.UtcNow.Year}-\\d{{4}}$", letter.LetterNumber);
        Assert.Null(letter.DocumentId);
        Assert.False(letter.IsAnswered);

        var original = Assert.Single(letter.Messages);
        Assert.Equal(ReviewLetterMessage.KindLetter, original.Kind);
        Assert.Equal(letter.LetterNumber, original.MessageNumber);
        Assert.Contains("create_review_letter", _audit.Actions);

        // النص يُحفظ معقّمًا (HTML مسموح فقط) ويُستخلص منه نص عادي للبحث
        Assert.Contains("نطلب التوجيه في الأمر", letter.Messages.Single().BodyHtml);
    }

    [Fact]
    public async Task Create_EmptyBody_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(
            new CreateReviewLetterRequest(null, "<p></p>"),
            _lawyer1.Id, "المحامي الأول", _branchId));
    }

    [Fact]
    public async Task Create_LinkedLetter_BuildsFileContextFromDocument()
    {
        var doc = await AddDocumentAsync(_lawyer1);

        var letter = await _service.CreateAsync(
            new CreateReviewLetterRequest(doc.Id, "<p>مطالعة بملف</p>"),
            _lawyer1.Id, "المحامي الأول", _branchId);

        Assert.NotNull(letter.FileContext);
        Assert.Equal("أحمد محمد العلي", letter.FileContext.ExecutedName);
        Assert.Equal("77/2026", letter.FileContext.FileNumber);
        Assert.Equal("دائرة تنفيذ دمشق", letter.FileContext.Court);
    }

    [Fact]
    public async Task Create_OnForeignFile_IsDenied()
    {
        var doc = await AddDocumentAsync(_lawyer2);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateAsync(
            new CreateReviewLetterRequest(doc.Id, "<p>x</p>"),
            _lawyer1.Id, "المحامي الأول", _branchId));
    }

    [Fact]
    public async Task Reply_BySameBranchHead_MarksAnsweredWithGeneratedReplyNumber()
    {
        var letter = await _service.CreateAsync(
            new CreateReviewLetterRequest(null, "<p>نطلب التوجيه</p>"),
            _lawyer1.Id, "المحامي الأول", _branchId);

        var reply = await _service.ReplyAsync(
            letter.Id, new ReplyReviewLetterRequest("<p>يوجه بالتالي...</p>"),
            _head.Id, "رئيس القسم", _branchId);

        Assert.Equal(ReviewLetterMessage.KindReply, reply.Kind);
        Assert.Matches("^DAM-\\d{4}-\\d{4}$", reply.MessageNumber);

        var stored = await _service.GetByIdAsync(letter.Id, _head.Id, UserRole.Head, _branchId);
        Assert.True(stored.IsAnswered);
        Assert.Equal(2, stored.Messages.Count);
        Assert.Equal(0, await _service.CountPendingForHeadAsync(_branchId));
    }

    [Fact]
    public async Task Reply_ByOtherBranchHead_IsDenied()
    {
        var letter = await _service.CreateAsync(
            new CreateReviewLetterRequest(null, "<p>سؤال</p>"),
            _lawyer1.Id, "المحامي الأول", _branchId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.ReplyAsync(
            letter.Id, new ReplyReviewLetterRequest("<p>رد</p>"),
            _otherBranchHead.Id, "رئيس حلب", _otherBranchId));
    }

    [Fact]
    public async Task AddAddendum_ByOwnerResetsAnswered_AndOthersAreDenied()
    {
        var letter = await _service.CreateAsync(
            new CreateReviewLetterRequest(null, "<p>الأصل</p>"),
            _lawyer1.Id, "المحامي الأول", _branchId);
        await _service.ReplyAsync(letter.Id, new ReplyReviewLetterRequest("<p>الرد</p>"),
            _head.Id, "رئيس القسم", _branchId);

        // محامٍ آخر لا يستطيع إضافة لاحق لكتاب غيره
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.AddAddendumAsync(
            letter.Id, new AddReviewLetterAddendumRequest("<p>لاحق</p>"),
            _lawyer2.Id, "المحامي الثاني"));

        var addendum = await _service.AddAddendumAsync(
            letter.Id, new AddReviewLetterAddendumRequest("<p>لاحق جديد بعد الرد</p>"),
            _lawyer1.Id, "المحامي الأول");

        Assert.Equal(ReviewLetterMessage.KindAddendum, addendum.Kind);
        Assert.NotEqual(letter.LetterNumber, addendum.MessageNumber);

        var stored = await _service.GetByIdAsync(letter.Id, _head.Id, UserRole.Head, _branchId);
        Assert.False(stored.IsAnswered);
        Assert.Equal(3, stored.Messages.Count);
        Assert.Equal(ReviewLetterMessage.KindAddendum, stored.Messages[^1].Kind);
    }

    [Fact]
    public async Task Search_ScopesByRole()
    {
        await _service.CreateAsync(new CreateReviewLetterRequest(null, "<p>كتاب الأول</p>"),
            _lawyer1.Id, "المحامي الأول", _branchId);
        await _service.CreateAsync(new CreateReviewLetterRequest(null, "<p>كتاب الثاني</p>"),
            _lawyer2.Id, "المحامي الثاني", _branchId);
        await _service.CreateAsync(new CreateReviewLetterRequest(null, "<p>كتاب فرع آخر</p>"),
            _otherBranchHead.Id is not 0 ? _otherBranchHead.Id : 1, "رئيس حلب", _otherBranchId);

        var lawyerView = await _service.SearchAsync(_lawyer1.Id, UserRole.Lawyer, _branchId, null, 1, 20);
        Assert.Equal(1, lawyerView.TotalCount);
        Assert.All(lawyerView.Items, i => Assert.Equal("المحامي الأول", i.LawyerName));

        var headView = await _service.SearchAsync(_head.Id, UserRole.Head, _branchId, null, 1, 20);
        Assert.Equal(2, headView.TotalCount);

        var managerView = await _service.SearchAsync(1, UserRole.Admin, null, null, 1, 20);
        Assert.Equal(3, managerView.TotalCount);
    }

    [Fact]
    public async Task Search_ByExecutedNameFindsLinkedLetters()
    {
        var doc = await AddDocumentAsync(_lawyer1);
        await _service.CreateAsync(new CreateReviewLetterRequest(doc.Id, "<p>مطالعة ملف أحمد</p>"),
            _lawyer1.Id, "المحامي الأول", _branchId);
        await _service.CreateAsync(new CreateReviewLetterRequest(null, "<p>كتاب عام</p>"),
            _lawyer1.Id, "المحامي الأول", _branchId);

        var hit = await _service.SearchAsync(_lawyer1.Id, UserRole.Lawyer, _branchId, "أحمد", 1, 20);
        Assert.Equal(1, hit.TotalCount);
        Assert.NotNull(hit.Items[0].FileContext);
    }

    [Fact]
    public async Task ListByDocument_FollowerViaDelegation_CanViewStrangersCannot()
    {
        var sourceDoc = await AddDocumentAsync(_lawyer1);
        var followerDoc = await AddDocumentAsync(_lawyer2);
        _db.DocumentDelegations.Add(new DocumentDelegation
        {
            SourceDocumentId = sourceDoc.Id,
            TargetDocument = followerDoc,
            AssignedLawyerId = _lawyer2.Id,
            CreatedById = _lawyer2.Id,
        });
        await _db.SaveChangesAsync();

        var linkedLetter = await _service.CreateAsync(
            new CreateReviewLetterRequest(sourceDoc.Id, "<p>مطالعة الملف المنيب</p>"),
            _lawyer1.Id, "المحامي الأول", _branchId);

        // المتابع بالإنابة يرى كتب الملف
        var followerView = await _service.ListByDocumentAsync(
            sourceDoc.Id, _lawyer2.Id, UserRole.Lawyer, _branchId);
        Assert.Single(followerView);

        // محامٍ غريب لا يرى
        var stranger = NewUser("stranger", "محامٍ غريب", UserRole.Lawyer, _branchId);
        _db.Users.Add(stranger);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.GetByIdAsync(linkedLetter.Id, stranger.Id, UserRole.Lawyer, _branchId));
    }

    [Fact]
    public async Task ListByDocument_FollowerViaAppeal_CanView()
    {
        var doc = await AddDocumentAsync(_lawyer1);
        var assignee = NewUser("assignee", "محامي الاستئناف", UserRole.Lawyer, _branchId);
        _db.Users.Add(assignee);
        await _db.SaveChangesAsync();
        _db.DocumentAppeals.Add(new DocumentAppeal
        {
            DocumentId = doc.Id,
            AssignedLawyerId = assignee.Id,
            CreatedById = _lawyer1.Id,
        });
        await _db.SaveChangesAsync();

        await _service.CreateAsync(
            new CreateReviewLetterRequest(doc.Id, "<p>مطالعة الملف المستأنف</p>"),
            _lawyer1.Id, "المحامي الأول", _branchId);

        var view = await _service.ListByDocumentAsync(doc.Id, assignee.Id, UserRole.Lawyer, _branchId);
        Assert.Single(view);
    }

    [Fact]
    public async Task GeneratedNumbers_AreUniqueAcrossManyLetters()
    {
        var numbers = new HashSet<string>();
        for (var i = 0; i < 5; i++)
        {
            var letter = await _service.CreateAsync(
                new CreateReviewLetterRequest(null, $"<p>كتاب رقم {i}</p>"),
                _lawyer1.Id, "المحامي الأول", _branchId);
            Assert.True(numbers.Add(letter.LetterNumber));
        }
    }

    [Fact]
    public async Task Reply_CreatesAlertForAuthorLawyer_AndCoalescesConsecutiveReplies()
    {
        var letter = await _service.CreateAsync(
            new CreateReviewLetterRequest(null, "<p>الأصل</p>"),
            _lawyer1.Id, "المحامي الأول", _branchId);

        await _service.ReplyAsync(letter.Id, new ReplyReviewLetterRequest("<p>الرد الأول</p>"),
            _head.Id, "رئيس القسم", _branchId);
        await _service.ReplyAsync(letter.Id, new ReplyReviewLetterRequest("<p>رد ثانٍ قبل الاطلاع</p>"),
            _head.Id, "رئيس القسم", _branchId);

        // قائمة المحامي تعرض الكتاب مع علم «رد غير مطّلع عليه»
        var list = await _service.SearchAsync(_lawyer1.Id, UserRole.Lawyer, _branchId, null, 1, 20);
        Assert.True(list.Items[0].HasUnseenReply);
        Assert.Equal(1, await _service.CountUnseenRepliesForLawyerAsync(_lawyer1.Id));

        // حالة الإطلاق خاصة بمحامي الكتاب: تُحجب عن رئيس القسم والمدير
        var headView = await _service.SearchAsync(_head.Id, UserRole.Head, _branchId, null, 1, 20);
        Assert.False(headView.Items[0].HasUnseenReply);
        var managerView = await _service.SearchAsync(1, UserRole.Admin, null, null, 1, 20);
        Assert.All(managerView.Items, i => Assert.False(i.HasUnseenReply));

        // رئيس قسم آخر لا يتأثر بعدّاد محامٍ غير صاحب الكتاب
        Assert.Equal(0, await _service.CountUnseenRepliesForLawyerAsync(_lawyer2.Id));
    }

    [Fact]
    public async Task MarkRepliesSeen_ByOwnerClearsBadge_OthersDenied()
    {
        var letter = await _service.CreateAsync(
            new CreateReviewLetterRequest(null, "<p>سؤال</p>"),
            _lawyer1.Id, "المحامي الأول", _branchId);
        await _service.ReplyAsync(letter.Id, new ReplyReviewLetterRequest("<p>جواب</p>"),
            _head.Id, "رئيس القسم", _branchId);

        // محامٍ آخر لا يستطيع التعليم على كتاب غيره
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.MarkRepliesSeenAsync(letter.Id, _lawyer2.Id));

        await _service.MarkRepliesSeenAsync(letter.Id, _lawyer1.Id);

        Assert.Equal(0, await _service.CountUnseenRepliesForLawyerAsync(_lawyer1.Id));
        var list = await _service.SearchAsync(_lawyer1.Id, UserRole.Lawyer, _branchId, null, 1, 20);
        Assert.False(list.Items[0].HasUnseenReply);

        // الإطلاع لا يزيل حالة «تم الرد» ولا يغيّر عدد الرسائل
        Assert.True(list.Items[0].IsAnswered);
    }

    [Fact]
    public async Task AddAddendum_AfterSeenReply_ReactivatesUnseenFlowOnNextReply()
    {
        var letter = await _service.CreateAsync(
            new CreateReviewLetterRequest(null, "<p>س</p>"),
            _lawyer1.Id, "المحامي الأول", _branchId);
        await _service.ReplyAsync(letter.Id, new ReplyReviewLetterRequest("<p>ج</p>"),
            _head.Id, "رئيس القسم", _branchId);
        await _service.MarkRepliesSeenAsync(letter.Id, _lawyer1.Id);
        Assert.Equal(0, await _service.CountUnseenRepliesForLawyerAsync(_lawyer1.Id));

        await _service.AddAddendumAsync(letter.Id,
            new AddReviewLetterAddendumRequest("<p>استفسار إضافي</p>"), _lawyer1.Id, "المحامي الأول");
        await _service.ReplyAsync(letter.Id, new ReplyReviewLetterRequest("<p>جواب اللاحق</p>"),
            _head.Id, "رئيس القسم", _branchId);

        Assert.Equal(1, await _service.CountUnseenRepliesForLawyerAsync(_lawyer1.Id));
    }
}
