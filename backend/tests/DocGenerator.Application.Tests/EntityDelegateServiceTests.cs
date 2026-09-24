using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>اختبارات إدارة حسابات مندوبي الجهات وربط نطاقهم (د11 — المرحلة 3).</summary>
public class EntityDelegateServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IEntityDelegateService _service;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _groupId;
    private readonly int _entryId;

    public EntityDelegateServiceTests()
    {
        _db = TestDb.Create();

        // مستخدم مرجعي (Id=1) لملكية إنشاء القيود قبل إدراجها.
        _db.Users.Add(new User { Username = "creator", FullName = "منشئ", Role = UserRole.Admin, PasswordHash = "x" });

        var group = new PublicEntityGroup { CanonicalName = "وزارة التعليم", EntityType = PublicEntityTypeCatalog.Ministry };
        group.Entries.Add(new PublicEntity { Governorate = "دمشق", BranchName = "الفرع الرئيسي", Status = EntityStatusCatalog.Final, CreatedById = 1 });
        _db.PublicEntityGroups.Add(group);
        _db.SaveChanges();
        _groupId = group.Id;
        _entryId = group.Entries.First().Id;

        _service = new EntityDelegateService(
            new UserRepository(_db),
            new PublicEntityRepository(_db),
            new PasswordHasher(),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit);
    }

    public void Dispose() => _db.Dispose();

    /// <summary>قيد إضافي بحالة محددة لاختبارات أهلية الربط (معلق/مراجعة/موقوف).</summary>
    private async Task<int> AddEntryAsync(string branch, string? status = null, bool needsReview = false, bool isActive = true)
    {
        var entry = new PublicEntity
        {
            GroupId = _groupId,
            Governorate = "دمشق",
            BranchName = branch,
            Status = status ?? EntityStatusCatalog.Final,
            NeedsReview = needsReview,
            IsActive = isActive,
            CreatedById = 1,
        };
        _db.PublicEntities.Add(entry);
        await _db.SaveChangesAsync();
        return entry.Id;
    }

    [Fact]
    public async Task Create_HashesPassword_BindsScope_AndAudits()
    {
        var dto = await _service.CreateAsync(
            new CreateDelegateRequest("delegate.one", "مندوب الوزارة", "secret6", _groupId, null), "المدير");

        Assert.True(dto.IsActive);
        Assert.Equal(_groupId, dto.PortalGroupId);
        Assert.Equal("وزارة التعليم", dto.PortalGroupName);

        var stored = await _db.Users.AsNoTracking().SingleAsync(u => u.Username == "delegate.one");
        Assert.Equal(UserRole.EntityManager, stored.Role);
        Assert.Null(stored.BranchId);
        Assert.NotEqual("secret6", stored.PasswordHash);
        Assert.Contains("create_delegate", _audit.Actions);
    }

    [Fact]
    public async Task Create_RequiresExactlyOneScope()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateDelegateRequest("d1", "مندوب", "secret6", null, null), "مدير"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateDelegateRequest("d1", "مندوب", "secret6", _groupId, _entryId), "مدير"));

        Assert.Empty(_audit.Actions);
    }

    [Fact]
    public async Task Create_DuplicateUsername_FailsFriendly()
    {
        await _service.CreateAsync(new CreateDelegateRequest("delegate.dup", "أول", "secret6", _groupId, null), "مدير");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateDelegateRequest("DELEGATE.DUP", "ثانٍ", "secret6", _groupId, null), "مدير"));

        // التطبيع يوحّد حالة الأحرف فيكشف التكرار.
        Assert.Contains("بنفس اسم الدخول", ex.Message);
        Assert.Single(await _service.ListAsync());
    }

    [Fact]
    public async Task Create_UnknownEntry_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateDelegateRequest("delegate.x", "مندوب", "secret6", null, 999), "مدير"));
    }

    [Fact]
    public async Task Create_PendingEntry_Rejected()
    {
        var pendingId = await AddEntryAsync("فرع معلق", status: EntityStatusCatalog.Pending);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateDelegateRequest("delegate.pend", "مندوب", "secret6", null, pendingId), "مدير"));

        Assert.Contains("بانتظار الاعتماد", ex.Message);
        Assert.Empty(await _service.ListAsync());
        Assert.Empty(_audit.Actions);
    }

    [Fact]
    public async Task Create_NeedsReviewEntry_Rejected()
    {
        var reviewId = await AddEntryAsync("فرع مراجعة", needsReview: true);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateDelegateRequest("delegate.rev", "مندوب", "secret6", null, reviewId), "مدير"));

        Assert.Contains("بانتظار المراجعة", ex.Message);
        Assert.Empty(await _service.ListAsync());
    }

    [Fact]
    public async Task Create_InactiveEntry_Rejected()
    {
        var offId = await AddEntryAsync("فرع موقوف", isActive: false);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(new CreateDelegateRequest("delegate.stop", "مندوب", "secret6", null, offId), "مدير"));

        Assert.Contains("غير نشط", ex.Message);
        Assert.Empty(await _service.ListAsync());
    }

    [Fact]
    public async Task Create_EntryScope_WithUnloadedGroup_ResolvesLabel()
    {
        // يحاكي طلب الإنتاج: القيد مُتتبَّع بلا Include(Group) كما يحمّله
        // ResolveScopeAsync عبر GetEntryAsync، فيملأ EF fix-up navigation
        // user.PortalEntry بكيان مجموعته null — وكان يفجر NullReferenceException
        // في BuildDtoWithScopeAsync قبل الإصلاح (سطر entryLabel).
        _db.ChangeTracker.Clear();

        var dto = await _service.CreateAsync(
            new CreateDelegateRequest("delegate.entry", "مندوب القيد", "secret6", null, _entryId), "مدير");

        Assert.Null(dto.PortalGroupId);
        Assert.Equal(_entryId, dto.PortalEntryId);
        Assert.Equal("وزارة التعليم / الفرع الرئيسي", dto.PortalEntryLabel);
    }

    [Fact]
    public async Task Update_ToEntryScope_AfterDetach_ResolvesLabel()
    {
        var created = await _service.CreateAsync(new CreateDelegateRequest("delegate.move", "مندوب", "old1234", _groupId, null), "مدير");
        _db.ChangeTracker.Clear();

        var updated = await _service.UpdateAsync(created.Id,
            new UpdateDelegateRequest(null, null, null, PortalGroupId: null, PortalEntryId: _entryId),
            "المدير");

        Assert.NotNull(updated);
        Assert.Null(updated.PortalGroupId);
        Assert.Equal(_entryId, updated.PortalEntryId);
        Assert.Equal("وزارة التعليم / الفرع الرئيسي", updated.PortalEntryLabel);
    }

    [Fact]
    public async Task Update_ToIneligibleEntry_Rejected()
    {
        var created = await _service.CreateAsync(new CreateDelegateRequest("delegate.keep", "مندوب", "old1234", _groupId, null), "مدير");
        var pendingId = await AddEntryAsync("فرع معلق", status: EntityStatusCatalog.Pending);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateAsync(created.Id,
                new UpdateDelegateRequest(null, null, null, PortalGroupId: null, PortalEntryId: pendingId),
                "المدير"));

        Assert.Contains("بانتظار الاعتماد", ex.Message);

        // الرفض قبل أي تعديل: النطاق القديم محفوظ.
        var dto = Assert.Single(await _service.ListAsync());
        Assert.Equal(_groupId, dto.PortalGroupId);
        Assert.Null(dto.PortalEntryId);
    }

    [Fact]
    public async Task Update_RebindsScope_ResetsPassword_AndBumpsTokenVersion()
    {
        var created = await _service.CreateAsync(new CreateDelegateRequest("delegate.re", "مندوب", "old1234", _groupId, null), "مدير");
        var hashBefore = (await _db.Users.AsNoTracking().SingleAsync(u => u.Id == created.Id)).PasswordHash;

        var updated = await _service.UpdateAsync(created.Id,
            new UpdateDelegateRequest("الاسم الجديد", IsActive: false, NewPassword: "new9876", PortalGroupId: null, PortalEntryId: _entryId),
            "المدير");

        Assert.NotNull(updated);
        Assert.Equal("الاسم الجديد", updated.FullName);
        Assert.False(updated.IsActive);
        Assert.Null(updated.PortalGroupId);
        Assert.Equal(_entryId, updated.PortalEntryId);

        var stored = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == created.Id);
        Assert.NotEqual(hashBefore, stored.PasswordHash);
        Assert.True(stored.TokenVersion > 0);
        Assert.Contains("update_delegate", _audit.Actions);
    }

    [Fact]
    public async Task Update_NonDelegate_ReturnsNull()
    {
        _db.Users.Add(new User { Username = "plain", FullName = "محامي", Role = UserRole.Lawyer, PasswordHash = "x" });
        await _db.SaveChangesAsync();
        var lawyerId = _db.Users.Single(u => u.Username == "plain").Id;

        Assert.Null(await _service.UpdateAsync(lawyerId,
            new UpdateDelegateRequest(null, false, null, _groupId, null), "مدير"));
    }

    [Fact]
    public async Task Update_ShortPassword_Rejected()
    {
        var created = await _service.CreateAsync(new CreateDelegateRequest("delegate.pw", "مندوب", "old1234", _groupId, null), "مدير");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateAsync(created.Id,
                new UpdateDelegateRequest(null, null, "123", _groupId, null), "مدير"));

        Assert.Contains("6 أحرف", ex.Message);
    }

    [Fact]
    public async Task Deactivate_WithoutPasswordChange_BumpsTokenVersion_ToRevokeSessions()
    {
        var created = await _service.CreateAsync(new CreateDelegateRequest("delegate.off", "مندوب", "old1234", _groupId, null), "مدير");
        var before = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == created.Id);

        await _service.UpdateAsync(created.Id,
            new UpdateDelegateRequest(null, IsActive: false, null, _groupId, null), "المدير");

        var after = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == created.Id);
        Assert.False(after.IsActive);
        Assert.True(after.TokenVersion > before.TokenVersion);
    }
}
