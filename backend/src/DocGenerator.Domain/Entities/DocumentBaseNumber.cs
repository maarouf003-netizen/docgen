namespace DocGenerator.Domain.Entities;

/// <summary>
/// رقم أساس ملف التنفيذ لسنة معينة — نتيجة «تدوير أرقام الملفات» السنوي في دوائر التنفيذ.
/// سجلات متعددة لكل (ملف، سنة) بفهرس غير فريد: كل تدوير/تجديد يُنشئ سجلًا جديدًا،
/// فيُحفظ تاريخ أرقام الأساس لكل السنوات والأحداث، والمعتبر هو الأحدث (Year ثم CreatedAt).
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
