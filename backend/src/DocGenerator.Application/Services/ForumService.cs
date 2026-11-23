using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// عقد الاستثناءات (كبقية الخدمات): غائب ← `KeyNotFoundException` (404)، ومدخل
/// خاطئ ← `ArgumentException` (400)، ورفض صلاحية ← `UnauthorizedAccessException`
/// (403)، وسباق تثبيت ← `DocumentConflictException` (409) — لا يُرمى أحدهما
/// مكان الآخر في أي مسار.
/// </summary>
public interface IForumService
{
    /// <summary>شريحة التيار بمفتاح `Id` (الأحدث أولًا بلا مؤشر) + `readCount` لرسائلي.</summary>
    Task<ForumMessagesPageDto> ListAsync(int actorUserId, int limit, int? before, int? after, CancellationToken ct = default);

    /// <summary>رسالة واحدة بالمعرف (`null` للغائبة — دون كشف إضافي).</summary>
    Task<ForumMessageDto?> GetByIdAsync(int id, int actorUserId, CancellationToken ct = default);

    /// <summary>الرسالة المثبّتة للشريط (`null` عند عدمها).</summary>
    Task<ForumMessageDto?> GetPinnedAsync(int actorUserId, CancellationToken ct = default);

    /// <summary>نشر رسالة: تحقق + لقط هوية الكاتب خلفيًا من `User+Branch+Section`.</summary>

    /// <summary>تعديل رسالة — الكاتب فقط (يختم «عُدّل»).</summary>

    /// <summary>حذف صلب — الكاتب لرسالته والمشرف لأي رسالة (مع إيصالاتها).</summary>

    /// <summary>تبديل التثبيت — المشرف فقط (الجديد يُسقط السابق).</summary>

    /// <summary>تعليم القراءة حتى رسالة — عدّادي (`max` فقط بلا تراجع)؛ يُعيد العلامة الجديدة.</summary>

    /// <summary>نافذة «شوهدت بواسطة» — الكاتب فقط.</summary>
    Task<List<ForumReaderDto>> GetReadersAsync(int id, int actorUserId, CancellationToken ct = default);

    /// <summary>مرشحو الذِكر بالاسم — نشطون داخليون فقط بلا الذات ولا المندوب.</summary>
    Task<List<ForumUserLookupDto>> SearchUsersAsync(string? q, int actorUserId, CancellationToken ct = default);

    /// <summary>العدّاد — رسائل بلا إيصال لي (شارة الأيقونة).</summary>
    Task<int> CountUnreadAsync(int actorUserId, CancellationToken ct = default);
}

/// <summary>
/// منتدى المحامين: تيار واحد مسطّح عابر للفروع عمدًا — نص عادي فقط، والهوية لقطات
/// تُجمَع من المستخدم وفرعه وشعبته لحظة الإرسال (لا من مدخلات العميل إطلاقًا)،
/// والحذف صلب دائمًا. بلا سجل تدقيق (خارج النطاق بقرار المالك).
/// الخدمة للقراءة والبحث فقط؛ النشر/التعديل/الحذف/التثبيت/القراءة عبر أوامر `P4-01`.
/// </summary>
public sealed class ForumService : IForumService
{
    /// <summary>حدّ النص (يطابق عمود القاعدة — يُفرض هنا برسالة عربية لا بالاستثناء الخام).</summary>
    public const int MaxBodyLength = 2000;
    public const int DefaultLimit = 30;
    public const int MaxLimit = 100;
    public const int MaxQuotedAuthorLength = 150;
    public const int MaxQuotedExcerptLength = 200;
    /// <summary>سقف المذكورين المميزين في الرسالة الواحدة (مرحلة 2).</summary>
    public const int MaxMentionsPerMessage = 10;
    /// <summary>سقف مرشحي منتقي الذِكر المعروضين.</summary>
    public const int MentionLookupLimit = 20;

    private readonly IForumRepository _forum;
    private readonly IUserRepository _users;

    public ForumService(
        IForumRepository forum,
        IUserRepository users)
    {
        _forum = forum;
        _users = users;
    }

    public async Task<ForumMessagesPageDto> ListAsync(int actorUserId, int limit, int? before, int? after, CancellationToken ct = default)
    {
        if (limit < 1 || limit > MaxLimit)
            limit = DefaultLimit;
        var items = await _forum.ListSliceAsync(limit, before, after, ct);
        var counts = await _forum.CountReadsAsync(
            items.Where(m => m.AuthorId == actorUserId).Select(m => m.Id).ToList(), ct);
        var dtos = items.Select(m => ToDto(m, counts.GetValueOrDefault(m.Id))).ToList();
        var hasOlder = dtos.Count > 0 && await _forum.HasOlderAsync(dtos[0].Id, ct);
        return new ForumMessagesPageDto(dtos, hasOlder);
    }

    public async Task<ForumMessageDto?> GetByIdAsync(int id, int actorUserId, CancellationToken ct = default)
    {
        var message = await _forum.GetByIdAsync(id, ct);
        if (message is null)
            return null;
        var counts = message.AuthorId == actorUserId
            ? await _forum.CountReadsAsync(new[] { message.Id }, ct)
            : new Dictionary<int, int>();
        return ToDto(message, counts.GetValueOrDefault(message.Id));
    }

    public async Task<ForumMessageDto?> GetPinnedAsync(int actorUserId, CancellationToken ct = default)
    {
        var pinned = await _forum.GetPinnedAsync(ct);
        if (pinned is null)
            return null;
        var counts = pinned.AuthorId == actorUserId
            ? await _forum.CountReadsAsync(new[] { pinned.Id }, ct)
            : new Dictionary<int, int>();
        return ToDto(pinned, counts.GetValueOrDefault(pinned.Id));
    }

    public async Task<List<ForumReaderDto>> GetReadersAsync(int id, int actorUserId, CancellationToken ct = default)
    {
        var message = await _forum.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("الرسالة غير موجودة");
        if (message.AuthorId != actorUserId)
            throw new UnauthorizedAccessException("نافذة «شوهدت بواسطة» متاحة لكاتب الرسالة فقط");
        var reads = await _forum.ListReadersAsync(id, ct);
        return reads.Select(r => new ForumReaderDto(r.UserId, r.UserName, r.ReadAtUtc)).ToList();
    }

    public async Task<int> CountUnreadAsync(int actorUserId, CancellationToken ct = default)
    {
        await RequireForumMemberAsync(actorUserId, ct);
        var max = await _forum.MaxReadMessageIdAsync(actorUserId, ct);
        return await _forum.CountUnreadAsync(actorUserId, max, ct);
    }

    /// <summary>
    /// عضوية المنتدى (مشتركة عبر <see cref="ForumMembership"/>) — تبقى هنا لمسارات
    /// الخدمة الداخلية، ونقطة الحقيقة واحدة مع المعالجات.
    /// </summary>
    private Task<User> RequireForumMemberAsync(int actorUserId, CancellationToken ct)
        => ForumMembership.RequireAsync(_users, actorUserId, ct);

    public async Task<List<ForumUserLookupDto>> SearchUsersAsync(string? q, int actorUserId, CancellationToken ct = default)
    {
        await RequireForumMemberAsync(actorUserId, ct);
        var users = await _users.SearchForumMentionTargetsAsync(actorUserId, q, MentionLookupLimit, ct);
        return users.Select(u => new ForumUserLookupDto(
            u.Id,
            ForumIdentityParts.DisplayName(u),
            u.Role.ToString().ToLowerInvariant(),
            ForumIdentityParts.ResolveLocation(u),
            ForumIdentityParts.ResolveSection(u))).ToList();
    }

    internal static ForumMessageDto ToDto(ForumMessage m, int readCount) => new(
        m.Id,
        m.Body,
        m.AuthorId,
        m.AuthorName,
        m.AuthorRole,
        m.AuthorLocation,
        m.AuthorSection,
        m.IsPinned,
        m.QuotedMessageId,
        m.QuotedAuthorName,
        m.QuotedExcerpt,
        m.CreatedAt,
        m.EditedById,
        m.EditedAtUtc,
        readCount,
        m.Version);
}

/// <summary>
/// أجزاء هوية المستخدم للقطات المنتدى (الاسم/الموقع/الشعبة): مصدر واحد تشترك
/// فيه لقطة الكاتب ومرشحو الذِكر وإعادة صياغته — بلا ازدواج منطق.
/// </summary>
internal static class ForumIdentityParts
{
    internal static string DisplayName(User user)
        => string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName.Trim();

    internal static string ResolveLocation(User user)
    {
        var governorate = user.Branch?.Governorate?.Trim();
        if (!string.IsNullOrEmpty(governorate))
            return governorate;
        var name = user.Branch?.Name?.Trim();
        return string.IsNullOrEmpty(name) ? "كل الفروع" : name;
    }

    internal static string? ResolveSection(User user)
    {
        if (user.Role != UserRole.SubHead)
            return null;
        var name = user.Section?.Name?.Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }
}
