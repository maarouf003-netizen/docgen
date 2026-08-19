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

    /// <summary>
    /// حالة الملف المناب عند إتمام الإنابة: بيع الأموال موضوع الإنابة بالمزاد العلني
    /// وإعادة الملف إلى الدائرة المنيبة. حالة نهائية تُعامل «منفذًا» في القوائم والإحصاءات،
    /// ولا تُختار عبر آلة الحالات العادية (يضبطها مسار إتمام الإنابة حصرًا).
    /// </summary>
    public const string DelegationExecuted = "منفذ إنابة";

    public const string SubPartiallyExecuted = "منفذ جزئيا";
    public const string SubFullyExecuted = "منفذ كاملا";

    /// <summary>حالة «مشطوب» في نظام «طالبة تنفيذ» (موحّدة مع صفحة «الملفات المشطوبة»).</summary>
    public const string StateStruckOff = "مشطوب";

    /// <summary>الحالة «متداول» في آلة الحالات (حالة ملف مقيد بلا حالة تغيير).</summary>
    public const string StateCirculating = "متداول";

    /// <summary>قيمة فلتر "منفذ" في البحث — تغطي التنفيذ الجبري والتنفيذ بالتسوية.</summary>
    public const string ExecutedFilter = "منفذ";

    /// <summary>قيمة فلتر "تحت رفع" في البحث.</summary>
    public const string DraftFilter = "تحت رفع";

    public static readonly IReadOnlySet<string> ValidStatuses = new HashSet<string>
    {
        None, ExecutedForcibly, ExecutedBySettlement, Deferred, DelegationExecuted,
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
        DelegationExecuted => ExecutionStatus.DelegationExecuted,
        _ => ExecutionStatus.None,
    };

    /// <summary>
    /// هل الملف منفَّذ وانتهى (لا يدور بعده ولا يُدوَّر)؟ يشمل التسوية والتنفيذ الجبري
    /// الكامل، وحالة «منفذ إنابة» للملف المناب. أما «منفذ جبريا / منفذ جزئيا» فما زال
    /// متداولًا ويخضع لمنطق المتداول.
    /// </summary>
    public static bool IsExecuted(string? status, string? subStatus) =>
        status == ExecutedBySettlement
        || status == DelegationExecuted
        || (status == ExecutedForcibly && subStatus != SubPartiallyExecuted);

    public static string ToLabel(ExecutionStatus status) => status switch
    {
        ExecutionStatus.ExecutedForcibly => ExecutedForcibly,
        ExecutionStatus.ExecutedBySettlement => ExecutedBySettlement,
        ExecutionStatus.Deferred => Deferred,
        ExecutionStatus.DelegationExecuted => DelegationExecuted,
        _ => None,
    };

    /// <summary>الحالة الحالية للملف (نظام «طالبة تنفيذ») لآلة الحالات.</summary>
    public static string CurrentState(bool isDraft, string? status, string? executedStatus)
    {
        if (executedStatus == ExecutedStatusCatalog.StruckOff || status == StateStruckOff)
            return StateStruckOff;
        if (status == DelegationExecuted) return DelegationExecuted;
        if (status == Deferred) return Deferred;
        if (status == ExecutedBySettlement) return ExecutedBySettlement;
        if (status == ExecutedForcibly) return ExecutedForcibly;
        return isDraft ? DraftFilter : StateCirculating;
    }

    /// <summary>
    /// الانتقالات المسموحة من الحالة الحالية عبر «تغيير الحالة» (تريث/منفذ بالتسوية/منفذ جبريا/مشطوب).
    /// «تحت رفع → متداول» يتم بتسجيل رقم الملف في التعديل (المنطق القائم) وليس عبر تغيير الحالة،
    /// و«التراجع إلى متداول» من تريث/المنفذين إجراء مستقل (Revert) بحقوله الخاصة.
    /// </summary>
    public static IReadOnlySet<string> AllowedStatusChanges(string currentState) => currentState switch
    {
        DraftFilter => new HashSet<string> { Deferred, ExecutedBySettlement },
        StateCirculating => new HashSet<string> { Deferred, ExecutedBySettlement, ExecutedForcibly, StateStruckOff },
        Deferred => new HashSet<string> { ExecutedBySettlement },
        ExecutedBySettlement => new HashSet<string>(),
        ExecutedForcibly => new HashSet<string>(),
        StateStruckOff => new HashSet<string>(),
        _ => new HashSet<string>(),
    };

    public static bool IsAllowedStatusChange(string currentState, string target) =>
        AllowedStatusChanges(currentState).Contains(target);

    /// <summary>هل يجوز «التراجع» (إعادة إلى متداول بحقوقه) من الحالة الحالية؟</summary>
    public static bool CanRevert(string currentState) =>
        currentState == Deferred || currentState == ExecutedBySettlement || currentState == ExecutedForcibly;

    /// <summary>تسمية الحالة الحالية في رسائل التحقق (تُقرأ من قيم آلة الحالات).</summary>
    public static string ToStateLabel(string state) => state switch
    {
        DraftFilter => DraftFilter,
        StateCirculating => StateCirculating,
        Deferred => Deferred,
        ExecutedBySettlement => ExecutedBySettlement,
        ExecutedForcibly => ExecutedForcibly,
        DelegationExecuted => DelegationExecuted,
        StateStruckOff => StateStruckOff,
        _ => StateCirculating,
    };

    /// <summary>تسمية الحالة المستهدفة في رسائل التحقق.</summary>
    public static string ToStatusLabel(string status) => status switch
    {
        ExecutedForcibly => ExecutedForcibly,
        ExecutedBySettlement => ExecutedBySettlement,
        Deferred => Deferred,
        DelegationExecuted => DelegationExecuted,
        StateStruckOff => StateStruckOff,
        _ => StateCirculating,
    };
}
