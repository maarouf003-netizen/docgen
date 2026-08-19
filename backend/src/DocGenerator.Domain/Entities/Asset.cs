namespace DocGenerator.Domain.Entities;

/// <summary>
/// مال مرهون (منقول أو غير منقول) ضمن الملف. النوع (AssetKind) يحدد الحقول المعنية:
/// عقار / مركبة / متجر / كفالة رواتب / متجر غير مسجل. الملاك قائمة أسماء (واحد أو أكثر)
/// بترتيب الاختيار، وتُستخدم في النصوص والتوليد مجمعةً.
/// </summary>
public class Asset
{
    public int Id { get; set; }
    public int DocumentId { get; set; }

    /// <summary>نوع الأصل: عقار / مركبة / متجر / كفالة رواتب / متجر غير مسجل (AssetKindCatalog).</summary>
    public string AssetKind { get; set; } = string.Empty;

    /// <summary>
    /// مقدار الحصة: «تمام العقار/المركبة/المتجر» أو «حصة سهمية» للأنواع ذات الحصة،
    /// ويبقى بلا قيمة لكفالة الرواتب والمتجر غير المسجل.
    /// </summary>
    public string? ShareType { get; set; }

    // ── حقول العقار ──
    public string? Property { get; set; }
    public string? PropertyNumber { get; set; }
    public string? PropertyDistrict { get; set; }
    public string? LandRegistry { get; set; }

    // ── حقول المركبة ──
    public string? VehicleType { get; set; }
    public string? VehicleClass { get; set; }
    public string? PlateNumber { get; set; }
    public string? VehicleGovernorate { get; set; }

    // ── حقول المتجر (المسجل) ──
    public string? RegisterNumber { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public string? ShopGovernorate { get; set; }
    public string? ShopDescription { get; set; }
    public string? ShopLocation { get; set; }

    // ── حقول كفالة الرواتب ──
    /// <summary>الجهة العامة التي يعمل لديها صاحب الراتب.</summary>
    public string? PublicEntity { get; set; }

    // ── حقول المتجر غير المسجل ──
    public string? LicenseNumber { get; set; }
    public DateTime? LicenseDate { get; set; }
    public string? LicenseIssuer { get; set; }

    /// <summary>ملاحظات (كفالة الرواتب والمتجر غير المسجل).</summary>
    public string? Notes { get; set; }

    public ICollection<AssetOwner> Owners { get; set; } = new List<AssetOwner>();

    public Document Document { get; set; } = null!;
}
