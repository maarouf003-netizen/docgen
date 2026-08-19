namespace DocGenerator.Domain.Enums;

/// <summary>
/// المصدر الوحيد لحالات دورة حياة الإنابة (DocumentDelegation) وتسمياتها العربية.
/// يتبع الملف المناب حالة الملف المنيب قبل البيع فقط، وبعد البيع تعتبر الإنابة منفذة.
/// </summary>
public static class DelegationStatusCatalog
{
    /// <summary>بانتظار رئيس القسم: بعد تسطير المحامي للإنابة وقبل اختيار المحامي المختص.</summary>
    public const string PendingHead = "بانتظار رئيس القسم";

    /// <summary>محالة لمحامٍ: اختار رئيس القسم المحامي وأُنشئ الملف المناب تلقائيًا.</summary>
    public const string Assigned = "محالة";

    /// <summary>مسجلة أصولًا: أدخل محامي الفرع المناب رقم أساس الإنابة وتاريخ قيدها.</summary>
    public const string Registered = "مسجلة أصولًا";

    /// <summary>منفذ إنابة: أُتم بيع الأموال موضوع الإنابة بالمزاد وأُعيد الملف للدائرة المنيبة.</summary>
    public const string Executed = "منفذ إنابة";

    public static readonly IReadOnlySet<string> ValidStatuses = new HashSet<string>
    {
        PendingHead, Assigned, Registered, Executed,
    };
}
