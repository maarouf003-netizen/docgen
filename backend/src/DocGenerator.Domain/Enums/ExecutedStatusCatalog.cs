namespace DocGenerator.Domain.Enums;

/// <summary>
/// المصدر الوحيد لحالات وضع «منفذ عليه» وتسمياتها العربية.
/// هذا الكتالوج معزول تمامًا عن نظام «طالبة تنفيذ» (ExecutionStatusCatalog):
/// حالاته (متداول/منفذ/مشطوب) مستقلة ولا تتداخل مع حالة التنفيذ القديمة.
/// «متداول» لا يُخزَّن كقيمة (سلسلة فارغة = لا حالة) فلا يُنشأ له سجل حالة.
/// </summary>
public static class ExecutedStatusCatalog
{
    /// <summary>متداول: القيمة الفارغة تدل عليه — لا يُسجَّل أي سجل حالة.</summary>
    public const string None = "";
    public const string Executed = "منفذ";
    public const string StruckOff = "مشطوب";

    /// <summary>قيمة فلتر «منفذ» في البحث عن ملفات وضع «منفذ عليه».</summary>
    public const string ExecutedFilter = Executed;

    /// <summary>قيمة فلتر «مشطوب» في البحث عن ملفات وضع «منفذ عليه».</summary>
    public const string StruckOffFilter = StruckOff;

    /// <summary>قيمة فلتر «متداول» في البحث عن ملفات وضع «منفذ عليه».</summary>
    public const string TradingFilter = "متداول";

    public static readonly IReadOnlySet<string> ValidStatuses = new HashSet<string>
    {
        None, Executed, StruckOff,
    };

    /// <summary>هل الملف مشطوب؟ المشطوب يُخفى من القوائم والتصدير ويظهر في صفحة «الملفات المشطوبة».</summary>
    public static bool IsStruckOff(string? status) => status == StruckOff;

    /// <summary>هل الحالة حالة قابلة للتخزين الفعلي في قاعدة البيانات؟</summary>
    public static bool IsStored(string status) => status == Executed || status == StruckOff;

    public static string ToLabel(string status) => status switch
    {
        Executed => Executed,
        StruckOff => StruckOff,
        _ => TradingFilter,
    };
}
