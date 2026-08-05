namespace DocGenerator.Domain.Entities;

public class LoginAttempt
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public DateTime AttemptedAtUtc { get; set; } = DateTime.UtcNow;
}
