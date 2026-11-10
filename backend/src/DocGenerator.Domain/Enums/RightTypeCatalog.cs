namespace DocGenerator.Domain.Enums;

/// <summary>
/// أنواع الحق على الأصل: «حق رقبة» و«حق انتفاع». يُقتصر الحقل على العقار والمركبة
/// (قرار المالك)، ويُطبَّع الفارغ/غير الصالح إلى «حق رقبة» لهذين النوعين.
/// </summary>
public static class RightTypeCatalog
{
    public const string BareOwnership = "حق رقبة";
    public const string Usufruct = "حق انتفاع";

    public static readonly IReadOnlySet<string> ValidTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        BareOwnership, Usufruct,
    };

    /// <summary>الأنواع التي تحمل نوع الحق: العقار والمركبة فقط.</summary>
    public static readonly IReadOnlySet<string> SupportedKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        AssetKindCatalog.RealEstate, AssetKindCatalog.Vehicle,
    };

    public static bool IsSupported(string? kind) =>
        !string.IsNullOrWhiteSpace(kind) && SupportedKinds.Contains(kind.Trim());

    public static bool IsValid(string? value) => value is not null && ValidTypes.Contains(value);

    /// <summary>تطبيع نوع الحق: null للأنواع غير المدعومة، و«حق رقبة» للفارغ/غير الصالح.</summary>
    public static string? Normalize(string? kind, string? value)
    {
        if (!IsSupported(kind))
            return null;

        var trimmed = (value ?? string.Empty).Trim();
        return IsValid(trimmed) ? trimmed : BareOwnership;
    }
}
