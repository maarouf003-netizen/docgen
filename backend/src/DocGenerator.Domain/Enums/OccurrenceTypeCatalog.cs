namespace DocGenerator.Domain.Enums;

/// <summary>
/// المصدر الوحيد لأنواع «وقوعات الملف» وتسمياتها العربية.
/// الوقعة سجل زمني مستقل في وضع «منفذ عليه»/«عرض وايداع»: شطب (struck-off) أو تجديد (renewal)،
/// وتمتد لتسجّل إجراءات تغيير الحالة في نظام «طالبة تنفيذ» (تريث، منفذ بالتسوية، منفذ جبريا،
/// تراجع/إلغاء) وإحالة إلى البداية (referred-to-start) واسترداد ملف الإنابة (recovered) بحقولها الكاملة في Details. يُخزَّن النوع
/// بالإنكليزية في القاعدة لتجنب اعتماد البحث والتصفية على النصوص العربية.
/// </summary>
public static class OccurrenceTypeCatalog
{
    /// <summary>شطب الملف (صفة منفذ عليها أو طالبة تنفيذ): يُخفى من القوائم والتصدير ويظهر في صفحة «الملفات المشطوبة».</summary>
    public const string StruckOff = "struck-off";

    /// <summary>تجديد/إعادة الملف: إعادة ملف مشطوب إلى المتداول برقم ملف جديد لسنة الإعادة.</summary>
    public const string Renewal = "renewal";

    /// <summary>إجراء «تريث» في نظام «طالبة تنفيذ»: توقف السير بموجب كتاب التريث.</summary>
    public const string Deferred = "deferred";

    /// <summary>إجراء «منفذ بالتسوية» في نظام «طالبة تنفيذ»: بموجب كتاب براءة الذمة.</summary>
    public const string Settled = "settled";

    /// <summary>إجراء «منفذ جبريا» في نظام «طالبة تنفيذ»: بالمزاد العلني للعقارات المباعة.</summary>
    public const string Forcible = "forcible";

    /// <summary>
    /// إجراء «تراجع/إلغاء» في نظام «طالبة تنفيذ»: إعادة الملف إلى المتداول من تريث أو من
    /// منفذ بالتسوية أو منفذ جبريا بموجب كتاب الجهة العامة بالسير بالملف.
    /// </summary>
    public const string Revert = "revert";

    /// <summary>حدث تغيير على قيد أو هوية أم في سجل الجهات (يُسجَّل آليًا فقط).</summary>
    public const string EntityChange = "entity-change";

    /// <summary>
    /// إجراء «استرداد» الملف المناب: يُسجَّل على المناب (آليًا، مصدر نظامي) عند اعتبار
    /// الملف المنيب منفذًا (تسوية/جبريا-كاملا)، وتوثّق تفاصيله سبب الاسترداد وكتاب براءة
    /// الذمة (تسوية) أو تحويل البدل (اكتمال جبري) ورقم أساس المنيب (F2).
    /// </summary>
    public const string Recovered = "recovered";

    /// <summary>
    /// إجراء «محال الى البداية» في نظام «طالبة تنفيذ»: إحالة الملف إلى قسم البداية لعدم
    /// وجود أموال للتنفيذ عليها، بحقول كتاب المطالعة وكتاب الإحالة في Details. سجّل
    /// «تغيير حالة» (يظهر في مرشح الحالات/تفاصيل البطاقة) ويُخرج منه عبر نقطة العودة
    /// المخصصة ووقعة <see cref="Revert"/>.
    /// </summary>
    public const string ReferredToStart = "referred-to-start";

    /// <summary>
    /// إحالة نقل دائرة: تفريغ الملف من الدائرة الملغاة (يحمل الرقم/النوع/السنة القديمة
    /// + FromCircuitName/ToCircuitName نصًا مجمدًا للتاريخ).
    /// </summary>
    public const string CircuitReferred = "circuit-referred";

    /// <summary>
    /// إعادة قيد بدائرة جديدة: إدخال المحامي الأرقام الجديدة (يحمل الجديدة،
    /// والقديمة تُقرأ من وقوعّة الإحالة).
    /// </summary>
    public const string CircuitReregistered = "circuit-reregistered";

    /// <summary>
    /// إعادة تسمية دائرة (§5.2): تُسجَّل على كل ملف متأثر بالتسمية (قديم/جديد
    /// في FromCircuitName/ToCircuitName) — وإلا مرّت التسمية الجماعية بلا أثر ملفي.
    /// </summary>
    public const string CircuitRenamed = "circuit-renamed";

    public static readonly IReadOnlySet<string> ValidTypes = new HashSet<string>
    {
        StruckOff, Renewal, Deferred, Settled, Forcible, Revert, EntityChange, Recovered,
        ReferredToStart, CircuitReferred, CircuitReregistered, CircuitRenamed,
    };

    /// <summary>
    /// التسمية العربية للنوع — المجهول يُعرض خامه. (الافتراض السابق إلى «شطب» كان الأخطر
    /// دلاليًا: حدث مجهول يظهر كشطب يُخفي الملف من القوائم.)
    /// </summary>
    public static string ToLabel(string type) => type switch
    {
        StruckOff => "شطب",
        Renewal => "تجديد",
        Deferred => "تريث",
        Settled => "منفذ بالتسوية",
        Forcible => "منفذ جبريا",
        Revert => "تراجع / إلغاء",
        EntityChange => "تغيير جهة",
        Recovered => "استرداد",
        ReferredToStart => "محال الى البداية",
        CircuitReferred => "إحالة نقل دائرة",
        CircuitReregistered => "إعادة قيد بدائرة جديدة",
        CircuitRenamed => "إعادة تسمية دائرة",
        _ => type,
    };

    public static bool IsStruckOff(string? type) => type == StruckOff;
    public static bool IsRenewal(string? type) => type == Renewal;
    public static bool IsRecovered(string? type) => type == Recovered;
    public static bool IsStatusChange(string? type) =>
        type == Deferred || type == Settled || type == Forcible || type == Revert
        || type == ReferredToStart;
}
