namespace DocGenerator.Domain.Entities;

public class AuditLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? UserName { get; set; }
    public string? ActionType { get; set; }
    public int? DocumentId { get; set; }
    public string? DocumentType { get; set; }
    public string? Details { get; set; }
}
