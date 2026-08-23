namespace DocGenerator.Domain.Enums;

/// <summary>
/// المصدر الوحيد لاتجاه الاستئناف (DocumentAppeal.Direction) وتسمياته العربية:
/// مستأنِفين (نحن من تقدم بالاستئناف) أو مستأنف علينا (أحد أطراف الملف الآخرين هو المستأنف).
/// يُخزَّن النوع بالإنكليزية في القاعدة لتجنب اعتماد البحث والتصفية على النصوص العربية.
/// </summary>
public static class AppealDirectionCatalog
{
    /// <summary>مستأنِفين: نحن (جهة/جهات عامة طالبة التنفيذ) من استأنف قرار رئيس التنفيذ.</summary>
    public const string Appellants = "appellants";

    /// <summary>مستأنف علينا: أحد المنفذ عليهم استأنف، ونحن نتابعه أصولًا.</summary>
    public const string AgainstUs = "against-us";

    public static readonly IReadOnlySet<string> ValidDirections = new HashSet<string>
    {
        Appellants, AgainstUs,
    };

    public static string ToLabel(string direction) => direction switch
    {
        Appellants => "مستأنِفين",
        AgainstUs => "مستأنف علينا",
        _ => Appellants,
    };
}
