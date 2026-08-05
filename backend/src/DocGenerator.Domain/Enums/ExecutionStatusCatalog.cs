namespace DocGenerator.Domain.Enums;

/// <summary>
/// المصدر الوحيد لحقيقة حالات التنفيذ وتسمياتها العربية.
/// تُخزَّن القيم في قاعدة البيانات وتُعرَّض للواجهة كنصوص عربية (توافق مع البيانات القائمة)،
/// ويُحصر تعريف هذه النصوص في هذا الكتالوج بدل تكرارها في الخدمة والمستودعات.
/// </summary>
public static class ExecutionStatusCatalog
{
    public const string None = "";
    public const string ExecutedForcibly = "منفذ جبريا";
    public const string ExecutedBySettlement = "منفذ بالتسوية";
    public const string Deferred = "تريث";

    public const string SubPartiallyExecuted = "منفذ جزئيا";
    public const string SubFullyExecuted = "منفذ كاملا";

    /// <summary>قيمة فلتر "منفذ" في البحث — تغطي التنفيذ الجبري والتنفيذ بالتسوية.</summary>
    public const string ExecutedFilter = "منفذ";

    /// <summary>قيمة فلتر "تحت رفع" في البحث.</summary>
    public const string DraftFilter = "تحت رفع";

    public static readonly IReadOnlySet<string> ValidStatuses = new HashSet<string>
    {
        None, ExecutedForcibly, ExecutedBySettlement, Deferred,
    };

    public static readonly IReadOnlySet<string> ValidSubStatuses = new HashSet<string>
    {
        SubPartiallyExecuted, SubFullyExecuted,
    };

    public static ExecutionStatus Classify(string status) => status switch
    {
        ExecutedForcibly => ExecutionStatus.ExecutedForcibly,
        ExecutedBySettlement => ExecutionStatus.ExecutedBySettlement,
        Deferred => ExecutionStatus.Deferred,
        _ => ExecutionStatus.None,
    };

    public static string ToLabel(ExecutionStatus status) => status switch
    {
        ExecutionStatus.ExecutedForcibly => ExecutedForcibly,
        ExecutionStatus.ExecutedBySettlement => ExecutedBySettlement,
        ExecutionStatus.Deferred => Deferred,
        _ => None,
    };
}
