namespace DocGenerator.Domain.Entities;

/// <summary>
/// حدث تغيير على قيد أو هوية أم في سجل الجهات: إعادة تسمية، نقل، دمج، إلغاء، إنشاء، مراجعة، استيراد.
/// يُكتب داخل المعاملة نفسها للعملية، ويُستخدم لشاشة مراقبة الإدارة ووقوعات الملفات الآلية والتنبيهات.
/// </summary>
public class PublicEntityChangeEvent
{
    public int Id { get; set; }

    /// <summary>القيد المتأثر (FK اختياري) — يُفكّ بحذف القيد (SetNull).</summary>
    public int? EntryId { get; set; }

    /// <summary>الهوية الأم المتأثرة (FK اختياري) — يُفكّ بحذف الهوية (SetNull).</summary>
    public int? GroupId { get; set; }

    /// <summary>نوع الإجراء: rename / move / merge / abolish / create / review / import.</summary>
    public string ActionKind { get; set; } = string.Empty;

    /// <summary>نوع المرسوم (اختياري): إداري / تشريعي / قرار وزاري ...</summary>
    public string? DecreeKind { get; set; }

    /// <summary>رقم المرسوم (اختياري).</summary>
    public string? DecreeNumber { get; set; }

    /// <summary>تاريخ المرسوم (اختياري).</summary>
    public DateTime? DecreeDate { get; set; }

    /// <summary>بيانات JSON تحتوي: الحالة قبل/بعد + خريطة الفروع المنقولة.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>المستخدم الذي نفّذ العملية (Restrict عند حذف المستخدم).</summary>
    public int ActorUserId { get; set; }

    /// <summary>التوقيت بالUTC لحظة تسجيل الحدث.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>القيد المتأثر ( navegation ).</summary>
    public PublicEntity? Entry { get; set; }

    /// <summary>الهوية الأم المتأثرة ( navegation ).</summary>
    public PublicEntityGroup? Group { get; set; }

    /// <summary>الفاعل ( navegation ).</summary>
    public User ActorUser { get; set; } = null!;
}
