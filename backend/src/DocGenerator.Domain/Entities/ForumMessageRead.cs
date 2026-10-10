namespace DocGenerator.Domain.Entities;

/// <summary>
/// توثيق اطلاع مستخدم على رسالة منتدى: يُنشأ ضمنيًا عند فتح التيار (لا زر تأكيد
/// صريح — لذا تُسمّى النافذة «شوهدت بواسطة» لا «أكّد المشاهدة»)، مرة واحدة لكل
/// (رسالة، مستخدم)، ويُستخدم لاشتقاق العدّاد (رسائل بلا إيصال) وعلامتي ✓✓.
/// يُحذف مع رسالته (حذف صلب متتالٍ) فلا يبقى يتيمًا أبدًا.
/// </summary>
public class ForumMessageRead
{
    public int Id { get; set; }

    public int MessageId { get; set; }

    public int UserId { get; set; }

    /// <summary>اسم المطلع لحظة التوثيق (لقطة للسجل).</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>لحظة التوثيق (UTC).</summary>
    public DateTime ReadAtUtc { get; set; } = DateTime.UtcNow;

    public ForumMessage Message { get; set; } = null!;
}
