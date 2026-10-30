using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

public class HeadAlertServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IHeadAlertService _service;
    private readonly FakeAuditLogger _audit = new();
    private readonly TransactionRunner _tx;
    private readonly int _branchId;
    private readonly User _head;
    private readonly User _lawyer1;
    private readonly User _lawyer2;

    public HeadAlertServiceTests()
    {
        _db = TestDb.Create();
        var branch = new Branch { Name = "دمشق", Code = "DAM" };
        _db.Branches.Add(branch);
        _db.SaveChanges();
        _branchId = branch.Id;

        _head = new User
        {
            Username = "head_x",
            FullName = "رئيس القسم",
            Role = UserRole.Head,
            BranchId = _branchId,
            PasswordHash = new PasswordHasher().Hash("123456"),
        };
        _lawyer1 = new User
        {
            Username = "law1",
            FullName = "محامي دمشق 1",
            Role = UserRole.Lawyer,
            BranchId = _branchId,
            PasswordHash = new PasswordHasher().Hash("123456"),
        };
        _lawyer2 = new User
        {
            Username = "law2",
            FullName = "محامي دمشق 2",
            Role = UserRole.Lawyer,
            BranchId = _branchId,
            PasswordHash = new PasswordHasher().Hash("123456"),
        };
        _db.Users.AddRange(_head, _lawyer1, _lawyer2);
        _db.SaveChanges();

        // الإنابة بلا مفتاح أجنبي في المخطط؛ فحص المراجع يحتاج صفًا فعليًا (77).
        HeadAlertReferences.SeedDelegations(_db, _branchId, _head.Id, 77);

        var documents = new DocumentRepository(_db);
        var users = new UserRepository(_db);
        var alerts = new HeadAlertRepository(_db);
        var branches = new Repository<Branch>(_db);
        var uow = new UnitOfWork(_db);
        _tx = new TransactionRunner(_db);
        _service = new HeadAlertService(alerts, documents, users, branches,
            new DelegationRepository(_db), uow, _tx, _audit);
    }

    public void Dispose() => _db.Dispose();

    private static AlertPageRequest FullPage() => new() { Page = 1, PageSize = AlertPageRequest.MaxPageSize };

    private static HeadAlertActor ActorFor(User user) => new(user.Id, user.Role, user.BranchId);

    private async Task<List<HeadAlertSummaryDto>> ReceivedAsync(User user) =>
        (await _service.ListForActorAsync(
            ActorFor(user),
            new HeadAlertListQuery { Page = FullPage() })).Items.ToList();

    private async Task<List<HeadAlertSummaryDto>> SentAsync(User user) =>
        (await _service.ListSentAsync(
            ActorFor(user), AlertArchiveFilter.Active, FullPage())).Items.ToList();

    private async Task<int> CreateSystemHeadAlertAsync(
        string message,
        HeadAlertAudience audience,
        int? recipientUserId = null,
        int? delegationId = null,
        int? createdById = null)
    {
        var id = 0;
        await _tx.RunAsync(async token =>
        {
            await _service.CreateSystemAsync(
                new CreateSystemHeadAlertRequest(
                    HeadAlertKindCatalog.DelegationPendingApproval,
                    message,
                    audience,
                    _branchId,
                    createdById ?? _head.Id,
                    DelegationId: delegationId,
                    RecipientUserId: recipientUserId),
                DateTime.UtcNow,
                null,
                null,
                token);
            id = await _db.HeadAlerts
                .OrderByDescending(a => a.Id)
                .Select(a => a.Id)
                .FirstAsync(token);
        });
        return id;
    }

    private async Task<Document> AddDocumentAsync(User owner, int? branchId = null)
    {
        var doc = new Document
        {
            BranchId = branchId ?? _branchId,
            CreatedById = owner.Id,
            IsDraft = false,
            BorrowerName = "أحمد",
            BorrowerFamily = "العلي",
            AmountNumeric = 0,
            ExecStatus = string.Empty,
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        return doc;
    }

    [Fact]
    public async Task Create_DocumentTargeted_RecipientIsDocumentLawyer()
    {
        var doc = await AddDocumentAsync(_lawyer1);

        var alert = await _service.CreateAsync(
            new CreateHeadAlertRequest("document", doc.Id, null, "راجع هذا الملف"), ActorFor(_head), "head_x");

        Assert.Equal(HeadAlertTargetType.Document.ToString().ToLowerInvariant(), alert.TargetType);
        Assert.Equal(doc.Id, alert.DocumentId);
        Assert.Contains("create_alert", _audit.Actions);

        var lawyerList = await ReceivedAsync(_lawyer1);
        var lawyerAlert = Assert.Single(lawyerList);
        // القائمة تحمل معاينة فقط؛ النص الكامل للتفاصيل وحدها (§9.2). النص هنا أقصر من حدّ المعاينة.
        Assert.Equal(
            HeadAlertMessages.ManualMessage(UserRole.Head, "راجع هذا الملف"),
            lawyerAlert.MessagePreview);
        Assert.Equal("أحمد العلي", lawyerAlert.DocumentTitle);
    }

    [Fact]
    public async Task Create_LawyerTargeted_RecipientIsThatLawyerOnly()
    {
        var alert = await _service.CreateAsync(
            new CreateHeadAlertRequest("lawyer", null, _lawyer2.Id, "رسالة خاصة"), ActorFor(_head), "head_x");

        Assert.Equal(_lawyer2.Id, alert.TargetLawyerId);

        var list1 = await ReceivedAsync(_lawyer1);
        var list2 = await ReceivedAsync(_lawyer2);
        Assert.Empty(list1);
        Assert.Single(list2);
    }

    [Fact]
    public async Task Create_BranchBroadcast_ReachesAllActiveLawyers()
    {
        _db.Users.Add(new User
        {
            Username = "inactive_law",
            FullName = "محامي موقوف",
            Role = UserRole.Lawyer,
            BranchId = _branchId,
            IsActive = false,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        await _db.SaveChangesAsync();

        var alert = await _service.CreateAsync(
            new CreateHeadAlertRequest("branch", null, null, "تعميم لجميع المحامين"), ActorFor(_head), "head_x");

        Assert.Equal(2, alert.RecipientCount);
        Assert.Equal(2, (await ReceivedAsync(_lawyer1)).Count
            + (await ReceivedAsync(_lawyer2)).Count);
    }

    [Fact]
    public async Task Create_EmptyMessage_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateHeadAlertRequest("branch", null, null, "  "), ActorFor(_head), "head_x"));
    }

    [Fact]
    public async Task Create_DocumentOutsideBranch_Throws()
    {
        var other = new Branch { Name = "حلب", Code = "ALP" };
        _db.Branches.Add(other);
        _db.SaveChanges();
        var lawyerOther = new User
        {
            Username = "law_alp",
            FullName = "محامي حلب",
            Role = UserRole.Lawyer,
            BranchId = other.Id,
            PasswordHash = new PasswordHasher().Hash("123456"),
        };
        _db.Users.Add(lawyerOther);
        await _db.SaveChangesAsync();
        var doc = await AddDocumentAsync(lawyerOther, branchId: other.Id);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateHeadAlertRequest("document", doc.Id, null, "خارج الفرع"), ActorFor(_head), "head_x"));

        Assert.Contains("فرعك", ex.Message);
    }

    [Fact]
    public async Task Create_LawyerOutsideBranch_Throws()
    {
        var other = new Branch { Name = "حلب", Code = "ALP" };
        _db.Branches.Add(other);
        _db.SaveChanges();
        var lawyerOther = new User
        {
            Username = "law_alp2",
            FullName = "محامي حلب",
            Role = UserRole.Lawyer,
            BranchId = other.Id,
            PasswordHash = new PasswordHasher().Hash("123456"),
        };
        _db.Users.Add(lawyerOther);
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateHeadAlertRequest("lawyer", null, lawyerOther.Id, "خارج الفرع"), ActorFor(_head), "head_x"));

        Assert.Contains("فرعك", ex.Message);
    }

    [Fact]
    public async Task Create_InvalidTargetType_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateHeadAlertRequest("anyone", null, null, "رسالة"), ActorFor(_head), "head_x"));
    }

    [Fact]
    public async Task ListForHead_ReturnsOnlyOwnRecipientAlerts()
    {
        var doc = await AddDocumentAsync(_lawyer1);
        await _service.CreateAsync(
            new CreateHeadAlertRequest("document", doc.Id, null, "تنبيه أول"), ActorFor(_head), "head_x");
        await _service.CreateAsync(
            new CreateHeadAlertRequest("branch", null, null, "تعميم"), ActorFor(_head), "head_x");
        var ownId = await CreateSystemHeadAlertAsync(
            "تنبيه نظامي للرئيس", HeadAlertAudience.SingleHead, recipientUserId: _head.Id);

        // قراءة بالمستلم (§8): الرئيس يرى تنبيهه فقط — لا تنبيهات المحامين ولا التعميم.
        var list = await ReceivedAsync(_head);

        var single = Assert.Single(list);
        Assert.Equal(ownId, single.Id);
        Assert.False(single.IsRead);
        Assert.Equal(1, single.RecipientCount);
        Assert.Equal(1, single.UnreadCount);

        // تعليم القراءة يعمل للمستلم الرئيس ويُرى أثره.
        Assert.True(await _service.MarkReadAsync(ownId, ActorFor(_head)));
        list = await ReceivedAsync(_head);
        Assert.True(Assert.Single(list).IsRead);

        // محامٍ غير مستلم لا يرى تنبيه الرئيس.
        Assert.DoesNotContain(
            await ReceivedAsync(_lawyer2),
            a => a.Id == ownId);
    }

    [Fact]
    public async Task MarkRead_SetsRead_AndDecrementsUnread()
    {
        var alert = await _service.CreateAsync(
            new CreateHeadAlertRequest("lawyer", null, _lawyer1.Id, "خاص"), ActorFor(_head), "head_x");

        Assert.Equal(1, await _service.CountUnreadAsync(ActorFor(_lawyer1)));

        var ok = await _service.MarkReadAsync(alert.Id, ActorFor(_lawyer1));

        Assert.True(ok);
        Assert.Equal(0, await _service.CountUnreadAsync(ActorFor(_lawyer1)));

        var dto = Assert.Single(await ReceivedAsync(_lawyer1));
        Assert.True(dto.IsRead);
    }

    [Fact]
    public async Task MarkRead_NotFound_ReturnsFalse()
    {
        var ok = await _service.MarkReadAsync(999, ActorFor(_lawyer1));
        Assert.False(ok);
    }

    [Fact]
    public async Task MarkRead_NonRecipient_ReturnsFalse()
    {
        var alert = await _service.CreateAsync(
            new CreateHeadAlertRequest("lawyer", null, _lawyer1.Id, "خاص"), ActorFor(_head), "head_x");

        var ok = await _service.MarkReadAsync(alert.Id, ActorFor(_lawyer2));

        Assert.False(ok);
    }

    [Fact]
    public async Task Create_ManualHeadTarget_Rejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(
                new CreateHeadAlertRequest("head", null, null, "manual head"),
                ActorFor(_head), "head_x"));
    }

    [Fact]
    public async Task CreateSystem_SingleHead_MissingRecipient_ThrowsNoRecipients()
    {
        // لا رئيس نشط في الفرع: حالة عمل بلا مستلمين — تُترجم لدى المشغّل إلى
        // skipped/no_recipients، لا إلى إعادة ثم failed.
        await Assert.ThrowsAsync<HeadAlertNoRecipientsException>(() =>
            CreateSystemHeadAlertAsync("system head", HeadAlertAudience.SingleHead));
    }

    [Fact]
    public async Task CreateSystem_HeadTargeted_ReachesExplicitActiveRecipient()
    {
        var sub = new User
        {
            Username = "sub_y",
            FullName = "sub head",
            Role = UserRole.SubHead,
            BranchId = _branchId,
            IsActive = true,
            PasswordHash = new PasswordHasher().Hash("123456"),
        };
        _db.Users.Add(sub);
        var blocked = _db.Users.Single(u => u.Id == _head.Id);
        blocked.IsActive = false;
        await _db.SaveChangesAsync();

        var alertId = await CreateSystemHeadAlertAsync(
            "system head",
            HeadAlertAudience.SingleHead,
            recipientUserId: sub.Id,
            delegationId: 77,
            createdById: sub.Id);

        var stored = await _db.HeadAlerts.Include(a => a.Recipients).SingleAsync(a => a.Id == alertId);
        Assert.Equal(HeadAlertTargetType.Head, stored.TargetType);
        Assert.Equal(sub.Id, Assert.Single(stored.Recipients).UserId);
        Assert.Equal(77, stored.DelegationId);
    }
    [Fact]
    public async Task DetailContract_CarriesAllFields_AndCanViewRecipientsIsServerComputed()
    {
        var doc = await AddDocumentAsync(_lawyer1);

        var created = await _service.CreateAsync(
            new CreateHeadAlertRequest("document", doc.Id, null, "نص كامل للتفاصيل"), ActorFor(_head), "head_x");

        Assert.Equal(HeadAlertKindCatalog.ManualToDocument, created.Kind);
        // D3: ملخص الإنشاء يطابق صف القائمة في الحقول المشتقة من العلاقات — لا «مستند محذوف»
        // ولا اسم منشئ فارغ لكيان أُنشئ للتو.
        Assert.Equal("أحمد العلي", created.DocumentTitle);
        Assert.Equal(_head.FullName, created.CreatedByName);

        // عقد التفاصيل (§9.2): الحقول السبعة المضافة كلها موجودة، والنص الكامل هنا وحده.
        var fullMessage = HeadAlertMessages.ManualMessage(UserRole.Head, "نص كامل للتفاصيل");
        var detail = await _service.GetDetailsAsync(created.Id, ActorFor(_head));
        Assert.NotNull(detail);
        Assert.Equal(fullMessage, detail!.Message);
        Assert.Equal(created.Kind, detail.Kind);
        Assert.Equal(created.CreatedAt, detail.LastContentChangedAt);
        Assert.False(detail.IsArchived);
        Assert.Null(detail.ArchivedAt);
        Assert.Null(detail.PublicEntityId);
        Assert.Null(detail.ExecutionCircuitId);
        // المنشئ يرى المستلمين دائمًا، وهو ليس مستلمًا فلا حالة قراءة له.
        Assert.True(detail.CanViewRecipients);
        Assert.Null(detail.IsRead);
        Assert.Equal(1, detail.RecipientCount);

        // المستلم غير المنشئ: يرى التفاصيل لكنه لا يرى المستلمين، وحالة القراءة تخصّه.
        var recipientDetail = await _service.GetDetailsAsync(created.Id, ActorFor(_lawyer1));
        Assert.NotNull(recipientDetail);
        Assert.False(recipientDetail!.CanViewRecipients);
        Assert.False(recipientDetail.IsRead);
        Assert.Equal(created.Kind, recipientDetail.Kind);

        // وفي القائمة: معاينة بلا عدّادات مستلمين (قاعدة المحامي — §9.2).
        var recipientView = Assert.Single(await ReceivedAsync(_lawyer1));
        Assert.False(recipientView.IsRead);
        Assert.Null(recipientView.RecipientCount);
        Assert.Equal(created.Kind, recipientView.Kind);
    }

    [Fact]
    public async Task Detail_CreatorWithThreeRecipients_GetsCounters_AndNullIsRead()
    {
        var lawyer3 = new User
        {
            Username = "law3",
            FullName = "محامي دمشق 3",
            Role = UserRole.Lawyer,
            BranchId = _branchId,
            PasswordHash = new PasswordHasher().Hash("123456"),
        };
        _db.Users.Add(lawyer3);
        await _db.SaveChangesAsync();

        var created = await _service.CreateAsync(
            new CreateHeadAlertRequest("branch", null, null, "تعميم"), ActorFor(_head), "head_x");

        // المنشئ ليس مستلمًا: IsRead فارغة، والعدّادان محتفظ بهما في التفاصيل (D5).
        var detail = await _service.GetDetailsAsync(created.Id, ActorFor(_head));
        Assert.NotNull(detail);
        Assert.True(detail!.CanViewRecipients);
        Assert.Null(detail.IsRead);
        Assert.Equal(3, detail.RecipientCount);
        Assert.Equal(3, detail.UnreadCount);

        // مستلم غير الفاعل: حالته تخصّه (غير مقروء) ولا يرى المستلمين.
        var recipientDetail = await _service.GetDetailsAsync(created.Id, ActorFor(lawyer3));
        Assert.NotNull(recipientDetail);
        Assert.False(recipientDetail!.IsRead);
        Assert.False(recipientDetail.CanViewRecipients);
        Assert.Equal(3, recipientDetail.RecipientCount);
    }

    [Fact]
    public async Task MarkRead_UpdatesOnlyActorRow_AndDetailReflectsIt()
    {
        var created = await _service.CreateAsync(
            new CreateHeadAlertRequest("branch", null, null, "تعميم"), ActorFor(_head), "head_x");

        Assert.True(await _service.MarkReadAsync(created.Id, ActorFor(_lawyer1)));

        // قراءة حالة القاعدة فعليًا (لا الكيانات المتتبَّعة من الإنشاء): التعليم صار
        // تحديثًا ذرّيًا في المستودع لا يمرّ بمتتبِّع التغييرات.
        var rows = await _db.HeadAlertRecipients
            .AsNoTracking()
            .Where(r => r.HeadAlertId == created.Id)
            .ToListAsync();
        var mine = Assert.Single(rows, r => r.UserId == _lawyer1.Id);
        Assert.True(mine.IsRead);
        Assert.NotNull(mine.ReadAt);
        Assert.All(rows.Where(r => r.UserId != _lawyer1.Id), r =>
        {
            Assert.False(r.IsRead);
            Assert.Null(r.ReadAt);
        });

        // إعادة التعليم idempotent: لا تُغيّر زمن القراءة المحفوظ.
        var readAt = mine.ReadAt;
        Assert.True(await _service.MarkReadAsync(created.Id, ActorFor(_lawyer1)));
        _db.ChangeTracker.Clear();
        Assert.Equal(readAt, (await _db.HeadAlertRecipients.SingleAsync(r => r.Id == mine.Id)).ReadAt);

        // التفاصيل تعكس حالة القارئ والعدّاد المحتفظ به (D5) بدقة.
        var detail = await _service.GetDetailsAsync(created.Id, ActorFor(_lawyer1));
        Assert.NotNull(detail);
        Assert.True(detail!.IsRead);
        Assert.Equal(2, detail.RecipientCount);
        Assert.Equal(1, detail.UnreadCount);
    }

    [Fact]
    public async Task GetDetails_MapsArchivedAndReferenceFields_ForOversight()
    {
        var createdAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var changedAt = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        var archivedAt = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var alert = new HeadAlert
        {
            BranchId = _branchId,
            CreatedById = _head.Id,
            TargetType = HeadAlertTargetType.Lawyer,
            Kind = HeadAlertKindCatalog.ManualToLawyer,
            TargetLawyerId = _lawyer1.Id,
            Message = "نص مؤرشف",
            CreatedAt = createdAt,
            LastContentChangedAt = changedAt,
            IsArchived = true,
            ArchivedAt = archivedAt,
            DelegationId = 77,
        };
        _db.HeadAlerts.Add(alert);
        await _db.SaveChangesAsync();

        // مدير غير منشئ وغير مستلم: يصل عبر الإشراف (D2).
        var dto = await _service.GetDetailsAsync(alert.Id, new HeadAlertActor(int.MaxValue, UserRole.Manager, null));

        Assert.NotNull(dto);
        Assert.Equal(HeadAlertKindCatalog.ManualToLawyer, dto!.Kind);
        Assert.Equal("نص مؤرشف", dto.Message);
        Assert.Equal(createdAt, dto.CreatedAt);
        Assert.Equal(changedAt, dto.LastContentChangedAt);
        Assert.True(dto.IsArchived);
        Assert.Equal(archivedAt, dto.ArchivedAt);
        Assert.Null(dto.PublicEntityId);
        Assert.Equal(77, dto.DelegationId);
        // الإشراف يرى المستلمين دائمًا، وحالة القراءة فارغة لأنه ليس مستلمًا.
        Assert.True(dto.CanViewRecipients);
        Assert.Null(dto.IsRead);
    }

    [Fact]
    public async Task Sent_ListsOwnIssued_AndReceivedExcludesThem()
    {
        var sent = await _service.CreateAsync(
            new CreateHeadAlertRequest("lawyer", null, _lawyer2.Id, "رسالة خاصة"), ActorFor(_head), "head_x");

        // الصادر: يراه مُصدِره بفرعه مع عدّادات المستلمين...
        var sentList = await SentAsync(_head);
        var item = Assert.Single(sentList);
        Assert.Equal(sent.Id, item.Id);
        Assert.Equal(1, item.RecipientCount);

        // ...ولا يظهر في مستلَماته (قراءة المستلم لا تشمل المُرسِل).
        var received = await ReceivedAsync(_head);
        Assert.DoesNotContain(received, a => a.Id == sent.Id);

        // وفرع آخر لا يراه.
        var otherBranch = new Branch { Name = "حلب", Code = "ALP" };
        _db.Branches.Add(otherBranch);
        await _db.SaveChangesAsync();
        var otherBranchSent = await _service.ListSentAsync(
            new HeadAlertActor(_head.Id, _head.Role, otherBranch.Id),
            AlertArchiveFilter.Active,
            FullPage());
        Assert.Empty(otherBranchSent.Items);
    }

    [Fact]
    public async Task Sent_SubHead_SeesOwnOnly()
    {
        var sub = new User
        {
            Username = "sub_x",
            FullName = "رئيس الشعبة",
            Role = UserRole.SubHead,
            BranchId = _branchId,
            PasswordHash = "x",
        };
        _db.Users.Add(sub);
        await _db.SaveChangesAsync();

        await _service.CreateAsync(
            new CreateHeadAlertRequest("lawyer", null, _lawyer1.Id, "من الشعبة"), ActorFor(sub), "sub_x");
        await _service.CreateAsync(
            new CreateHeadAlertRequest("lawyer", null, _lawyer2.Id, "من القسم"), ActorFor(_head), "head_x");

        var subSent = await SentAsync(sub);
        Assert.Equal(
            HeadAlertMessages.ManualMessage(UserRole.SubHead, "من الشعبة"),
            Assert.Single(subSent).MessagePreview);
        var headSent = await SentAsync(_head);
        Assert.Equal(
            HeadAlertMessages.ManualMessage(UserRole.Head, "من القسم"),
            Assert.Single(headSent).MessagePreview);
    }
}
