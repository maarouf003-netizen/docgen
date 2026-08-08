namespace DocGenerator.Domain.Entities;

/// <summary>
/// رقم أساس ملف التنفيذ لسنة معينة — نتيجة «تدوير أرقام الملفات» السنوي في دوائر التنفيذ.
/// سجل واحد لكل (ملف، سنة) بفهرس فريد، فيُحفظ تاريخ أرقام الأساس لكل السنوات السابقة
/// ويُسمح بتحديث رقم السنة الحالية فقط دون فقدان السابق.
/// </summary>
public class DocumentBaseNumber
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int Year { get; set; }
    public string BaseNumber { get; set; } = string.Empty;
    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Document Document { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}
