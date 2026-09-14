namespace DocGenerator.Domain.Entities;

/// <summary>
/// رقم أساس استئنافي لسنة معينة — نتيجة «تدوير أرقام الأساس الاستئنافية» السنوي أمام
/// محكمة الاستئناف: سجلات متعددة لكل (استئناف، سنة) بفهرس غير فريد، فيُحفظ تاريخ
/// الأرقام لكل السنوات والأحداث، والمعتبر هو الأحدث (Year ثم CreatedAt).
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
