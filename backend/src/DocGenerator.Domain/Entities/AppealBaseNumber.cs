namespace DocGenerator.Domain.Entities;

/// <summary>
/// رقم أساس استئنافي لسنة معينة — نتيجة «تدوير أرقام الأساس الاستئنافية» السنوي أمام
/// محكمة الاستئناف: سجل واحد لكل (استئناف، سنة) بفهرس فريد، فيُحفظ تاريخ الأرقام
/// لكل السنوات السابقة ويُسمح بتحديث رقم السنة الحالية دون فقدان السابق.
/// </summary>
public class AppealBaseNumber
{
    public int Id { get; set; }
    public int AppealId { get; set; }
    public int Year { get; set; }
    public string BaseNumber { get; set; } = string.Empty;
    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DocumentAppeal Appeal { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}
