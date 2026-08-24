namespace DocGenerator.Domain.Entities;

/// <summary>
/// اسم كتابي بديل لقيد الجهة (من الاستيراد التاريخي أو إضافة إدارية) يُستخدم
/// في البحث لجمع الكتابات المتشابهة تحت القيد نفسه.
/// </summary>
public class PublicEntityAlias
{
    public int Id { get; set; }

    public int PublicEntityId { get; set; }

    /// <summary>الاسم البديل (max 500) — مفهرس للبحث.</summary>
    public string AliasText { get; set; } = string.Empty;

    public PublicEntity PublicEntity { get; set; } = null!;
}
