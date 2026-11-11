namespace DocGenerator.Domain.Entities;

public class ExecutionAction
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public string Type { get; set; } = "action";
    public string Text { get; set; } = string.Empty;
    public string? ActionDate { get; set; }
    public string? ReminderDuration { get; set; }
    public string? ReminderColor { get; set; }
    /// <summary>
    /// تاريخ أساس التذكير (ليس تاريخ الإجراء): يُضبط مرة واحدة عند إنشاء التذكير من تاريخ
    /// الإجراء المجلول، ويعدّله «نقل التذكير» وحده. تاريخ الإجراء واقعة قانونية لا تُشتق
    /// ولا تُعاد كتابتها من أي مسار تذكير (`I1`/`I2`/`I6`)؛ و`null` يعني السقوط الديناميكي
    /// إلى `ActionDate` للصفوف القديمة.
    /// </summary>
    public DateTime? ReminderBaseDate { get; set; }
    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Document Document { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}
