using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

public class HeadAlertServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IHeadAlertService _service;
    private readonly FakeAuditLogger _audit = new();
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

        var documents = new DocumentRepository(_db);
        var users = new UserRepository(_db);
        var alerts = new HeadAlertRepository(_db);
        var branches = new Repository<Branch>(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        _service = new HeadAlertService(alerts, documents, users, branches, uow, tx, _audit);
    }

    public void Dispose() => _db.Dispose();

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
            new CreateHeadAlertRequest("document", doc.Id, null, "راجع هذا الملف"), _head.Id, _branchId, "head_x");

        Assert.Equal(HeadAlertTargetType.Document.ToString().ToLowerInvariant(), alert.TargetType);
        Assert.Equal(doc.Id, alert.DocumentId);
        Assert.Contains("create_alert", _audit.Actions);

        var lawyerList = await _service.ListForLawyerAsync(_lawyer1.Id);
        var lawyerAlert = Assert.Single(lawyerList);
        Assert.Equal("راجع هذا الملف", lawyerAlert.Message);
        Assert.Equal("أحمد العلي", lawyerAlert.DocumentTitle);
    }

    [Fact]
    public async Task Create_LawyerTargeted_RecipientIsThatLawyerOnly()
    {
        var alert = await _service.CreateAsync(
            new CreateHeadAlertRequest("lawyer", null, _lawyer2.Id, "رسالة خاصة"), _head.Id, _branchId, "head_x");

        Assert.Equal(_lawyer2.Id, alert.TargetLawyerId);

        var list1 = await _service.ListForLawyerAsync(_lawyer1.Id);
        var list2 = await _service.ListForLawyerAsync(_lawyer2.Id);
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
            new CreateHeadAlertRequest("branch", null, null, "تعميم لجميع المحامين"), _head.Id, _branchId, "head_x");

        Assert.Equal(2, alert.RecipientCount);
        Assert.Equal(2, (await _service.ListForLawyerAsync(_lawyer1.Id)).Count
            + (await _service.ListForLawyerAsync(_lawyer2.Id)).Count);
    }

    [Fact]
    public async Task Create_EmptyMessage_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateHeadAlertRequest("branch", null, null, "  "), _head.Id, _branchId, "head_x"));
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
            _service.CreateAsync(new CreateHeadAlertRequest("document", doc.Id, null, "خارج الفرع"), _head.Id, _branchId, "head_x"));

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
            _service.CreateAsync(new CreateHeadAlertRequest("lawyer", null, lawyerOther.Id, "خارج الفرع"), _head.Id, _branchId, "head_x"));

        Assert.Contains("فرعك", ex.Message);
    }

    [Fact]
    public async Task Create_InvalidTargetType_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateHeadAlertRequest("anyone", null, null, "رسالة"), _head.Id, _branchId, "head_x"));
    }

    [Fact]
    public async Task ListForHead_ReturnsBranchAlertsWithCounts()
    {
        var doc = await AddDocumentAsync(_lawyer1);
        await _service.CreateAsync(
            new CreateHeadAlertRequest("document", doc.Id, null, "تنبيه أول"), _head.Id, _branchId, "head_x");
        await _service.CreateAsync(
            new CreateHeadAlertRequest("branch", null, null, "تعميم"), _head.Id, _branchId, "head_x");

        var list = await _service.ListForHeadAsync(_branchId);

        Assert.Equal(2, list.Count);
        Assert.Equal("تعميم", list[0].Message); // الأحدث أولاً
        Assert.Equal(2, list[0].RecipientCount); // محاميان في الفرع
        Assert.Equal(2, list[0].UnreadCount);
        Assert.Equal("تنبيه أول", list[1].Message);
        Assert.Equal(1, list[1].RecipientCount); // المحامي المختص فقط
        Assert.Equal(1, list[1].UnreadCount);
    }

    [Fact]
    public async Task MarkRead_SetsRead_AndDecrementsUnread()
    {
        var alert = await _service.CreateAsync(
            new CreateHeadAlertRequest("lawyer", null, _lawyer1.Id, "خاص"), _head.Id, _branchId, "head_x");

        Assert.Equal(1, await _service.CountUnreadAsync(_lawyer1.Id));

        var ok = await _service.MarkReadAsync(alert.Id, _lawyer1.Id);

        Assert.True(ok);
        Assert.Equal(0, await _service.CountUnreadAsync(_lawyer1.Id));

        var dto = Assert.Single(await _service.ListForLawyerAsync(_lawyer1.Id));
        Assert.True(dto.IsRead);
    }

    [Fact]
    public async Task MarkRead_NotFound_ReturnsFalse()
    {
        var ok = await _service.MarkReadAsync(999, _lawyer1.Id);
        Assert.False(ok);
    }

    [Fact]
    public async Task MarkRead_NonRecipient_ReturnsFalse()
    {
        var alert = await _service.CreateAsync(
            new CreateHeadAlertRequest("lawyer", null, _lawyer1.Id, "خاص"), _head.Id, _branchId, "head_x");

        var ok = await _service.MarkReadAsync(alert.Id, _lawyer2.Id);

        Assert.False(ok);
    }

    [Fact]
    public async Task Create_HeadTargeted_ReachesAllActiveHeads()
    {
        // رئيس فرع موقوف لا يستقبل تنبيهات النظام المرحلية (نطاق «head»).
        _db.Users.Add(new User
        {
            Username = "head_stopped",
            FullName = "رئيس قسم موقوف",
            Role = UserRole.Head,
            BranchId = _branchId,
            IsActive = false,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        await _db.SaveChangesAsync();

        var alert = await _service.CreateAsync(
            new CreateHeadAlertRequest("head", null, null, "بانتظار اعتماد الإنابة — ثمّة إنابة معلّقة", DelegationId: 77),
            _head.Id, _branchId, "head_x");

        Assert.Equal("head", alert.TargetType);
        Assert.Equal(1, alert.RecipientCount); // الرأس المفعل فقط

        var stored = await _db.HeadAlerts.Include(a => a.Recipients).SingleAsync(a => a.Id == alert.Id);
        Assert.Equal(77, stored.DelegationId);
        Assert.Contains(_head.Id, stored.Recipients.Select(r => r.UserId));

        var headList = await _service.ListForLawyerAsync(_head.Id);
        Assert.Contains(headList, a => a.Id == alert.Id);
    }

    [Fact]
    public async Task Create_HeadTargeted_NoActiveHeads_Throws()
    {
        var blocked = _db.Users.Single(u => u.Id == _head.Id);
        blocked.IsActive = false;
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateHeadAlertRequest("head", null, null, "رسالة بلا مستلم", DelegationId: 1),
                _head.Id, _branchId, "head_x"));
    }

    [Fact]
    public async Task UpdateDelegationAlert_UpdatesLatestMessageKeepingRecipients()
    {
        var alert = await _service.CreateAsync(
            new CreateHeadAlertRequest("head", null, null, "بانتظار اعتماد الإنابة — رسالة أولى", DelegationId: 5),
            _head.Id, _branchId, "head_x");
        await _service.MarkReadAsync(alert.Id, _head.Id);

        var updated = await _service.UpdateDelegationAlertAsync(5, "بانتظار اعتماد الإنابة — رسالة معدّلة");

        Assert.NotNull(updated);
        Assert.Equal("بانتظار اعتماد الإنابة — رسالة معدّلة", updated!.Message);

        // المستلمون وعلامات القراءة صامدة بعد التحديث.
        var stored = await _db.HeadAlerts.Include(a => a.Recipients).SingleAsync(a => a.Id == alert.Id);
        var recipient = Assert.Single(stored.Recipients);
        Assert.True(recipient.IsRead);
    }

    [Fact]
    public async Task UpdateDelegationAlert_WithoutExistingAlert_ReturnsNull()
    {
        var updated = await _service.UpdateDelegationAlertAsync(999, "رسالة");
        Assert.Null(updated);
    }

    [Fact]
    public async Task DeleteByDelegation_RemovesOnlyThatDelegationsAlerts()
    {
        var first = await _service.CreateAsync(
            new CreateHeadAlertRequest("head", null, null, "إنابة أولى", DelegationId: 1), _head.Id, _branchId, "head_x");
        var second = await _service.CreateAsync(
            new CreateHeadAlertRequest("head", null, null, "إنابة ثانية", DelegationId: 2), _head.Id, _branchId, "head_x");

        Assert.True(await _service.DeleteByDelegationAsync(1));
        Assert.False(await _service.DeleteByDelegationAsync(1)); // لا شيء متبقٍ للإنابة الأولى

        Assert.Null(await _db.HeadAlerts.FindAsync(first.Id));
        Assert.NotNull(await _db.HeadAlerts.FindAsync(second.Id));
    }
}
