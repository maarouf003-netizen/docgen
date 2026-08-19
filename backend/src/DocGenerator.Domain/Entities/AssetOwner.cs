namespace DocGenerator.Domain.Entities;

/// <summary>
/// مالك من قائمة ملاك الأصل (واحد أو أكثر). يحفظ اسم المالك وترتيبه ضمن القائمة،
/// ويُعاد إنشاء السجلات عند كل تعديل على المستند مع الحفاظ على الترتيب المختار.
/// </summary>
public class AssetOwner
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }

    public Asset Asset { get; set; } = null!;
}
