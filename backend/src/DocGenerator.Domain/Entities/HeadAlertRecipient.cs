namespace DocGenerator.Domain.Entities;

/// <summary>
/// سجل استلام تنبيه رئيس القسم لمحامٍ معين، مع حالة القراءة المستقلة لكل محامٍ
/// (نفس التنبيه يمكن أن يكون مقروءاً عند محامٍ وغير مقروء عند آخر).
/// </summary>
public class HeadAlertRecipient
{
    public int Id { get; set; }
    public int HeadAlertId { get; set; }
    public int UserId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    public HeadAlert? HeadAlert { get; set; }
    public User? User { get; set; }
}
