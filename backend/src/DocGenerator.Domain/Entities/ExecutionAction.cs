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
    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Document Document { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}
