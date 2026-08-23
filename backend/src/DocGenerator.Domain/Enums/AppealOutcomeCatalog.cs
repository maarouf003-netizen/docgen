namespace DocGenerator.Domain.Enums;

/// <summary>
/// المصدر الوحيد لنتيجة الاستئناف المحسوم (DocumentAppeal.Outcome) وتسمياتها العربية.
/// يُخزَّن النوع بالإنكليزية في القاعدة لتجنب اعتماد البحث والتصفية على النصوص العربية.
/// </summary>
public static class AppealOutcomeCatalog
{
    /// <summary>للصالح: جاء قرار الحسم لمصلحة من نتابع الاستئناف.</summary>
    public const string InFavor = "in-favor";

    /// <summary>للضد: جاء قرار الحسم ضد من نتابع الاستئناف.</summary>
    public const string Against = "against";

    public static readonly IReadOnlySet<string> ValidOutcomes = new HashSet<string>
    {
        InFavor, Against,
    };

    public static string ToLabel(string outcome) => outcome switch
    {
        InFavor => "للصالح",
        Against => "للضد",
        _ => InFavor,
    };
}
