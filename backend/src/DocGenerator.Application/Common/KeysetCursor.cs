namespace DocGenerator.Application.Common;

/// <summary>
/// مؤشر keyset للقوائم الكبيرة: نوع الترتيب + قيمة مفتاح الفرز + المعرف الفاصل.
/// يُستخدم بدل `Skip/Take` على الإزاحات العميقة، ويُرمَّز موقّعًا حتى لا يُلفَّق
/// بمؤشر قائمة أخرى (يزيد التعرّف على تلف العميل ولا يمنح وصولًا جديدًا — النطاق
/// والفلاتر تُطبَّق خادميًا دائمًا).
/// </summary>
public sealed record KeysetCursor(string Kind, DateTime? SortValue, int Id);

/// <summary>أنواع مؤشرات قوائم الملفات — تربط المؤشر بترتيب القائمة فلا يُعاد استخدامه في قائمة أخرى.</summary>
public static class DocumentListCursorKinds
{
    public const string CreatedAt = "documents.created";
    public const string DeletedAt = "documents.deleted";
    public const string StruckOffDate = "documents.struck";
    public const string UpdatedAt = "documents.executed";
    public const string StartReferralDate = "documents.referred";
    public const string ReminderLastActivity = "documents.reminders";
}

/// <summary>ترميز وفك مؤشرات keyset الموقّعة — يفصل الخدمات عن مفتاح الخادم.</summary>
public interface IKeysetCursorCodec
{
    /// <summary>يبني مؤشرًا موقّعًا من نوع القائمة وقيمة آخر صف ومعرّفه.</summary>
    string Encode(string kind, DateTime? sortValue, int id);

    /// <summary>يفك المؤشر ويتحقق من توقيعه ونوعه؛ `null` عند غيابه، و`400` عند تلفه.</summary>
    KeysetCursor? DecodeOptional(string? value, string kind);
}
