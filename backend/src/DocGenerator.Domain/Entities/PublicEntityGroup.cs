namespace DocGenerator.Domain.Entities;

/// <summary>
/// الهوية الأم للجهة العامة في السجل المرجعي المركزي (وزارة/إدارة/هيئة/مؤسسة/شركة)
/// بالاسم المعتمد CanonicalName. تحتها قيود المستويين: المحافظة + الفرع.
/// </summary>
public class PublicEntityGroup
{
    public int Id { get; set; }

    /// <summary>الاسم المعتمد للجهة — فريد في السجل (max 200).</summary>
    public string CanonicalName { get; set; } = string.Empty;

    /// <summary>نوع الجهة (كتالوج نصي: ministry/administration/authority/foundation/company).</summary>
    public string EntityType { get; set; } = Enums.PublicEntityTypeCatalog.Ministry;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PublicEntity> Entries { get; set; } = new List<PublicEntity>();
}
