namespace DocGenerator.Domain.Entities;

/// <summary>
/// إجراء أو ملاحظة على الاستئناف: قائمة مستقلة تمامًا عن إجراءات الملف الأساس،
/// يدخلها المحامي المتابع للاستئناف، وتدعم تذكيرًا بموعد (مدة + لون) يظهر ضمن
/// بطاقة التذكيرات في لوحة المحامي المتابع.
/// </summary>
public class AppealAction
{
    public int Id { get; set; }
    public int AppealId { get; set; }
    public string Type { get; set; } = "action";
    public string Text { get; set; } = string.Empty;
    /// <summary>تاريخ الإجراء (نص حر يُفسَّر عند العرض والتذكير).</summary>
    public string? ActionDate { get; set; }
    /// <summary>مدة التذكير (3 أيام / أسبوع / أسبوعين / شهر).</summary>
    public string? ReminderDuration { get; set; }
    /// <summary>لون شارة التذكير.</summary>
    public string? ReminderColor { get; set; }
    /// <summary>
    /// تاريخ أساس التذكير (ليس تاريخ الإجراء): يُضبط مرة عند إنشاء التذكير من تاريخ الإجراء
    /// المجلول، ويعدّله «نقل التذكير» وحده — فلا تكتب أي عملية تذكير في `ActionDate`.
    /// </summary>
    public DateTime? ReminderBaseDate { get; set; }
    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DocumentAppeal Appeal { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}
