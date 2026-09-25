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

    /// <summary>
    /// حالة «محال الى البداية» في نظام «طالبة تنفيذ»: أُحيل الملف إلى قسم البداية لعدم وجود
    /// أموال للتنفيذ عليها، بكتابَي المطالعة والإحالة، ويبقى ظاهرًا في القوائم والإحصاءات
    /// وعدّاده مستقل، ولا يدخل بما يُسجّل عليه إنابة سارية، ويخرج عبر نقطة العودة المخصصة
    /// (إلى متداول أو منفذ جزئيًا حسب مصدر دخوله).
    /// </summary>
    public const string ReferredToStart = "محال الى البداية";

    /// <summary>حالة «مشطوب» في نظام «طالبة تنفيذ» (موحّدة مع صفحة «الملفات المشطوبة»).</summary>
    public const string StateStruckOff = "مشطوب";

    /// <summary>الحالة «متداول» في آلة الحالات (حالة ملف مقيد بلا حالة تغيير).</summary>
    public const string StateCirculating = "متداول";

    /// <summary>
    /// حالة «مسترد» للملف المناب عند إعادة الملف إلى الدائرة المنيبة: سجّل الفرع المسترد
    /// الوقعة بحقولها كاملة في سجله، وأصبح ملف الإنابة منتهيًا ولا يُعاد تسطير حالته
    /// عليه بعد الآن. حالة نهائية تُعامل «منفذًا» في القوائم والإحصاءات، ولا تُختار عبر
    /// آلة الحالات العادية ولا عبر «الاسترداد» (يضبطها مسار استرداد الإنابة حصرًا، ولا
    /// يُسمح بالانتقال منها إلى أي حالة).
    /// </summary>
    public const string Recovered = "مسترد";

    /// <summary>قيمة فلتر "منفذ" في البحث — تغطي التنفيذ الجبري والتنفيذ بالتسوية.</summary>
    public const string ExecutedFilter = "منفذ";

    /// <summary>قيمة فلتر "تحت رفع" في البحث.</summary>
    public const string DraftFilter = "تحت رفع";

    public static readonly IReadOnlySet<string> ValidStatuses = new HashSet<string>
    {
        None, ExecutedForcibly, ExecutedBySettlement, Deferred, DelegationExecuted, Recovered,
        ReferredToStart,
    };

    public static readonly IReadOnlySet<string> ValidSubStatuses = new HashSet<string>
    {
        SubPartiallyExecuted, SubFullyExecuted,
    };

    /// <summary>
    /// قيم فلتر «الحالة» المقبولة في البحث/التصدير/خيارات الفلاتر — أي قيمة خارجها تُرفض
    /// (400) بدل السقوط الصامت في فرع «متداول». الفارغ/الأبيض يعني «بلا فلتر» لا قيمة مرفوضة.
    /// </summary>
    public static readonly IReadOnlySet<string> ValidSearchFilters = new HashSet<string>
    {
        None, StateCirculating, ExecutedFilter, Deferred, DraftFilter,
    };

    public static bool IsValidSearchFilter(string? status) =>
        string.IsNullOrWhiteSpace(status) || ValidSearchFilters.Contains(status.Trim());

    /// <summary>
    /// قيم فلتر «الحالة» في بوابة المندوب: الخمسة المشتركة + «محال الى البداية» الذي
    /// تخفيه القائمة الرئيسية للمحامين في صفحة مستقلة بينما البوابة تعرضه فلترًا —
    /// المصدر الوحيد للحقيقة في البوابة (تستهلكه `PortalRepository.ScopedQuery`) فلا
    /// تُبنى مجموعة حرفيات يدوية في أي موضع آخر.
    /// </summary>
    public static readonly IReadOnlySet<string> PortalSearchFilters = new HashSet<string>
    {
        None, StateCirculating, ExecutedFilter, Deferred, DraftFilter, ReferredToStart,
    };

    public static bool IsValidPortalFilter(string? status) =>
        string.IsNullOrWhiteSpace(status) || PortalSearchFilters.Contains(status.Trim());

    public static ExecutionStatus Classify(string status) => status switch
    {
        ExecutedForcibly => ExecutionStatus.ExecutedForcibly,
        ExecutedBySettlement => ExecutionStatus.ExecutedBySettlement,
        Deferred => ExecutionStatus.Deferred,
        DelegationExecuted => ExecutionStatus.DelegationExecuted,
        Recovered => ExecutionStatus.Recovered,
        ReferredToStart => ExecutionStatus.ReferredToStart,
        _ => ExecutionStatus.None,
    };

    /// <summary>
    /// هل الملف منفَّذ وانتهى (لا يدور بعده ولا يُدوَّر)؟ يشمل التسوية والتنفيذ الجبري
    /// الكامل، وحالة «منفذ إنابة» للملف المناب. أما «منفذ جبريا / منفذ جزئيا» فما زال
    /// متداولًا ويخضع لمنطق المتداول، و«محال الى البداية» حالة محايدة تعود إلى السير
    /// بمجرد موافرة أموال، فهي غير منفذة في القوائم والإحصاءات والتدوير مهما حملّت
    /// جزئيته. تُضاف حالة «مسترد» (المناب المسترد إلى دائرة المنيب) إلى «منفذ» لكونها
    /// حالة نهائية لا تتأثر بمسارات النقل والمعالجات بل تظهر منفذةً.
    /// </summary>
    public static bool IsExecuted(string? status, string? subStatus) =>
        status == ExecutedBySettlement
        || status == DelegationExecuted
        || status == Recovered
        || (status == ExecutedForcibly && subStatus != SubPartiallyExecuted);

    public static string ToLabel(ExecutionStatus status) => status switch
    {
        ExecutionStatus.ExecutedForcibly => ExecutedForcibly,
        ExecutionStatus.ExecutedBySettlement => ExecutedBySettlement,
        ExecutionStatus.Deferred => Deferred,
        ExecutionStatus.DelegationExecuted => DelegationExecuted,
        ExecutionStatus.Recovered => Recovered,
        ExecutionStatus.ReferredToStart => ReferredToStart,
        _ => None,
    };

    /// <summary>الحالة الحالية للملف (نظام «طالبة تنفيذ») لآلة الحالات.</summary>
    public static string CurrentState(bool isDraft, string? status, string? executedStatus)
    {
        if (executedStatus == ExecutedStatusCatalog.StruckOff || status == StateStruckOff)
            return StateStruckOff;
        if (status == DelegationExecuted) return DelegationExecuted;
        if (status == Recovered) return Recovered;
        if (status == Deferred) return Deferred;
        if (status == ExecutedBySettlement) return ExecutedBySettlement;
        if (status == ExecutedForcibly) return ExecutedForcibly;
        if (status == ReferredToStart) return ReferredToStart;
        return isDraft ? DraftFilter : StateCirculating;
    }

    /// <summary>
    /// الانتقالات المسموحة من الحالة الحالية عبر «تغيير الحالة» (تريث/منفذ بالتسوية/منفذ جبريا/
    /// مشطوب). تُلحق «محال الى البداية» كهدف من متداول وتريث ومنفذ جبريا — ومن منفذ جبريا
    /// يُشترط أن يكون منفذًا جزئيًا (القرار 1: دون «منفذ كاملا»). «تحت رفع → متداول» يتم
    /// بتسجيل رقم الملف في التعديل (المنطق القائم) وليس عبر تغيير الحالة، و«التراجع إلى
    /// متداول» من تريث/المنفذين إجراء مستقل (Revert) بحقوله الخاصة، و«محال الى البداية»
    /// بلا مخارج عبر هذه الآلة (خروجه عبر نقطة العودة المخصصة).
    /// </summary>
    public static IReadOnlySet<string> AllowedStatusChanges(string currentState) => currentState switch
    {
        DraftFilter => new HashSet<string> { Deferred, ExecutedBySettlement },
        StateCirculating => new HashSet<string> { Deferred, ExecutedBySettlement, ExecutedForcibly, StateStruckOff, ReferredToStart },
        Deferred => new HashSet<string> { ExecutedBySettlement, ReferredToStart },
        ExecutedBySettlement => new HashSet<string>(),
        ExecutedForcibly => new HashSet<string> { ReferredToStart },
        ReferredToStart => new HashSet<string>(),
        StateStruckOff => new HashSet<string>(),
        Recovered => new HashSet<string>(),
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
        ReferredToStart => ReferredToStart,
        DelegationExecuted => DelegationExecuted,
        Recovered => Recovered,
        StateStruckOff => StateStruckOff,
        _ => StateCirculating,
    };

    /// <summary>تسمية الحالة المستهدفة في رسائل التحقق.</summary>
    public static string ToStatusLabel(string status) => status switch
    {
        ExecutedForcibly => ExecutedForcibly,
        ExecutedBySettlement => ExecutedBySettlement,
        Deferred => Deferred,
        ReferredToStart => ReferredToStart,
        DelegationExecuted => DelegationExecuted,
        Recovered => Recovered,
        StateStruckOff => StateStruckOff,
        _ => StateCirculating,
    };
}
