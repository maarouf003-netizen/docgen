namespace DocGenerator.Application.DTOs;

/// <summary>نشر رسالة منتدى: نص عادي + اقتباس اختياري لرسالة قائمة.</summary>
public record PostForumMessageRequest(string Body, int? QuotedMessageId = null);

/// <summary>تعديل رسالة منتدى (الكاتب فقط — يختم «عُدّل»).</summary>
public record UpdateForumMessageRequest(string Body);

/// <summary>تعليم القراءة حتى رسالة (عدّادي — `max` فقط بلا تراجع).</summary>
public record MarkForumReadRequest(int UpToMessageId);

/// <summary>
/// رسالة منتدى للعرض: النص + أجزاء هوية الكاتب (الاسم/الدور/الموقع/الشعبة) فيركّبها
/// العميل عبر `formatRoleScope` — لا سلسلة مركّبة من الخادم إطلاقًا (بلا ازدواج صياغة).
/// `ReadCount` لرسائل الكاتب نفسه فقط (0 لغيرها) — مصدر علامتي ✓✓.
/// </summary>
public record ForumMessageDto(
    int Id,
    string Body,
    int AuthorId,
    string AuthorName,
    string AuthorRole,
    string AuthorLocation,
    string? AuthorSection,
    bool IsPinned,
    int? QuotedMessageId,
    string? QuotedAuthorName,
    string? QuotedExcerpt,
    DateTime CreatedAt,
    int? EditedById,
    DateTime? EditedAtUtc,
    int ReadCount);

/// <summary>صفحة تيار بمفتاح `Id`: الأحدث أولًا عند غياب المؤشر، و`HasOlder` لزر «تحميل المزيد».</summary>
public record ForumMessagesPageDto(List<ForumMessageDto> Items, bool HasOlder);

/// <summary>صف نافذة «شوهدت بواسطة»: هوية القارئ ولحظة اطلاعه — للكاتب فقط.</summary>
public record ForumReaderDto(int UserId, string UserName, DateTime ReadAtUtc);
