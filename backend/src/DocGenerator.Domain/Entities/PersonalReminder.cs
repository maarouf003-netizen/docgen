namespace DocGenerator.Domain.Entities;

/// <summary>
/// تذكير شخصي حر للمحامي — بلا ملف (التذكير المرتبط بملف/استئناف حقلان على الإجراء).
/// التكرار يُوسَّع عرضيًا فقط في الواجهة؛ الإنجاز أرشفة (`IsArchived`) أو تعليم
/// تكرارات منجزة (`CompletedOccurrenceKeys`) — لا حذف صامت للسلسلة.
/// </summary>
public class PersonalReminder
{
    public int Id { get; set; }
    public int LawyerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime DueDate { get; set; }
    public string? Color { get; set; }
    public string Recurrence { get; set; } = Enums.PersonalReminderCatalog.RecurrenceOnce;
    public DateTime? RecurrenceEnd { get; set; }
    public bool IsArchived { get; set; }
    /// <summary>مفاتيح `yyyy-MM-dd` للتكرارات المنجزة، مفصولة بفواصل — تُخفى من التقويم.</summary>
    public string CompletedOccurrenceKeys { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? Lawyer { get; set; }
}
