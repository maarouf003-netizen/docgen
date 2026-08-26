namespace DocGenerator.Domain.Enums;

/// <summary>
/// كتالوج المحافظات السورية المعتمد: يُستخدم في التحقق من CoverageLabel
/// (يُرفض إن طابق اسم محافظة واحدة) وإنشاء كتالوج البحث في الخلفية.
/// </summary>
public static class GovernorateCatalog
{
    public static readonly IReadOnlySet<string> Governorates = new HashSet<string>(StringComparer.Ordinal)
    {
        "دمشق",
        "حلب",
        "حمص",
        "حماة",
        "اللاذقية",
        "طرطوس",
        "إدلب",
        "دير الزور",
        "الرقة",
        "الحسكة",
        "القنيطرة",
        "السويداء",
        "درعا",
        "ريف دمشق",
    };

    /// <summary>هل القيمة (بعد trim) مطابقة لأي محافظة في الكتالوج؟</summary>
    public static bool IsGovernorate(string? name)
        => name is not null && Governorates.Contains(name.Trim());
}
