using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات سلوكية لمستودع ملخصات التنبيهات (PR-4 / S2): الإسقاط في SQL لا يجلب
/// النص الكامل، و<code>MessageLength</code> يطابق عدد نقاط Unicode، والعدّ الكلي
/// مستقل عن الصفحة، والترتيب حتمي عند تساوي <c>LastContentChangedAt</c>،
/// والفلاتر (الأرشفة/الفرع/غير المقروء/الإنابة) تعمل على الاستعلام نفسه.
/// </summary>
public class HeadAlertRepositorySummaryTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IHeadAlertRepository _repository;
    private readonly int _branchA;
    private readonly int _branchB;
    private readonly User _head;
    private readonly User _lawyer1;
    private readonly User _lawyer2;
    private readonly User _lawyerOtherBranch;

    public HeadAlertRepositorySummaryTests()
    {
        _db = TestDb.Create();

        var branchA = new Branch { Name = "دمشق", Code = "DAM" };
        var branchB = new Branch { Name = "حلب", Code = "ALP" };
        _db.Branches.AddRange(branchA, branchB);
        _db.SaveChanges();
        _branchA = branchA.Id;
        _branchB = branchB.Id;

        _head = NewUser("head_a", "رئيس قسم دمشق", UserRole.Head, _branchA);
        _lawyer1 = NewUser("law_a1", "محامي دمشق 1", UserRole.Lawyer, _branchA);
        _lawyer2 = NewUser("law_a2", "محامي دمشق 2", UserRole.Lawyer, _branchA);
        _lawyerOtherBranch = NewUser("law_b1", "محامي حلب", UserRole.Lawyer, _branchB);
        _db.Users.AddRange(_head, _lawyer1, _lawyer2, _lawyerOtherBranch);
        _db.SaveChanges();

        _repository = new HeadAlertRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Oversight_PreviewIsCodePointSafe_AndLengthMatchesUnicodeCount()
    {
        // 600 إيموجي = 600 نقطة Unicode لكن 1200 وحدة UTF-16: أي قصّ بوحدات UTF-16
        // كان سيقطع surrogate pair ويُظهر طولًا مضاعفًا.
        var astral = string.Concat(Enumerable.Repeat("😀", 600));
        var exact = new string('ا', HeadAlertTextLimits.PreviewMaxElements);
        var astralAlert = AddAlert(_branchA, _head.Id, astral, At(0), recipientIds: new[] { _lawyer1.Id });
        var exactAlert = AddAlert(_branchA, _head.Id, exact, At(1), recipientIds: new[] { _lawyer1.Id });

        var page = await _repository.ListSummariesForOversightAsync(
            AlertArchiveFilter.Active, delegationId: null, skip: 0, take: 10);

        Assert.Equal(2, page.TotalCount);
        var astralItem = Assert.Single(page.Items, i => i.Id == astralAlert.Id);
        Assert.Equal(600, astralItem.MessageLength);
        Assert.Equal(HeadAlertTextLimits.CountCodePoints(astral), astralItem.MessageLength);
        Assert.True(astralItem.PreviewTruncated);
        Assert.Equal(HeadAlertTextLimits.PreviewMaxElements, HeadAlertTextLimits.CountCodePoints(astralItem.MessagePreview));
        Assert.EndsWith(HeadAlertTextLimits.Ellipsis, astralItem.MessagePreview);
        Assert.DoesNotContain('\uFFFD', astralItem.MessagePreview);
        // لا نص كامل في الملخص: المعاينة أقصر من الرسالة الأصلية.
        Assert.True(astralItem.MessagePreview.Length < astral.Length);

        var exactItem = Assert.Single(page.Items, i => i.Id == exactAlert.Id);
        Assert.Equal(HeadAlertTextLimits.PreviewMaxElements, exactItem.MessageLength);
        Assert.False(exactItem.PreviewTruncated);
        Assert.Equal(exact, exactItem.MessagePreview);
    }

    [Fact]
    public async Task Oversight_TotalCountIndependentOfPage_AndOrderIsStableByIdOnEqualTimestamp()
    {
        // نفس الطابع الزمني للثلاثة: الترتيب الحتمي ThenByDescending(Id).
        var first = AddAlert(_branchA, _head.Id, "أول", At(5));
        var second = AddAlert(_branchA, _head.Id, "ثاني", At(5));
        var third = AddAlert(_branchA, _head.Id, "ثالث", At(5));

        var page1 = await _repository.ListSummariesForOversightAsync(
            AlertArchiveFilter.Active, delegationId: null, skip: 0, take: 2);
        var page2 = await _repository.ListSummariesForOversightAsync(
            AlertArchiveFilter.Active, delegationId: null, skip: 2, take: 2);

        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(3, page2.TotalCount);
        Assert.Equal(new[] { third.Id, second.Id }, page1.Items.Select(i => i.Id));
        Assert.Equal(new[] { first.Id }, page2.Items.Select(i => i.Id));

        // حدود الترقيم صريحة: لا Take سالب/صفر ولا تجاوز السقف.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _repository.ListSummariesForOversightAsync(AlertArchiveFilter.Active, null, skip: -1, take: 10));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _repository.ListSummariesForOversightAsync(AlertArchiveFilter.Active, null, skip: 0, take: 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _repository.ListSummariesForOversightAsync(
                AlertArchiveFilter.Active, null, skip: 0, take: AlertPageRequest.MaxPageSize + 1));
    }

    [Fact]
    public async Task ArchiveFilter_Active_All_OnlyArchived_AreAppliedBeforeCount()
    {
        var active = AddAlert(_branchA, _head.Id, "نشط", At(0));
        var archived = AddAlert(_branchA, _head.Id, "مؤرشف", At(1), archived: true);

        var onlyActive = await _repository.ListSummariesForOversightAsync(
            AlertArchiveFilter.Active, null, 0, 10);
        var all = await _repository.ListSummariesForOversightAsync(
            AlertArchiveFilter.All, null, 0, 10);
        var onlyArchived = await _repository.ListSummariesForOversightAsync(
            AlertArchiveFilter.OnlyArchived, null, 0, 10);

        Assert.Equal(1, onlyActive.TotalCount);
        Assert.Equal(active.Id, Assert.Single(onlyActive.Items).Id);

        Assert.Equal(2, all.TotalCount);
        Assert.Contains(all.Items, i => i.Id == archived.Id && i.IsArchived && i.ArchivedAt is not null);

        Assert.Equal(1, onlyArchived.TotalCount);
        Assert.Equal(archived.Id, Assert.Single(onlyArchived.Items).Id);
    }

    [Fact]
    public async Task RecipientList_LawyerIgnoresBranch_HeadIsBranchScoped_AndCountersFollowCaller()
    {
        var readAt = At(9);
        // محامي الفرع A: مستلم في فرعيه A وB (الاستلام شخصي بلا قيد فرع).
        var unreadInA = AddAlert(_branchA, _head.Id, "غير مقروء A", At(1), recipientIds: new[] { _lawyer1.Id });
        var readInA = AddAlert(_branchA, _head.Id, "مقروء A", At(0), recipientIds: new[] { _lawyer1.Id });
        var unreadInB = AddAlert(_branchB, _lawyerOtherBranch.Id, "غير مقروء B", At(2), recipientIds: new[] { _lawyer1.Id });
        // تنبيه لمحامٍ آخر لا يظهر له.
        AddAlert(_branchA, _head.Id, "لمحامٍ آخر", At(3), recipientIds: new[] { _lawyer2.Id });
        // رئيس الفرع A مستلم في فرعيه A وB: قيد الفرع يستبعد B.
        var headInA = AddAlert(_branchA, _head.Id, "للرئيس A", At(4), recipientIds: new[] { _head.Id });
        AddAlert(_branchB, _lawyerOtherBranch.Id, "للرئيس B", At(5), recipientIds: new[] { _head.Id });

        MarkRead(readInA.Id, _lawyer1.Id, readAt);

        var lawyerPage = await _repository.ListSummariesForRecipientAsync(
            _lawyer1.Id, branchId: null, AlertArchiveFilter.Active, unreadOnly: false,
            delegationId: null, skip: 0, take: 10, includeCounters: false);

        Assert.Equal(3, lawyerPage.TotalCount);
        Assert.Equal(new[] { unreadInB.Id, unreadInA.Id, readInA.Id }, lawyerPage.Items.Select(i => i.Id));
        Assert.False(Assert.Single(lawyerPage.Items, i => i.Id == unreadInA.Id).IsRead);
        Assert.True(Assert.Single(lawyerPage.Items, i => i.Id == readInA.Id).IsRead);
        // عدّادات المستلمين لا تُكشف لقائمة محامٍ (§9.2).
        Assert.All(lawyerPage.Items, i => Assert.Null(i.RecipientCount));
        Assert.All(lawyerPage.Items, i => Assert.Null(i.UnreadCount));

        var lawyerUnread = await _repository.ListSummariesForRecipientAsync(
            _lawyer1.Id, branchId: null, AlertArchiveFilter.Active, unreadOnly: true,
            delegationId: null, skip: 0, take: 10, includeCounters: false);
        Assert.Equal(2, lawyerUnread.TotalCount);
        Assert.DoesNotContain(lawyerUnread.Items, i => i.Id == readInA.Id);

        var headPage = await _repository.ListSummariesForRecipientAsync(
            _head.Id, branchId: _branchA, AlertArchiveFilter.Active, unreadOnly: false,
            delegationId: null, skip: 0, take: 10, includeCounters: true);
        Assert.Equal(headInA.Id, Assert.Single(headPage.Items).Id);

        // رئيس النطاق يرى عدّادَي المستلمين محسوبين في القاعدة.
        Assert.Equal(1, headPage.Items[0].RecipientCount);
        Assert.Equal(1, headPage.Items[0].UnreadCount);
    }

    [Fact]
    public async Task SentList_ReturnsOnlyCreatorRowsInBranch_WithBaseCounters()
    {
        var sent = AddAlert(_branchA, _head.Id, "صادر", At(0), recipientIds: new[] { _lawyer1.Id, _lawyer2.Id });
        // نفس المُنشئ لكن فرع آخر — خارج نطاق قائمة الصادر.
        AddAlert(_branchB, _head.Id, "صادر فرع آخر", At(1), recipientIds: new[] { _lawyerOtherBranch.Id });
        // محامٍ منشئ في الفرع نفسه — ليس صادر رئيس.
        AddAlert(_branchA, _lawyer1.Id, "أنشأه محامٍ", At(2), recipientIds: new[] { _lawyer2.Id });

        MarkRead(sent.Id, _lawyer1.Id, At(9));

        var page = await _repository.ListSummariesSentAsync(
            _head.Id, _branchA, AlertArchiveFilter.Active, skip: 0, take: 10);

        Assert.Equal(1, page.TotalCount);
        var item = Assert.Single(page.Items);
        Assert.Equal(sent.Id, item.Id);
        Assert.Equal(2, item.RecipientCount);
        Assert.Equal(1, item.UnreadCount);
        // المُرسِل ليس مستلمًا: لا حالة قراءة شخصية له.
        Assert.Null(item.IsRead);
    }

    [Fact]
    public async Task DelegationFilter_AppliesToRecipientAndOversightLists()
    {
        AddAlert(_branchA, _head.Id, "إنابة 77", At(0), delegationId: 77);
        AddAlert(_branchA, _head.Id, "إنابة 88", At(1), delegationId: 88);
        AddAlert(_branchA, _head.Id, "بلا إنابة", At(2));

        var oversight = await _repository.ListSummariesForOversightAsync(
            AlertArchiveFilter.Active, delegationId: 77, skip: 0, take: 10);
        Assert.Equal(1, oversight.TotalCount);
        Assert.Equal(77, Assert.Single(oversight.Items).DelegationId);

        var recipient = await _repository.ListSummariesForRecipientAsync(
            _head.Id, branchId: null, AlertArchiveFilter.Active, unreadOnly: false,
            delegationId: 88, skip: 0, take: 10, includeCounters: false);
        Assert.Equal(0, recipient.TotalCount);
        Assert.Empty(recipient.Items);
    }

    [Fact]
    public async Task Summary_TitleDistinguishesBlankExistingDocumentFromDeleted_AndMatchesSingleRow()
    {
        // صف قديم بلا حقول عرض لكن الملف قائم ⇒ «مستند {id}» لا «مستند محذوف».
        var existing = AddBlankDocument(isDeleted: false);
        var existingAlert = AddDocumentAlert(existing.Id, "قائم بلا حقول", At(0));

        // ملف محذوف منطقيًا: مُستثنى بـQuery Filter ⇒ «مستند محذوف».
        var deleted = AddBlankDocument(isDeleted: true);
        var deletedAlert = AddDocumentAlert(deleted.Id, "محذوف", At(1));

        // تنبيه بلا مرجع ملف ⇒ عنوان فارغ (ليس «محذوفًا»).
        var branchAlert = AddAlert(_branchA, _head.Id, "تعميم", At(2));

        var page = await _repository.ListSummariesForOversightAsync(
            AlertArchiveFilter.Active, delegationId: null, skip: 0, take: 10);

        var existingItem = Assert.Single(page.Items, i => i.Id == existingAlert.Id);
        Assert.Equal($"مستند {existing.Id}", existingItem.DocumentTitle);
        Assert.Equal("مستند محذوف", Assert.Single(page.Items, i => i.Id == deletedAlert.Id).DocumentTitle);
        Assert.Equal(string.Empty, Assert.Single(page.Items, i => i.Id == branchAlert.Id).DocumentTitle);

        // القراءة الفردية (مسار جسم 201) تعطي عنوان الصف نفسه حقلًا بحقل.
        var single = await _repository.GetSummaryAsync(
            existingAlert.Id, recipientUserId: null, includeCounters: true);
        Assert.NotNull(single);
        Assert.Equal(existingItem.DocumentTitle, single!.DocumentTitle);
    }

    private HeadAlert AddAlert(
        int branchId,
        int createdById,
        string message,
        DateTime changedAt,
        bool archived = false,
        int? delegationId = null,
        int[]? recipientIds = null)
    {
        var alert = new HeadAlert
        {
            BranchId = branchId,
            CreatedById = createdById,
            TargetType = HeadAlertTargetType.Branch,
            Kind = HeadAlertKindCatalog.ManualToBranch,
            Message = message,
            CreatedAt = changedAt,
            LastContentChangedAt = changedAt,
            IsArchived = archived,
            ArchivedAt = archived ? changedAt.AddDays(1) : null,
            DelegationId = delegationId,
            Recipients = (recipientIds ?? Array.Empty<int>())
                .Select(id => new HeadAlertRecipient { UserId = id })
                .ToList(),
        };
        _db.HeadAlerts.Add(alert);
        _db.SaveChanges();
        return alert;
    }

    /// <summary>
    /// ملف بكل حقول العرض الفارغة — محاكاة صفوف قديمة/مستوردة سبقت اشتقاق
    /// <c>DocumentType</c> في <c>DocumentText.FillDerivedFields</c>.
    /// </summary>
    private Document AddBlankDocument(bool isDeleted)
    {
        var document = new Document
        {
            BranchId = _branchA,
            CreatedById = _head.Id,
            DocumentType = null,
            BorrowerName = null,
            BorrowerFather = null,
            BorrowerFamily = null,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? At(0) : null,
        };
        _db.Documents.Add(document);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return document;
    }

    private HeadAlert AddDocumentAlert(int documentId, string message, DateTime changedAt)
    {
        var alert = new HeadAlert
        {
            BranchId = _branchA,
            CreatedById = _head.Id,
            TargetType = HeadAlertTargetType.Document,
            Kind = HeadAlertKindCatalog.ManualToDocument,
            DocumentId = documentId,
            Message = message,
            CreatedAt = changedAt,
            LastContentChangedAt = changedAt,
        };
        _db.HeadAlerts.Add(alert);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return alert;
    }

    private void MarkRead(int alertId, int userId, DateTime readAt)
    {
        var recipient = _db.HeadAlertRecipients.Single(r => r.HeadAlertId == alertId && r.UserId == userId);
        recipient.IsRead = true;
        recipient.ReadAt = readAt;
        _db.SaveChanges();
        // فصل التتبُّع حتى لا تعيد الاستعلامات كياناتٍ متتبَّعة من الاختبار.
        _db.ChangeTracker.Clear();
    }

    private static DateTime At(int minutes) =>
        new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddMinutes(minutes);

    private static User NewUser(string username, string fullName, UserRole role, int branchId) => new()
    {
        Username = username,
        FullName = fullName,
        Role = role,
        BranchId = branchId,
        IsActive = true,
        PasswordHash = "x",
    };
}
