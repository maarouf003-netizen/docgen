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
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.ListForActorAsync(
                ActorFor(_manager), new HeadAlertListQuery { Page = request }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.ListSentAsync(
                ActorFor(_headA), AlertArchiveFilter.Active, request));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _service.ListByDelegationAsync(77, ActorFor(_lawyerA), request));
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
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.Admin)]
    public async Task CountUnread_OversightRoles_AreRejected(UserRole role)
    {
        var actor = new HeadAlertActor(_manager.Id, role, null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CountUnreadAsync(actor));
    }

    [Fact]
    public async Task CountUnread_BranchlessHead_IsRejected_LawyerCountsOwnRows()
    {
        // D11: رئيس بلا فرع حساب غير صالح للقراءة ⇒ 403 (كان 200 قبل التوحيد).
        var branchlessHead = new HeadAlertActor(_headA.Id, UserRole.Head, null);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CountUnreadAsync(branchlessHead));

        // المحامي بلا قيد فرع أصلًا: تنبيهان غير مقروءين له في الفرعين.
        Assert.Equal(2, await _service.CountUnreadAsync(ActorFor(_lawyerA)));
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
