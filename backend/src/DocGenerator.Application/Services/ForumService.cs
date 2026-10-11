using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using System.Text.RegularExpressions;

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
    Task<ForumMessagesPageDto> ListAsync(int actorUserId, int limit, int? before, int? after, string? q, CancellationToken ct = default);

    /// <summary>رسالة واحدة بالمعرف (`null` للغائبة — دون كشف إضافي).</summary>
    Task<ForumMessageDto?> GetByIdAsync(int id, int actorUserId, CancellationToken ct = default);

    /// <summary>الرسالة المثبّتة للشريط (`null` عند عدمها).</summary>
    Task<ForumMessageDto?> GetPinnedAsync(int actorUserId, CancellationToken ct = default);

    /// <summary>نشر رسالة: تحقق + لقط هوية الكاتب خلفيًا من `User+Branch+Section`.</summary>
    Task<ForumMessageDto> PostAsync(PostForumMessageRequest request, int actorUserId, CancellationToken ct = default);

    /// <summary>تعديل رسالة — الكاتب فقط (يختم «عُدّل»).</summary>
    Task<ForumMessageDto> UpdateAsync(int id, UpdateForumMessageRequest request, int actorUserId, CancellationToken ct = default);

    /// <summary>حذف صلب — الكاتب لرسالته والمشرف لأي رسالة (مع إيصالاتها).</summary>
    Task DeleteAsync(int id, int actorUserId, CancellationToken ct = default);

    /// <summary>تبديل التثبيت — المشرف فقط (الجديد يُسقط السابق).</summary>
    Task<ForumMessageDto> TogglePinAsync(int id, int actorUserId, CancellationToken ct = default);

    /// <summary>تعليم القراءة حتى رسالة — عدّادي (`max` فقط بلا تراجع)؛ يُعيد العلامة الجديدة.</summary>
    Task<int> MarkReadAsync(int actorUserId, string? actorName, int upToMessageId, CancellationToken ct = default);

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

    /// <summary>
    /// رمز الذِكر القانوني `@[id:الاسم]` — مولَّد من المنتقي، ويُعاد توليده من
    /// الخادم عند الحفظ (فلا يُزوَّر اسم لمعرف). الشاذ منه نص حرفي بلا شريحة.
    /// </summary>
    private static readonly Regex MentionToken =
        new(@"@\[(\d{1,9}):([^\[\]]{1,100})\]", RegexOptions.Compiled);

    private readonly IForumRepository _forum;
    private readonly IRepository<ForumMessageRead> _reads;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IDbExceptionClassifier _errors;
    private readonly TimeProvider _clock;

    public ForumService(
        IForumRepository forum,
        IRepository<ForumMessageRead> reads,
        IUserRepository users,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IDbExceptionClassifier errors,
        TimeProvider clock)
    {
        _forum = forum;
        _reads = reads;
        _users = users;
        _uow = uow;
        _tx = tx;
        _errors = errors;
        _clock = clock;
    }

    public async Task<ForumMessagesPageDto> ListAsync(int actorUserId, int limit, int? before, int? after, string? q, CancellationToken ct = default)
    {
        if (limit < 1 || limit > MaxLimit)
            limit = DefaultLimit;
        var items = await _forum.ListSliceAsync(limit, before, after, q, ct);
        var counts = await _forum.CountReadsAsync(
            items.Where(m => m.AuthorId == actorUserId).Select(m => m.Id).ToList(), ct);
        var dtos = items.Select(m => ToDto(m, counts.GetValueOrDefault(m.Id))).ToList();
        var hasOlder = dtos.Count > 0 && await _forum.HasOlderAsync(dtos[0].Id, q, ct);
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

    public async Task<ForumMessageDto> PostAsync(PostForumMessageRequest request, int actorUserId, CancellationToken ct = default)
    {
        var body = await ResolveMentionsAsync(RequireBody(request.Body), ct);
        var author = await RequireForumMemberAsync(actorUserId, ct);

        string? quotedAuthor = null;
        string? quotedExcerpt = null;
        if (request.QuotedMessageId.HasValue)
        {
            var quoted = await _forum.GetByIdAsync(request.QuotedMessageId.Value, ct)
                ?? throw new ArgumentException("الرسالة المقتبسة غير موجودة");
            quotedAuthor = Truncate(quoted.AuthorName, MaxQuotedAuthorLength);
            quotedExcerpt = Truncate(quoted.Body, MaxQuotedExcerptLength);
        }

        // مصدر الوقت الوحيد: الساعة المحقونة (قابلة للتجميد في الاختبارات).
        var now = _clock.GetUtcNow().UtcDateTime;
        var message = new ForumMessage
        {
            Body = body,
            AuthorId = author.Id,
            AuthorName = ForumIdentityParts.DisplayName(author),
            AuthorRole = author.Role.ToString().ToLowerInvariant(),
            AuthorLocation = ForumIdentityParts.ResolveLocation(author),
            AuthorSection = ForumIdentityParts.ResolveSection(author),
            QuotedMessageId = request.QuotedMessageId,
            QuotedAuthorName = quotedAuthor,
            QuotedExcerpt = quotedExcerpt,
            CreatedAt = now,
        };

        await _tx.RunAsync(async token =>
        {
            await _forum.AddAsync(message, token);
            await _uow.SaveChangesAsync(token);
        }, ct);

        return ToDto(message, 0);
    }

    public async Task<ForumMessageDto> UpdateAsync(int id, UpdateForumMessageRequest request, int actorUserId, CancellationToken ct = default)
    {
        var body = await ResolveMentionsAsync(RequireBody(request.Body), ct);
        var message = await _forum.GetTrackedByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("الرسالة غير موجودة");
        if (message.AuthorId != actorUserId)
            throw new UnauthorizedAccessException("تعديل الرسالة متاح لكاتبها فقط");

        var now = _clock.GetUtcNow().UtcDateTime;
        await _tx.RunAsync(async token =>
        {
            message.Body = body;
            message.EditedById = actorUserId;
            message.EditedAtUtc = now;
            await _uow.SaveChangesAsync(token);
        }, ct);

        var counts = await _forum.CountReadsAsync(new[] { message.Id }, ct);
        return ToDto(message, counts.GetValueOrDefault(message.Id));
    }

    public async Task DeleteAsync(int id, int actorUserId, CancellationToken ct = default)
    {
        var message = await _forum.GetTrackedByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("الرسالة غير موجودة");
        var actor = await RequireForumMemberAsync(actorUserId, ct);
        if (message.AuthorId != actorUserId && actor.Role != UserRole.Admin)
            throw new UnauthorizedAccessException("حذف الرسالة متاح لكاتبها أو للمشرف فقط");

        // حذف صريح مرتب (الإيصالات أولًا) — لا اعتماد على تتالي المزود.
        await _tx.RunAsync(token => _forum.DeleteMessageAsync(id, token), ct);
    }

    public async Task<ForumMessageDto> TogglePinAsync(int id, int actorUserId, CancellationToken ct = default)
    {
        var actor = await RequireForumMemberAsync(actorUserId, ct);
        if (actor.Role != UserRole.Admin)
            throw new UnauthorizedAccessException("التثبيت متاح للمشرف فقط");

        var message = await _forum.GetTrackedByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("الرسالة غير موجودة");

        try
        {
            await _tx.RunAsync(async token =>
            {
                if (message.IsPinned)
                {
                    message.IsPinned = false;
                }
                else
                {
                    var current = await _forum.GetTrackedPinnedAsync(token);
                    if (current is not null && current.Id != message.Id)
                        current.IsPinned = false;
                    message.IsPinned = true;
                }
                await _uow.SaveChangesAsync(token);
            }, ct);
        }
        catch (Exception ex) when (_errors.IsUniqueViolation(ex))
        {
            // سباق تثبيت مزدوج: القيد الفريد هو الحارس الأخير — خطأ ودود لا 500.
            throw new DocumentConflictException("تزامن تثبيت مع مستخدم آخر — حُدّثت الحالة، أعد المحاولة", ex);
        }

        // العدّاد لرسائل الكاتب نفسه فقط (كبقية المسارات — لا كشف لغيره).
        var pinCounts = message.AuthorId == actorUserId
            ? await _forum.CountReadsAsync(new[] { message.Id }, ct)
            : new Dictionary<int, int>();
        return ToDto(message, pinCounts.GetValueOrDefault(message.Id));
    }

    public async Task<int> MarkReadAsync(int actorUserId, string? actorName, int upToMessageId, CancellationToken ct = default)
    {
        if (upToMessageId < 1)
            throw new ArgumentException("معرف الرسالة غير صالح");
        var member = await RequireForumMemberAsync(actorUserId, ct);

        var currentMax = await _forum.MaxReadMessageIdAsync(actorUserId, ct);
        if (upToMessageId <= currentMax)
            return currentMax;

        // رسائل الغير فقط — قراءة الذات لا توثَّق (تُستثنى في المستودع).
        var existing = await _forum.ExistingIdsInRangeAsync(actorUserId, currentMax, upToMessageId, ct);
        var already = await _forum.ReadIdsAsync(actorUserId, existing, ct);
        var missing = existing.Except(already).ToList();
        if (missing.Count == 0)
            return upToMessageId;

        var name = string.IsNullOrWhiteSpace(actorName) ? ForumIdentityParts.DisplayName(member) : actorName.Trim();
        var now = _clock.GetUtcNow().UtcDateTime;
        try
        {
            await _tx.RunAsync(async token =>
            {
                foreach (var messageId in missing)
                    await _reads.AddAsync(new ForumMessageRead
                    {
                        MessageId = messageId,
                        UserId = actorUserId,
                        UserName = name,
                        ReadAtUtc = now,
                    }, token);
                await _uow.SaveChangesAsync(token);
            }, ct);
        }
        catch (Exception ex) when (_errors.IsUniqueViolation(ex))
        {
            // سباق توثيق متزامن (فتح مزدوج/إعادة إرسال): فحص التفرّد خارج المعاملة
            // قد يتجاوزه إدراج متزامن بالصف نفسه — والقيد الفريد هو الحارس الأخير،
            // والصف المطلوب موجود فعلًا فالنتيجة متحققة (نجاح لا 500).
        }

        return upToMessageId;
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
    /// عضوية المنتدى من حقيقة القاعدة (لا من ادعاءات الرمز): الخمسة الداخلية فقط —
    /// المندوب والمعطَّل والغائب مرفوضون بنيويًا (فوق حارس البوابة وسمات التفويض).
    /// رئيس الشعبة بلا شعبة جلسة مكسورة (`null` تعني خللًا لا ملك القسم هنا).
    /// </summary>
    private async Task<User> RequireForumMemberAsync(int actorUserId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(actorUserId, ct)
            ?? throw new UnauthorizedAccessException("الحساب غير موجود");
        if (!user.IsActive || user.Role == UserRole.EntityManager)
            throw new UnauthorizedAccessException("لا تملك صلاحية المنتدى");
        if (user.Role == UserRole.SubHead && user.SectionId is null)
            throw new UnauthorizedAccessException("حساب رئيس الشعبة بلا شعبة — أعد الدخول");
        return user;
    }

    private static string RequireBody(string? body)
    {
        var trimmed = body?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("نص الرسالة مطلوب");
        if (trimmed.Length > MaxBodyLength)
            throw new ArgumentException($"نص الرسالة يتجاوز الحد الأقصى ({MaxBodyLength} حرفًا)");
        return trimmed;
    }

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

    /// <summary>
    /// تحقق الذِكر وإعادة صياغته القانونية: كل رمز `@[id:…]` يُطابَق مع حساب
    /// قائم نشط (والا `400`)، والسقف عشرة مميزين، ثم يُعاد توليد الرموز بالاسم
    /// الرسمي الحالي (فلا يُزوَّر اسم لمعرف ولا يُحقَن `HTML` — النص عادي).
    /// الشاذ من الصيغة يبقى نصًا حرفيًا بلا شريحة.
    /// </summary>
    private async Task<string> ResolveMentionsAsync(string body, CancellationToken ct)
    {
        var matches = MentionToken.Matches(body);
        if (matches.Count == 0)
            return body;

        var ids = new HashSet<int>();
        foreach (Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out var id))
                ids.Add(id);
        }
        if (ids.Count > MaxMentionsPerMessage)
            throw new ArgumentException(
                $"الذِكر محدود بـ ({MaxMentionsPerMessage}) أشخاص في الرسالة الواحدة");

        var canonical = new Dictionary<int, string>();
        foreach (var id in ids)
        {
            // رسالة موحدة للغائب والمعطّل والمندوب عمدًا: التفريق بينها يكشف
            // وجود حسابات معطّلة وأسماءها لمن يخمن المعرفات (البحث يعرض
            // النشطين الداخليين فقط)، والمندوب مستبعد من المنتدى كله.
            var user = await _users.GetByIdAsync(id, ct);
            if (user is null || !user.IsActive || user.Role == UserRole.EntityManager)
                throw new ArgumentException("مستخدم مذكور غير صالح");
            canonical[id] = ForumIdentityParts.DisplayName(user);
        }

        var rewritten = MentionToken.Replace(body, match =>
            int.TryParse(match.Groups[1].Value, out var id) && canonical.TryGetValue(id, out var name)
                ? "@[" + id + ":" + name + "]"
                : match.Value);
        if (rewritten.Length > MaxBodyLength)
            throw new ArgumentException($"نص الرسالة يتجاوز الحد الأقصى ({MaxBodyLength} حرفًا) بعد صياغة الذِكر");
        return rewritten;
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value.Substring(0, max);

    private static ForumMessageDto ToDto(ForumMessage m, int readCount) => new(
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
        readCount);
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
