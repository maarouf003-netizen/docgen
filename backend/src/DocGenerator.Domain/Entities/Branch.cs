namespace DocGenerator.Domain.Entities;

public class Branch
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }

    /// <summary>
    /// المحافظة التابعة لها الفرع (max 100) — تُستخدم لتحديد نطاق رئيس القسم
    /// في إدارة سجل الجهات العامة (د5) وكتالوج الاقتراحات.
    /// </summary>
    public string? Governorate { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
