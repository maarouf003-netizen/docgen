namespace DocGenerator.Domain.Enums;

/// <summary>
/// المصدر الوحيد لحالات دورة حياة الاستئناف (DocumentAppeal.Status) وتسمياتها العربية:
/// منظور (قيد النظر أمام محكمة الاستئناف)، محسوم (صدر قرار الحسم)، مشطوب (شوط الاستئناف).
/// يُخزَّن النوع بالإنكليزية في القاعدة لتجنب اعتماد البحث والتصفية على النصوص العربية.
/// </summary>
public static class AppealStatusCatalog
{
    /// <summary>منظور: قيد النظر لدى محكمة الاستئناف المدنية الغرفة الناظرة بالقضايا التنفيذية.</summary>
    public const string Pending = "pending";

    /// <summary>محسوم: صدر قرار الحسم برقمه وتاريخه ومنطوقه ونتيجته (للصالح/للضد).</summary>
    public const string Decided = "decided";

    /// <summary>مشطوب: شوط الاستئناف بقرار شطب برقمه وتاريخه.</summary>
    public const string StruckOff = "struck-off";

    /// <summary>
    /// ترتيب ثابت لقيد `CK_DocumentAppeals_Status` — مطابق للقطة الحالية.
    /// </summary>
    public static readonly IReadOnlyList<string> OrderedStatuses = new[]
    {
        Pending, Decided, StruckOff,
    };

    public static readonly IReadOnlySet<string> ValidStatuses =
        new HashSet<string>(OrderedStatuses);

    /// <summary>التسمية العربية للحالة — المجهول يُعرض خامه لا «منظور» مخترعة.</summary>
    public static string ToLabel(string status) => status switch
    {
        Pending => "منظور",
        Decided => "محسوم",
        StruckOff => "مشطوب",
        _ => status,
    };
}
