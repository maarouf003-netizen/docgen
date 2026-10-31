using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

/// <summary>
/// مصفوفة صلاحيات PR-4 (الخطة §9.2/§9.3/§12) على مسارات الخدمة الثمانية:
/// القوائم بحسب الدور، والصادر للرئيس/الشعبة فقط، وتفاصيل المستلم بأولوية مطلقة
/// (بلا شرط فرع)، ثم المنشئ، ثم الإشراف، وعدمه 404 (قيمة <c>null</c>) بلا تسريب وجود.
/// </summary>
public class HeadAlertServicePermissionTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IHeadAlertService _service;
    private readonly int _branchA;
    private readonly int _branchB;
    private readonly User _headA;
    private readonly User _headB;
    private readonly User _subA;
    private readonly User _lawyerA;
    private readonly User _lawyerB;
    private readonly User _manager;
    private readonly User _admin;

    private readonly HeadAlert _alertA;
    private readonly HeadAlert _alertB;
    private readonly HeadAlert _alertHead;
    private readonly HeadAlert _alertHeadInB;
    private readonly HeadAlert _alertOther;

    public HeadAlertServicePermissionTests()
    {
        _db = TestDb.Create();

        var branchA = new Branch { Name = "دمشق", Code = "DAM" };
        var branchB = new Branch { Name = "حلب", Code = "ALP" };
        _db.Branches.AddRange(branchA, branchB);
        _db.SaveChanges();
        _branchA = branchA.Id;
        _branchB = branchB.Id;

        _headA = NewUser("head_a", "رئيس قسم دمشق", UserRole.Head, _branchA);
        _headB = NewUser("head_b", "رئيس قسم حلب", UserRole.Head, _branchB);
        _subA = NewUser("sub_a", "رئيس شعبة دمشق", UserRole.SubHead, _branchA);
        _lawyerA = NewUser("law_a", "محامي دمشق", UserRole.Lawyer, _branchA);
        _lawyerB = NewUser("law_b", "محامي حلب", UserRole.Lawyer, _branchB);
        _manager = NewUser("manager", "مدير", UserRole.Manager, null);
        _admin = NewUser("admin", "مشرف", UserRole.Admin, null);
        _db.Users.AddRange(_headA, _headB, _subA, _lawyerA, _lawyerB, _manager, _admin);
        _db.SaveChanges();

        // إنابات مسار by-delegation: 77 مالك مصدرها headA (فمحامي دمشق ليس طرفًا)،
        // و88 مالك مصدرها lawyerA (فهو طرف). المعرّفان صريحان كما في مسار الإنتاج.
        HeadAlertReferences.SeedDelegations(_db, _branchA, _headA.Id, 77);
        HeadAlertReferences.SeedDelegations(_db, _branchA, _lawyerA.Id, 88);

        // A: منشئ headA ومستلمه lawyerA فقط.
        _alertA = AddAlert(_branchA, _headA.Id, "تنبيه دمشق", _lawyerA.Id, _lawyerA.Id);
        // B: رئيس حلب ينشئ ومحامي دمشق يستلم — الاستلام الشخصي لا يقيده الفرع.
        _alertB = AddAlert(_branchB, _headB.Id, "تنبيه حلب", _lawyerA.Id, _lawyerA.Id);
        // A: تنبيه استلام رئيس في فرعه (منشئه headB).
        _alertHead = AddAlert(_branchA, _headB.Id, "استلام رئيس دمشق", _headA.Id, _headA.Id);
        // B: نفس الرئيس مستلم لكن خارج فرعه — مستبعد من قائمته الفرعية.
        _alertHeadInB = AddAlert(_branchB, _headB.Id, "استلام رئيس حلب", _headA.Id, _headA.Id);
        // A لكن مستلمه محامي آخر: لا يظهر لمحامي دمشق.
        _alertOther = AddAlert(_branchA, _headA.Id, "تنبيه لمحامٍ آخر", _lawyerB.Id, _lawyerB.Id);

        var users = new UserRepository(_db);
        _service = new HeadAlertService(
            new HeadAlertRepository(_db),
            new DocumentRepository(_db),
            users,
            new Repository<Branch>(_db),
            new DelegationRepository(_db),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            new FakeAuditLogger());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ReceivedLists_FollowRoleScope_BranchScopedForHeads_BranchlessForLawyers()
    {
        // محامي: استلامه الشخصي عبر الفرعين (A وB) ولا يرى تنبيه غيره.
        var lawyerPage = await _service.ListForActorAsync(
            ActorFor(_lawyerA), new HeadAlertListQuery { Page = FullPage() });
        Assert.Equal(2, lawyerPage.TotalCount);
        Assert.Contains(lawyerPage.Items, i => i.Id == _alertA.Id);
        Assert.Contains(lawyerPage.Items, i => i.Id == _alertB.Id);
        Assert.DoesNotContain(lawyerPage.Items, i => i.Id == _alertOther.Id);

        // رئيس قسم: ما استلمه في فرعه فقط؛ تنبيه فرع حلب مستبعد رغم أنه مستلمه.
        var headPage = await _service.ListForActorAsync(
            ActorFor(_headA), new HeadAlertListQuery { Page = FullPage() });
        Assert.Equal(_alertHead.Id, Assert.Single(headPage.Items).Id);
        Assert.Equal(1, headPage.Items[0].RecipientCount);

        // رئيس شعبة بلا استلام: قائمة فارغة (لا يرى ما في فرعه بلا استلام).
        var subPage = await _service.ListForActorAsync(
            ActorFor(_subA), new HeadAlertListQuery { Page = FullPage() });
        Assert.Empty(subPage.Items);
        Assert.Equal(0, subPage.TotalCount);

        // المدير/المشرف: إشراف كل الفروع، وحالة القراءة غير مستلم-محددة.
        var managerPage = await _service.ListForActorAsync(
            ActorFor(_manager), new HeadAlertListQuery { Page = FullPage() });
        Assert.Equal(5, managerPage.TotalCount);
        Assert.All(managerPage.Items, i => Assert.Null(i.IsRead));
        Assert.All(managerPage.Items, i => Assert.NotNull(i.RecipientCount));

        var adminPage = await _service.ListForActorAsync(
            ActorFor(_admin), new HeadAlertListQuery { Page = FullPage() });
        Assert.Equal(5, adminPage.TotalCount);

        // غير المقروء متاح للمحامي/الرئيس، ويعتمد على صف الاستلام الشخصي.
        var lawyerUnread = await _service.ListForActorAsync(
            ActorFor(_lawyerA),
            new HeadAlertListQuery { UnreadOnly = true, Page = FullPage() });
        Assert.Equal(2, lawyerUnread.TotalCount);
    }

    [Theory]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.Admin)]
    public async Task ReceivedList_RejectsUnreadOnly_ForOversightRoles(UserRole role)
    {
        var actor = new HeadAlertActor(_manager.Id, role, null);
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ListForActorAsync(
            actor, new HeadAlertListQuery { UnreadOnly = true, Page = FullPage() }));
    }

    [Theory]
    [InlineData(UserRole.Lawyer, false)]
    [InlineData(UserRole.Head, true)]
    [InlineData(UserRole.SubHead, true)]
    [InlineData(UserRole.Manager, false)]
    [InlineData(UserRole.Admin, false)]
    public async Task SentList_IsAllowedOnlyForHeadAndSubHead(UserRole role, bool allowed)
    {
        var actor = new HeadAlertActor(_headA.Id, role, _branchA);

        if (!allowed)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.ListSentAsync(actor, AlertArchiveFilter.Active, FullPage()));
            return;
        }

        var page = await _service.ListSentAsync(actor, AlertArchiveFilter.Active, FullPage());
        Assert.Equal(2, page.TotalCount); // alertA + alertOther صادر headA في فرعه
        Assert.All(page.Items, i => Assert.Equal(_headA.FullName, i.CreatedByName));
    }

    [Theory]
    [InlineData(UserRole.Head)]
    [InlineData(UserRole.SubHead)]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.Admin)]
    public async Task ByDelegation_NonLawyerRoles_AreRejected(UserRole role)
    {
        var actor = new HeadAlertActor(_lawyerA.Id, role, _branchA);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ListByDelegationAsync(88, actor, FullPage()));
    }

    [Fact]
    public async Task ByDelegation_LawyerMustBeParty_AndMissingDelegationIsRejected()
    {
        // 77 مالك مصدرها headA ⇒ محامي دمشق ليس طرفًا: الرفض من الخدمة لا من المتحكم
        // (مراجعة PR-4 `ح‑1`: المستودع لا يقيّد النتيجة بالمستخدم).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ListByDelegationAsync(77, ActorFor(_lawyerA), FullPage()));

        // إنابة غير موجودة ⇒ الرفض نفسه، بلا تسريب وجود.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ListByDelegationAsync(999_999, ActorFor(_lawyerA), FullPage()));

        // 88 مالك مصدرها محامي دمشق ⇒ طرف، وتُقبل القائمة (فارغة هنا).
        var page = await _service.ListByDelegationAsync(88, ActorFor(_lawyerA), FullPage());
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Details_RecipientHasAbsolutePriority_ThenCreator_ThenOversight_ElseNull()
    {
        // المستلم: داخل الفرع...
        var recipientInBranch = await _service.GetDetailsAsync(_alertA.Id, ActorFor(_lawyerA));
        Assert.NotNull(recipientInBranch);
        Assert.False(recipientInBranch!.CanViewRecipients);
        Assert.False(recipientInBranch.IsRead);

        // ...وخارج فرعه (إنابة/تنبيه من فرع آخر) — بلا شرط فرع.
        var recipientOutsideBranch = await _service.GetDetailsAsync(_alertB.Id, ActorFor(_lawyerA));
        Assert.NotNull(recipientOutsideBranch);
        Assert.Equal(_alertB.Id, recipientOutsideBranch!.Id);

        // المنشئ يرى المستلمين.
        var creator = await _service.GetDetailsAsync(_alertA.Id, ActorFor(_headA));
        Assert.NotNull(creator);
        Assert.True(creator!.CanViewRecipients);
        Assert.Null(creator.IsRead); // ليس مستلمًا

        // الإشراف يرى التفاصيل والمستلمين دائمًا.
        foreach (var oversight in new[] { _manager, _admin })
        {
            var detail = await _service.GetDetailsAsync(_alertA.Id, ActorFor(oversight));
            Assert.NotNull(detail);
            Assert.True(detail!.CanViewRecipients);
            Assert.Null(detail.IsRead);
        }

        // رئيس فرع آخر غير منشئ ولا مستلم ولا مشرف → 404 (null).
        Assert.Null(await _service.GetDetailsAsync(_alertA.Id, ActorFor(_headB)));
        // محامٍ غير مستلم → 404.
        Assert.Null(await _service.GetDetailsAsync(_alertA.Id, ActorFor(_lawyerB)));
        // رئيس شعبة غير مستلم → 404.
        Assert.Null(await _service.GetDetailsAsync(_alertA.Id, ActorFor(_subA)));
        // معرّف غير موجود → null.
        Assert.Null(await _service.GetDetailsAsync(int.MaxValue, ActorFor(_manager)));
    }

    [Fact]
    public async Task Recipients_OnlyCreatorOrOversight_OthersGetNull()
    {
        var creator = await _service.GetRecipientsAsync(_alertA.Id, ActorFor(_headA));
        Assert.NotNull(creator);
        Assert.Single(creator!);
        var lawyerRow = Assert.Single(creator, r => r.UserId == _lawyerA.Id);
        Assert.Equal(_lawyerA.FullName, lawyerRow.FullName);
        Assert.Equal("lawyer", lawyerRow.Role);
        Assert.False(lawyerRow.IsRead);

        Assert.NotNull(await _service.GetRecipientsAsync(_alertA.Id, ActorFor(_manager)));
        Assert.NotNull(await _service.GetRecipientsAsync(_alertA.Id, ActorFor(_admin)));

        // المستلم لا يرى قائمة المستلمين، وغير المنشئ/الإشراف يُترجم إلى 404.
        Assert.Null(await _service.GetRecipientsAsync(_alertA.Id, ActorFor(_lawyerA)));
        Assert.Null(await _service.GetRecipientsAsync(_alertA.Id, ActorFor(_headB)));
        Assert.Null(await _service.GetRecipientsAsync(int.MaxValue, ActorFor(_manager)));
    }

    [Fact]
    public async Task BranchlessHead_IsRejectedOnBranchScopedLists()
    {
        var actor = new HeadAlertActor(_headA.Id, UserRole.Head, null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ListForActorAsync(actor, new HeadAlertListQuery { Page = FullPage() }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ListSentAsync(actor, AlertArchiveFilter.Active, FullPage()));

        // التفاصيل لا تحتاج فرعًا: غير المستلم/المنشئ/الإشراف → null، لا استثناء.
        Assert.Null(await _service.GetDetailsAsync(_alertB.Id, actor));
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task InvalidPaging_IsRejected(int page, int pageSize)
    {
        var request = new AlertPageRequest { Page = page, PageSize = pageSize };
        await Assert.ThrowsAsync<AlertPagingException>(() =>
            _service.ListForActorAsync(
                ActorFor(_manager), new HeadAlertListQuery { Page = request }));
        await Assert.ThrowsAsync<AlertPagingException>(() =>
            _service.ListSentAsync(
                ActorFor(_headA), AlertArchiveFilter.Active, request));
        await Assert.ThrowsAsync<AlertPagingException>(() =>
            _service.ListByDelegationAsync(77, ActorFor(_lawyerA), request));
    }

    [Fact]
    public async Task InvalidPaging_RejectsHugePage()
    {
        // P3-6: فيضان الإزاحة رفض صريح من العقد برسالة عربية نظيفة، لا التفاف
        // `int` يعتمد على حارس المستودع. الأسطح الثلاثة تمرّ بالتحقق نفسه.
        var request = new AlertPageRequest { Page = int.MaxValue, PageSize = AlertPageRequest.MaxPageSize };

        var listEx = await Assert.ThrowsAsync<AlertPagingException>(() =>
            _service.ListForActorAsync(
                ActorFor(_manager), new HeadAlertListQuery { Page = request }));
        Assert.DoesNotContain("Parameter", listEx.Message);

        await Assert.ThrowsAsync<AlertPagingException>(() =>
            _service.ListSentAsync(ActorFor(_headA), AlertArchiveFilter.Active, request));
        await Assert.ThrowsAsync<AlertPagingException>(() =>
            _service.ListByDelegationAsync(77, ActorFor(_lawyerA), request));
    }

    [Fact]
    public async Task Paging_ExtremePageWithSize1_IsAllowed()
    {
        // السقف يرفض الفيضان لا كِبَر رقم الصفحة: الإزاحة 2,147,483,646 ≤ int.MaxValue.
        var page = await _service.ListForActorAsync(
            ActorFor(_manager),
            new HeadAlertListQuery { Page = new() { Page = int.MaxValue, PageSize = 1 } });

        Assert.Empty(page.Items);
        Assert.Equal(int.MaxValue, page.Page);
        Assert.Equal(1, page.PerPage);
    }

    [Theory]
    [InlineData(UserRole.Lawyer)]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.Admin)]
    public async Task Create_NonHeadRoles_AreRejectedWithUnauthorized(UserRole role)
    {
        // الدور قرار الخدمة (D8): المتحكم لم يعد يفحصه، فالرفض هنا هو ما يُترجم 403.
        var actor = new HeadAlertActor(_lawyerA.Id, role, _branchA);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.CreateAsync(
                new CreateHeadAlertRequest("branch", null, null, "رسالة"), actor, "actor"));
    }

    [Fact]
    public async Task Create_BranchlessHead_IsArgumentError_NotAuthorization()
    {
        // D11: الكتابة بلا فرع 400 (عقد POST القائم) لا 403.
        var actor = new HeadAlertActor(_headA.Id, UserRole.Head, null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(
                new CreateHeadAlertRequest("branch", null, null, "رسالة"), actor, "head_a"));
    }

    [Fact]
    public async Task Create_DatabaseRoleMismatch_IsRejected_AsDefenseInDepth()
    {
        // رمز بدور رئيس لحساب صار محاميًا في القاعدة (تعديل مباشر بلا رفع TokenVersion):
        // فحص دور التوكن يمرّ، ودفاع القاعدة يرفض — لا يُصدر محامٍ تنبيهًا باسم رئيس.
        var actor = new HeadAlertActor(_lawyerA.Id, UserRole.Head, _branchA);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(
                new CreateHeadAlertRequest("branch", null, null, "رسالة"), actor, "law_a"));
    }

    [Theory]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.Admin)]
    public async Task MarkRead_OversightRoles_AreRejected(UserRole role)
    {
        var actor = new HeadAlertActor(_manager.Id, role, null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.MarkReadAsync(_alertA.Id, actor));
    }

    [Fact]
    public async Task MarkRead_NonRecipientInRecipientRole_ReturnsFalse()
    {
        // دور يملك استلامًا لكنه ليس مستلم هذا الصف ⇒ false (يُترجم 404)، لا استثناء.
        Assert.False(await _service.MarkReadAsync(_alertOther.Id, ActorFor(_lawyerA)));
    }

    [Theory]
    [InlineData(UserRole.Manager, AlertUnreadScope.User)]
    [InlineData(UserRole.Manager, AlertUnreadScope.Received)]
    [InlineData(UserRole.Admin, AlertUnreadScope.User)]
    [InlineData(UserRole.Admin, AlertUnreadScope.Received)]
    public async Task CountUnread_OversightRoles_AreRejectedInBothScopes(
        UserRole role, AlertUnreadScope scope)
    {
        var actor = new HeadAlertActor(_manager.Id, role, null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CountUnreadAsync(actor, scope));
    }

    [Theory]
    [InlineData(AlertUnreadScope.User)]
    [InlineData(AlertUnreadScope.Received)]
    public async Task CountUnread_BranchlessHead_IsRejectedInBothScopes(AlertUnreadScope scope)
    {
        // D11: رئيس بلا فرع حساب غير صالح للقراءة ⇒ 403 في النطاقين.
        var branchlessHead = new HeadAlertActor(_headA.Id, UserRole.Head, null);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CountUnreadAsync(branchlessHead, scope));
    }

    [Fact]
    public async Task CountUnread_LawyerIsBranchless_ButReceivedScopeIsForHeadsOnly()
    {
        // المحامي بلا قيد فرع أصلًا: تنبيهان غير مقروءين له في الفرعين.
        Assert.Equal(2, await _service.CountUnreadAsync(ActorFor(_lawyerA), AlertUnreadScope.User));

        // «المستلَم» مجموع مستلمي تنبيهات الرئيس — لا معنى له لمحامٍ ⇒ 403.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.CountUnreadAsync(ActorFor(_lawyerA), AlertUnreadScope.Received));
    }

    [Fact]
    public async Task CountUnread_UserScope_EqualsUnreadOnlyListTotal_ForHead()
    {
        // D7 + ح‑19: الثبات الحقيقي هو «العدّاد = TotalCount لقائمة غير المقروء»
        // (يصمد أمام الترقيم)، لا «طول الصفحة» الذي ينجح مصادفةً حين تكون كل الصفوف غير مقروءة.
        var unreadList = await _service.ListForActorAsync(
            ActorFor(_headA), new HeadAlertListQuery { UnreadOnly = true, Page = FullPage() });

        var count = await _service.CountUnreadAsync(ActorFor(_headA), AlertUnreadScope.User);

        Assert.Equal(unreadList.TotalCount, count);
        // تنبيه واحد في فرعه؛ تنبيه فرع حلب (رغم أنه مستلمه) مستبعد.
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CountUnread_UserScope_ExcludesReadRow_WhileListStillContainsIt()
    {
        // ح‑19: العدّاد «غير المقروء» لا «طول القائمة» — القائمة تبقى بطولها بعد القراءة.
        var before = await _service.ListForActorAsync(
            ActorFor(_headA), new HeadAlertListQuery { Page = FullPage() });
        Assert.Single(before.Items);

        Assert.True(await _service.MarkReadAsync(_alertHead.Id, ActorFor(_headA)));

        var after = await _service.ListForActorAsync(
            ActorFor(_headA), new HeadAlertListQuery { Page = FullPage() });
        Assert.Single(after.Items);
        Assert.Equal(0, await _service.CountUnreadAsync(ActorFor(_headA), AlertUnreadScope.User));
    }

    [Fact]
    public async Task CountUnread_HeadMarkingOwnRowRead_DecrementsUserOnly()
    {
        // ح‑21: الفرق العملي بين النطاقين — قراءة الرئيس صفّه تخفض `user`،
        // ولا تُسقط `received` إلا بمقدار صفّه (يبقى صف المحامي غير المقروء).
        var collective = AddAlert(_branchA, _headB.Id, "استلام جماعي", _lawyerA.Id,
            _headA.Id, _lawyerA.Id);

        // received = 1 (`_alertHead`) + 2 (collective) = 3، وuser = 2.
        Assert.Equal(3, await _service.CountUnreadAsync(ActorFor(_headA), AlertUnreadScope.Received));
        Assert.Equal(2, await _service.CountUnreadAsync(ActorFor(_headA), AlertUnreadScope.User));

        Assert.True(await _service.MarkReadAsync(collective.Id, ActorFor(_headA)));

        Assert.Equal(2, await _service.CountUnreadAsync(ActorFor(_headA), AlertUnreadScope.Received));
        Assert.Equal(1, await _service.CountUnreadAsync(ActorFor(_headA), AlertUnreadScope.User));
    }

    [Fact]
    public async Task CountUnread_ReceivedScope_IsAvailableToSubHead_InHisBranch()
    {
        // ح‑21: الشعبة دور مستقل بعقد قراءة — النطاقان متاحان لها داخل فرعها.
        var collective = AddAlert(_branchA, _headB.Id, "استلام شعبة", _lawyerA.Id,
            _subA.Id, _lawyerA.Id);

        var user = await _service.CountUnreadAsync(ActorFor(_subA), AlertUnreadScope.User);
        var received = await _service.CountUnreadAsync(ActorFor(_subA), AlertUnreadScope.Received);

        Assert.Equal(1, user);
        Assert.Equal(2, received);
        // ح‑23: الثابت البنيوي — `received` ⊇ `user` لكل دور مستلم.
        Assert.True(received >= user);
    }

    [Fact]
    public async Task CountUnread_UndefinedScope_IsRejected_FailClosed()
    {
        // ح‑22: قيمة enum غير معروفة (مستدعٍ داخلي) تُرفض 400 ولا تسقط صامتةً إلى `user`.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.CountUnreadAsync(ActorFor(_lawyerA), (AlertUnreadScope)42));
    }

    [Fact]
    public async Task CountUnread_ReceivedScope_CountsAllUnreadRecipientsOfReceivedAlerts()
    {
        // تنبيه جماعي في فرع الرئيس: هو مستلم فيه مع محاميين، والثلاثة لم يقرأوا.
        var collective = AddAlert(_branchA, _headB.Id, "استلام جماعي", _lawyerA.Id,
            _headA.Id, _lawyerA.Id, _lawyerB.Id);

        // D6: كل صفوف غير المقروءة على التنبيهات التي استلمها (3 + 1 في `_alertHead`)
        // — لا صفّه وحده (2)، ولا تنبيهات لا يستلمها (`_alertA` و`_alertOther`).
        Assert.Equal(4, await _service.CountUnreadAsync(ActorFor(_headA), AlertUnreadScope.Received));
        Assert.Equal(2, await _service.CountUnreadAsync(ActorFor(_headA), AlertUnreadScope.User));

        // قراءة محامٍ من التنبيه تخفض «المستلَم» وحده، ولا تمس صف الرئيس.
        Assert.True(await _service.MarkReadAsync(collective.Id, ActorFor(_lawyerA)));
        Assert.Equal(3, await _service.CountUnreadAsync(ActorFor(_headA), AlertUnreadScope.Received));
        Assert.Equal(2, await _service.CountUnreadAsync(ActorFor(_headA), AlertUnreadScope.User));
    }

    [Fact]
    public async Task CountUnread_ArchivedAndOtherBranch_AreExcludedInBothScopes()
    {
        // مؤرشف في فرعه ومستلم فيه: لو حُسب لأضاف 1 إلى `user` و2 إلى `received`.
        var archived = AddAlert(_branchA, _headB.Id, "مؤرشف", _lawyerA.Id,
            _headA.Id, _lawyerA.Id);
        archived.IsArchived = true;
        _db.SaveChanges();

        Assert.Equal(1, await _service.CountUnreadAsync(ActorFor(_headA), AlertUnreadScope.User));
        Assert.Equal(1, await _service.CountUnreadAsync(ActorFor(_headA), AlertUnreadScope.Received));
    }

    private HeadAlert AddAlert(
        int branchId, int createdById, string message, int targetLawyerId, params int[] recipientIds)
    {
        var alert = new HeadAlert
        {
            BranchId = branchId,
            CreatedById = createdById,
            TargetType = HeadAlertTargetType.Lawyer,
            Kind = HeadAlertKindCatalog.ManualToLawyer,
            TargetLawyerId = targetLawyerId,
            Message = message,
            CreatedAt = DateTime.UtcNow,
            LastContentChangedAt = DateTime.UtcNow,
            Recipients = recipientIds
                .Select(id => new HeadAlertRecipient { UserId = id })
                .ToList(),
        };
        _db.HeadAlerts.Add(alert);
        _db.SaveChanges();
        return alert;
    }

    private static AlertPageRequest FullPage() => new() { Page = 1, PageSize = AlertPageRequest.MaxPageSize };

    private static HeadAlertActor ActorFor(User user) => new(user.Id, user.Role, user.BranchId);

    private static User NewUser(string username, string fullName, UserRole role, int? branchId) => new()
    {
        Username = username,
        FullName = fullName,
        Role = role,
        BranchId = branchId,
        IsActive = true,
        PasswordHash = "x",
    };
}
