namespace DocGenerator.Domain.Entities;

/// <summary>
/// رسالة في منتدى المحامين (تيار واحد مسطّح عابر للفروع): نص عادي فقط بلا تنسيق،
/// مع لقطة هوية الكاتب لحظة الإرسال (الاسم — الدور — الموقع — الشعبة) فتصمد بعد
/// تغيير الاسم أو الفرع أو التعطيل، واقتباس اختياري لرسالة سابقة بظل لقطة.
/// الحذف صلب دائمًا (بلا حذف ناعم — قرار المالك)، والتنظيف التلقائي يحذف
/// الأقدم من مدة الاحتفاظ عدا الرسالة المثبّتة.
/// </summary>
public class ForumMessage
{
    public int Id { get; set; }

    /// <summary>نص الرسالة العادي (1..2000 حرفًا بعد القصّ — الفراغ مرفوض).</summary>
    public string Body { get; set; } = string.Empty;

    public int AuthorId { get; set; }

    /// <summary>اسم الكاتب لحظة الإرسال (لقطة — `User.FullName`).</summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    /// دور الكاتب لحظة الإرسال (لقطة — `UserRole.ToString().ToLowerInvariant()` فيطابق
    /// نوع `Role` الأمامي حرفيًا: lawyer/head/subhead/manager/admin).
    /// </summary>
    public string AuthorRole { get; set; } = "lawyer";

    /// <summary>
    /// موقع الكاتب لحظة الإرسال (لقطة — `Branch.Governorate ?? Branch.Name ?? 'كل الفروع'`).
    /// </summary>
    public string AuthorLocation { get; set; } = "كل الفروع";

    /// <summary>شعبة الكاتب لحظة الإرسال (لقطة — `Section.Name` لرئيس الشعبة فقط).</summary>
    public string? AuthorSection { get; set; }

    /// <summary>تثبيت المشرف: رسالة واحدة مثبتة في اللحظة (فهرس فريد مفلتر).</summary>
    public bool IsPinned { get; set; }

    /// <summary>الرسالة المقتبسة (`SetNull` عند حذفها — تبقى اللقطة كظل واتساب).</summary>
    public int? QuotedMessageId { get; set; }

    /// <summary>اسم كاتب الرسالة المقتبسة لحظة الاقتباس (لقطة ≤150).</summary>
    public string? QuotedAuthorName { get; set; }

    /// <summary>مقتطف نص الرسالة المقتبسة لحظة الاقتباس (لقطة ≤200).</summary>
    public string? QuotedExcerpt { get; set; }

    /// <summary>تاريخ الإرسال (UTC — مصدر العرض النسبي والكامل).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>من عدّل الرسالة (الكاتب نفسه حصرًا).</summary>
    public int? EditedById { get; set; }

    /// <summary>لحظة التعديل (UTC — تُظهر الواجهة ختم «عُدّل»).</summary>
    public DateTime? EditedAtUtc { get; set; }

    public User Author { get; set; } = null!;
    public ForumMessage? QuotedMessage { get; set; }
}
