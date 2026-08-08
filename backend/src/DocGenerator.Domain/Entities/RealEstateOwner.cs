namespace DocGenerator.Domain.Entities;

/// <summary>
/// مالك من قائمة ملاك العقار (واحد أو أكثر). يحفظ اسم المالك وترتيبه ضمن القائمة،
/// ويُعاد إنشاء السجلات عند كل تعديل على المستند مع الحفاظ على الترتيب المختار.
/// </summary>
public class RealEstateOwner
{
    public int Id { get; set; }
    public int RealEstateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }

    public RealEstate RealEstate { get; set; } = null!;
}
