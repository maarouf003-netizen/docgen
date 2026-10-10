using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات شريحة المنتدى (مصفوفة التفويض §2-أ سطرًا بسطر + الترقيم بمفتاح +
/// الاحتفاظ + الهوية + الحدود) — قاعدة SQLite ذاكرة معزولة لكل اختبار.
/// </summary>
public class ForumServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly FakeTimeProvider _clock;
    private readonly IForumService _service;
    private readonly int _branchId;
    private readonly int _otherBranchId;
    private readonly int _sectionId;
    private readonly User _admin;
    private readonly User _manager;
    private readonly User _head;
    private readonly User _subHead;
    private readonly User _brokenSubHead;
    private readonly User _lawyer1;
    private readonly User _lawyer2;
    private readonly User _delegate;

    private static readonly DateTimeOffset FixedNow =
        new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    public ForumServiceTests()
    {
        _db = TestDb.Create();
        _clock = new FakeTimeProvider(FixedNow);

        var branch = new Branch { Name = "اللاذقية", Code = "LAT", Governorate = "اللاذقية" };
        var otherBranch = new Branch { Name = "دمشق", Code = "DAM", Governorate = "دمشق" };
        _db.Branches.AddRange(branch, otherBranch);
        _db.SaveChanges();
        _branchId = branch.Id;
        _otherBranchId = otherBranch.Id;

        var section = new Section { BranchId = _branchId, Name = "جبلة", NameNorm = "جبلة" };
        _db.Sections.Add(section);
        _db.SaveChanges();
        _sectionId = section.Id;

        _admin = NewUser("frm_admin", "المشرف", UserRole.Admin, null);
        _manager = NewUser("frm_mgr", "المدير", UserRole.Manager, null);
        _head = NewUser("frm_head", "رئيس القسم", UserRole.Head, _branchId);
        _subHead = NewUser("frm_sub", "رئيس الشعبة", UserRole.SubHead, _branchId, _sectionId);
        _brokenSubHead = NewUser("frm_sub_broken", "رئيس بلا شعبة", UserRole.SubHead, _branchId);
        _lawyer1 = NewUser("frm_law1", "المحامي الأول", UserRole.Lawyer, _branchId);
        _lawyer2 = NewUser("frm_law2", "محامي دمشق", UserRole.Lawyer, _otherBranchId);
        _delegate = NewUser("frm_del", "مندوب الجهة", UserRole.EntityManager, null);
        _db.Users.AddRange(_admin, _manager, _head, _subHead, _brokenSubHead, _lawyer1, _lawyer2, _delegate);
        _db.SaveChanges();

        _service = BuildService();
    }

    public void Dispose() => _db.Dispose();

    private ForumService BuildService(TimeProvider? clock = null)
    {
        var uow = new UnitOfWork(_db);
        return new ForumService(
            new ForumRepository(_db),
            new Repository<ForumMessageRead>(_db),
            new UserRepository(_db),
            uow,
            new TransactionRunner(_db),
            new DbExceptionClassifier(),
            clock ?? _clock);
    }

    private ForumRetentionService BuildRetention(int months, TimeProvider? clock = null)
        => new(
            new ForumRepository(_db),
            Options.Create(new ForumOptions { RetentionMonths = months }),
            clock ?? _clock);

    private static User NewUser(string username, string fullName, UserRole role, int? branchId, int? sectionId = null)
        => new()
        {
            Username = username,
            FullName = fullName,
            Role = role,
            BranchId = branchId,
            SectionId = sectionId,
            PasswordHash = "x",
        };

    private Task<ForumMessageDto> PostAsync(User author, string body = "رسالة اختبار", int? quotedId = null)
        => _service.PostAsync(new PostForumMessageRequest(body, quotedId), author.Id);

    // ---------- النشر والهوية ----------

    [Fact]
    public async Task Post_SnapshotsAuthorIdentity_PartsOnly()
    {
        var dto = await PostAsync(_lawyer1, "مرحبا بالمنتدى");

        Assert.Equal("المحامي الأول", dto.AuthorName);
        Assert.Equal("lawyer", dto.AuthorRole);
        Assert.Equal("اللاذقية", dto.AuthorLocation);
        Assert.Null(dto.AuthorSection);
        Assert.False(dto.IsPinned);
        Assert.Equal("مرحبا بالمنتدى", dto.Body);
    }

    [Fact]
    public async Task Post_SubHead_SnapshotsSectionName()
    {
        var dto = await PostAsync(_subHead);

        Assert.Equal("subhead", dto.AuthorRole);
        Assert.Equal("جبلة", dto.AuthorSection);
        Assert.Equal("اللاذقية", dto.AuthorLocation);
    }

    [Fact]
    public async Task Post_BranchlessAdmin_LocationFallsBackToAllBranches()
    {
        var dto = await PostAsync(_admin);

        Assert.Equal("admin", dto.AuthorRole);
        Assert.Equal("كل الفروع", dto.AuthorLocation);
    }

    [Fact]
    public async Task Post_EmptyBody_ThrowsArgument()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => PostAsync(_lawyer1, "   "));
    }

    [Fact]
    public async Task Post_OverLimitBody_ThrowsArgument()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => PostAsync(_lawyer1, new string('س', 2001)));
    }

    [Fact]
    public async Task Post_Delegate_ThrowsUnauthorized()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => PostAsync(_delegate));
    }

    [Fact]
    public async Task Post_BrokenSubHead_ThrowsUnauthorized()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => PostAsync(_brokenSubHead));
    }

    [Fact]
    public async Task Post_InactiveUser_ThrowsUnauthorized()
    {
        _lawyer1.IsActive = false;
        await _db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => PostAsync(_lawyer1));
    }

    // ---------- الاقتباس ----------

    [Fact]
    public async Task Post_WithQuote_SnapshotsAuthorAndExcerpt()
    {
        var original = await PostAsync(_lawyer1, "النص الأصلي للاقتباس");
        var reply = await PostAsync(_lawyer2, "رد", original.Id);

        Assert.Equal(original.Id, reply.QuotedMessageId);
        Assert.Equal("المحامي الأول", reply.QuotedAuthorName);
        Assert.Equal("النص الأصلي للاقتباس", reply.QuotedExcerpt);
    }

    [Fact]
    public async Task Post_WithMissingQuote_ThrowsArgument()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => PostAsync(_lawyer1, "رد", 9999));
    }

    [Fact]
    public async Task Delete_QuotedMessage_KeepsSnapshotAndNullsReference()
    {
        var original = await PostAsync(_lawyer1, "الأصل");
        var reply = await PostAsync(_lawyer2, "رد", original.Id);

        await _service.DeleteAsync(original.Id, _lawyer1.Id);

        var reloaded = await _service.GetByIdAsync(reply.Id, _lawyer2.Id);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded.QuotedMessageId);
        Assert.Equal("المحامي الأول", reloaded.QuotedAuthorName);
        Assert.Equal("الأصل", reloaded.QuotedExcerpt);
    }

    // ---------- الترقيم بمفتاح ----------

    [Fact]
    public async Task List_TailReturnsLatestAscending_WithHasOlder()
    {
        await PostAsync(_lawyer1, "1");
        await PostAsync(_lawyer1, "2");
        await PostAsync(_lawyer1, "3");

        var page = await _service.ListAsync(_lawyer1.Id, 2, null, null, null);

        Assert.Equal(new[] { "2", "3" }, page.Items.Select(m => m.Body));
        Assert.True(page.HasOlder);
    }

    [Fact]
    public async Task List_BeforeCursor_ReturnsOlderSlice()
    {
        var first = await PostAsync(_lawyer1, "1");
        await PostAsync(_lawyer1, "2");
        await PostAsync(_lawyer1, "3");
        var tail = await _service.ListAsync(_lawyer1.Id, 2, null, null, null);

        var older = await _service.ListAsync(_lawyer1.Id, 2, tail.Items[0].Id, null, null);

        Assert.Equal(new[] { first.Id }, older.Items.Select(m => m.Id));
        Assert.False(older.HasOlder);
    }

    [Fact]
    public async Task List_InsertDuringScroll_NeitherDuplicatesNorSkips()
    {
        await PostAsync(_lawyer1, "1");
        await PostAsync(_lawyer1, "2");
        var tail = await _service.ListAsync(_lawyer1.Id, 2, null, null, null);
        var fresh = await PostAsync(_lawyer1, "3");

        var older = await _service.ListAsync(_lawyer1.Id, 2, tail.Items[0].Id, null, null);
        var newer = await _service.ListAsync(_lawyer1.Id, 10, null, tail.Items[^1].Id, null);

        var walked = older.Items.Concat(tail.Items).Select(m => m.Id).ToList();
        Assert.Equal(walked.Count, walked.Distinct().Count());
        Assert.DoesNotContain(fresh.Id, walked);
        Assert.Equal(new[] { fresh.Id }, newer.Items.Select(m => m.Id));
    }

    [Fact]
    public async Task List_Search_MatchesBodyOnly()
    {
        await PostAsync(_lawyer1, "تقرير الجلسة الأسبوعي");
        await PostAsync(_lawyer1, "تحية طيبة");

        var page = await _service.ListAsync(_lawyer1.Id, 30, null, null, "الجلسة");

        Assert.Single(page.Items);
        Assert.Contains("الجلسة", page.Items[0].Body);
    }

    [Fact]
    public async Task List_IsCrossBranch_GlobalStream()
    {
        await PostAsync(_lawyer2, "من دمشق");

        var page = await _service.ListAsync(_lawyer1.Id, 30, null, null, null);

        Assert.Contains(page.Items, m => m.Body == "من دمشق" && m.AuthorLocation == "دمشق");
    }

    // ---------- التعديل والحذف (مصفوفة §2-أ) ----------

    [Fact]
    public async Task Update_OwnMessage_SetsEditedStamp()
    {
        var posted = await PostAsync(_lawyer1, "قبل");
        _clock.Advance(TimeSpan.FromMinutes(5));

        var updated = await _service.UpdateAsync(posted.Id, new UpdateForumMessageRequest("بعد"), _lawyer1.Id);

        Assert.Equal("بعد", updated.Body);
        Assert.Equal(_lawyer1.Id, updated.EditedById);
        Assert.NotNull(updated.EditedAtUtc);
    }

    [Fact]
    public async Task Update_OthersMessage_ThrowsUnauthorized_EvenForAdmin()
    {
        var posted = await PostAsync(_lawyer1);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.UpdateAsync(posted.Id, new UpdateForumMessageRequest("x"), _lawyer2.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.UpdateAsync(posted.Id, new UpdateForumMessageRequest("x"), _admin.Id));
    }

    [Fact]
    public async Task Update_MissingMessage_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.UpdateAsync(9999, new UpdateForumMessageRequest("x"), _lawyer1.Id));
    }

    [Fact]
    public async Task Delete_OwnMessage_RemovesItWithReads()
    {
        var posted = await PostAsync(_lawyer1);
        await _service.MarkReadAsync(_lawyer2.Id, "محامي دمشق", posted.Id);

        await _service.DeleteAsync(posted.Id, _lawyer1.Id);

        Assert.Null(await _service.GetByIdAsync(posted.Id, _lawyer1.Id));
        Assert.Empty(_db.ForumMessageReads.Where(r => r.MessageId == posted.Id));
    }

    [Fact]
    public async Task Delete_OthersMessage_ThrowsUnauthorized()
    {
        var posted = await PostAsync(_lawyer1);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.DeleteAsync(posted.Id, _lawyer2.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.DeleteAsync(posted.Id, _manager.Id));
    }

    [Fact]
    public async Task Delete_ByAdmin_RemovesAnyMessage()
    {
        var posted = await PostAsync(_lawyer1);

        await _service.DeleteAsync(posted.Id, _admin.Id);

        Assert.Null(await _service.GetByIdAsync(posted.Id, _admin.Id));
    }

    [Fact]
    public async Task Delete_MissingMessage_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.DeleteAsync(9999, _admin.Id));
    }

    // ---------- التثبيت ----------

    [Fact]
    public async Task Pin_ByAdmin_PinsAndUnpinsPrevious()
    {
        var first = await PostAsync(_lawyer1, "أولى");
        var second = await PostAsync(_lawyer1, "ثانية");

        await _service.TogglePinAsync(first.Id, _admin.Id);
        Assert.True((await _service.GetByIdAsync(first.Id, _admin.Id))!.IsPinned);

        await _service.TogglePinAsync(second.Id, _admin.Id);
        Assert.False((await _service.GetByIdAsync(first.Id, _admin.Id))!.IsPinned);
        Assert.True((await _service.GetByIdAsync(second.Id, _admin.Id))!.IsPinned);

        var pinned = await _service.GetPinnedAsync(_admin.Id);
        Assert.Equal(second.Id, pinned!.Id);
    }

    [Fact]
    public async Task Pin_ToggleOff_Unpins()
    {
        var posted = await PostAsync(_lawyer1);
        await _service.TogglePinAsync(posted.Id, _admin.Id);

        var result = await _service.TogglePinAsync(posted.Id, _admin.Id);

        Assert.False(result.IsPinned);
        Assert.Null(await _service.GetPinnedAsync(_admin.Id));
    }

    [Fact]
    public async Task Pin_ByNonAdmin_ThrowsUnauthorized()
    {
        var posted = await PostAsync(_lawyer1);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.TogglePinAsync(posted.Id, _manager.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.TogglePinAsync(posted.Id, _lawyer1.Id));
    }

    [Fact]
    public async Task Pin_MissingMessage_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.TogglePinAsync(9999, _admin.Id));
    }

    // ---------- القراءة والعدّاد ----------

    [Fact]
    public async Task MarkRead_CreatesBatchReceipts_AndIsMonotonic()
    {
        var first = await PostAsync(_lawyer1, "1");
        var second = await PostAsync(_lawyer1, "2");

        var max = await _service.MarkReadAsync(_lawyer2.Id, "محامي دمشق", second.Id);

        Assert.Equal(second.Id, max);
        Assert.Equal(2, _db.ForumMessageReads.Count(r => r.UserId == _lawyer2.Id));

        var same = await _service.MarkReadAsync(_lawyer2.Id, "محامي دمشق", first.Id);
        Assert.Equal(second.Id, same);
        Assert.Equal(2, _db.ForumMessageReads.Count(r => r.UserId == _lawyer2.Id));
    }

    [Fact]
    public async Task MarkRead_NeverRecordsSelfReads()
    {
        await PostAsync(_lawyer1, "1");
        var second = await PostAsync(_lawyer1, "2");

        var max = await _service.MarkReadAsync(_lawyer1.Id, "المحامي الأول", second.Id);

        Assert.Equal(second.Id, max);
        Assert.Empty(_db.ForumMessageReads.Where(r => r.UserId == _lawyer1.Id));
    }

    [Fact]
    public async Task UnreadCount_ExcludesOwnMessages()
    {
        await PostAsync(_lawyer1, "لي");

        Assert.Equal(0, await _service.CountUnreadAsync(_lawyer1.Id));
        Assert.Equal(1, await _service.CountUnreadAsync(_lawyer2.Id));
    }

    [Fact]
    public async Task Readers_NeverIncludeAuthorThemselves()
    {
        var posted = await PostAsync(_lawyer1);
        await _service.MarkReadAsync(_lawyer1.Id, "المحامي الأول", posted.Id);

        var readers = await _service.GetReadersAsync(posted.Id, _lawyer1.Id);

        Assert.Empty(readers);
    }

    [Fact]
    public async Task MarkRead_ConcurrentDuplicateInsert_TreatedAsSuccess()
    {
        var posted = await PostAsync(_lawyer1, "1");
        // متسابق وثّق قبيل حفظنا (نحاكي قدم القراءة بتجاوز يخفي الإيصال القائم).
        _db.ForumMessageReads.Add(new ForumMessageRead
        {
            MessageId = posted.Id,
            UserId = _lawyer2.Id,
            UserName = "محامي دمشق",
            ReadAtUtc = FixedNow.UtcDateTime,
        });
        await _db.SaveChangesAsync();

        var racing = new ForumService(
            new StaleReadForumRepository(_db),
            new Repository<ForumMessageRead>(_db),
            new UserRepository(_db),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            new DbExceptionClassifier(),
            _clock);

        var max = await racing.MarkReadAsync(_lawyer2.Id, "محامي دمشق", posted.Id);

        Assert.Equal(posted.Id, max);
        Assert.Single(_db.ForumMessageReads.Where(r => r.UserId == _lawyer2.Id));
    }

    /// <summary>يخفي الإيصالات القائمة لمحاكاة قراءة متسابق بين الفحص والحفظ.</summary>
    private sealed class StaleReadForumRepository : ForumRepository
    {
        public StaleReadForumRepository(DocGeneratorDbContext db) : base(db) { }

        public override Task<List<int>> ReadIdsAsync(
            int userId, IReadOnlyCollection<int> messageIds, CancellationToken ct = default)
            => Task.FromResult(new List<int>());
    }

    [Fact]
    public async Task MarkRead_SkipsDeletedIds_ButAdvancesWatermark()
    {
        var first = await PostAsync(_lawyer1, "1");
        var second = await PostAsync(_lawyer1, "2");
        await _service.DeleteAsync(first.Id, _admin.Id);

        var max = await _service.MarkReadAsync(_lawyer2.Id, "محامي دمشق", second.Id);

        Assert.Equal(second.Id, max);
        Assert.Equal(new[] { second.Id },
            _db.ForumMessageReads.Where(r => r.UserId == _lawyer2.Id).Select(r => r.MessageId));
    }

    [Fact]
    public async Task UnreadCount_DropsToZeroAfterFullRead()
    {
        await PostAsync(_lawyer1, "1");
        var second = await PostAsync(_lawyer1, "2");

        Assert.Equal(2, await _service.CountUnreadAsync(_lawyer2.Id));

        await _service.MarkReadAsync(_lawyer2.Id, "محامي دمشق", second.Id);

        Assert.Equal(0, await _service.CountUnreadAsync(_lawyer2.Id));
    }

    [Fact]
    public async Task Readers_VisibleToAuthorOnly_WithSnapshotName()
    {
        var posted = await PostAsync(_lawyer1);
        await _service.MarkReadAsync(_lawyer2.Id, "محامي دمشق", posted.Id);

        var readers = await _service.GetReadersAsync(posted.Id, _lawyer1.Id);

        Assert.Single(readers);
        Assert.Equal(_lawyer2.Id, readers[0].UserId);
        Assert.Equal("محامي دمشق", readers[0].UserName);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.GetReadersAsync(posted.Id, _lawyer2.Id));
    }

    [Fact]
    public async Task Readers_KeepSnapshotAfterRename()
    {
        var posted = await PostAsync(_lawyer1);
        await _service.MarkReadAsync(_lawyer2.Id, "محامي دمشق", posted.Id);

        _lawyer2.FullName = "اسم جديد";
        await _db.SaveChangesAsync();

        var readers = await _service.GetReadersAsync(posted.Id, _lawyer1.Id);
        Assert.Equal("محامي دمشق", readers[0].UserName);
    }

    [Fact]
    public async Task Readers_MissingMessage_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetReadersAsync(9999, _lawyer1.Id));
    }

    [Fact]
    public async Task List_ReadCount_OnlyForOwnMessages()
    {
        await PostAsync(_lawyer1, "لي");
        await PostAsync(_lawyer2, "له");
        // اطلاع الغير على رسالتي (لا اطلاعي أنا — قراءة الذات لا توثَّق).
        await _service.MarkReadAsync(_lawyer2.Id, "محامي دمشق", 100);

        var page = await _service.ListAsync(_lawyer1.Id, 30, null, null, null);

        var mine = page.Items.Single(m => m.AuthorId == _lawyer1.Id);
        var others = page.Items.Single(m => m.AuthorId == _lawyer2.Id);
        Assert.Equal(1, mine.ReadCount);
        Assert.Equal(0, others.ReadCount);
    }

    // ---------- الاحتفاظ ----------

    private async Task<ForumMessage> SeedMessageAsync(User author, DateTime createdAt, bool pinned = false)
    {
        var message = new ForumMessage
        {
            Body = "مؤرشفة",
            AuthorId = author.Id,
            AuthorName = author.FullName,
            AuthorRole = author.Role.ToString().ToLowerInvariant(),
            AuthorLocation = "اللاذقية",
            IsPinned = pinned,
            CreatedAt = createdAt,
        };
        _db.ForumMessages.Add(message);
        await _db.SaveChangesAsync();
        return message;
    }

    [Fact]
    public async Task Retention_DeletesOnlyStaleUnpinned_WithTheirReads()
    {
        var now = FixedNow.UtcDateTime;
        var cutoff = now.AddMonths(-6);
        var stale = await SeedMessageAsync(_lawyer1, cutoff.AddSeconds(-1));
        var boundary = await SeedMessageAsync(_lawyer1, cutoff);
        var fresh = await SeedMessageAsync(_lawyer1, now.AddDays(-1));
        var stalePinned = await SeedMessageAsync(_lawyer1, cutoff.AddDays(-10), pinned: true);
        _db.ForumMessageReads.Add(new ForumMessageRead
        {
            MessageId = stale.Id,
            UserId = _lawyer2.Id,
            UserName = "محامي دمشق",
            ReadAtUtc = now,
        });
        await _db.SaveChangesAsync();

        var deleted = await BuildRetention(6).ExecuteOnceAsync();

        Assert.Equal(1, deleted);
        Assert.Null(await _db.ForumMessages.FindAsync(stale.Id));
        Assert.NotNull(await _db.ForumMessages.FindAsync(boundary.Id));
        Assert.NotNull(await _db.ForumMessages.FindAsync(fresh.Id));
        Assert.NotNull(await _db.ForumMessages.FindAsync(stalePinned.Id));
        Assert.Empty(_db.ForumMessageReads.Where(r => r.MessageId == stale.Id));
    }

    [Fact]
    public async Task Retention_Disabled_ReturnsZeroWithoutDeleting()
    {
        await SeedMessageAsync(_lawyer1, FixedNow.UtcDateTime.AddYears(-2));

        var deleted = await BuildRetention(0).ExecuteOnceAsync();

        Assert.Equal(0, deleted);
        Assert.Equal(1, await _db.ForumMessages.CountAsync());
    }

    [Fact]
    public async Task Retention_NullsQuotesToStale_KeepingSnapshots()
    {
        var now = FixedNow.UtcDateTime;
        var stale = await SeedMessageAsync(_lawyer1, now.AddMonths(-7));
        var quoter = await PostAsync(_lawyer2, "تقتبس القديم", stale.Id);

        await BuildRetention(6).ExecuteOnceAsync();

        var reloaded = await _service.GetByIdAsync(quoter.Id, _lawyer2.Id);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded.QuotedMessageId);
        Assert.Equal(stale.AuthorName, reloaded.QuotedAuthorName);
    }
}
