namespace DocGenerator.Domain.Entities;

/// <summary>
/// اقتراح تطوير للتطبيق — يصل للمشرف (`admin`) فقط، والمرسل يرى سجل اقتراحاته
/// وحالة قراءتها. نص حر بلا `HTML` (يُعرض كنص).
/// </summary>
public class AppSuggestion
{
    public int Id { get; set; }
    public int SenderId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }

    public User? Sender { get; set; }
}
