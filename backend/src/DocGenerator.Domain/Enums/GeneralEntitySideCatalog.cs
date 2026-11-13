namespace DocGenerator.Domain.Enums;

/// <summary>
/// المصدر الوحيد لصفات الملف الثابتة (GeneralEntitySide).
/// تُثبَّت الصفة عند إنشاء الملف ولا تتغير أثناء التعديل:
/// Applicant = ملف «الجهة العامة طالبة التنفيذ»، Executed = وضع «الجهة العامة منفذ عليها»،
/// Deposit = وضع «عرض وايداع».
/// تُخزَّن القيم بالإنكليزية في قاعدة البيانات لتجنب اعتماد البحث والتصفية على النصوص العربية.
/// </summary>
public static class GeneralEntitySideCatalog
{
    public const string Applicant = "applicant";
    public const string Executed = "executed";
    public const string Deposit = "deposit";

    /// <summary>
    /// ترتيب ثابت لبناء قيد `CHECK` — لا تعتمد على ترتيب `HashSet` غير الحتمي بين العمليات.
    /// يجب أن يبقى مطابقًا لترتيب القيد في الهجرة/اللقطة الحالية (`applicant, executed, deposit`).
    /// </summary>
    public static readonly IReadOnlyList<string> OrderedSides = new[]
    {
        Applicant, Executed, Deposit,
    };

    /// <summary>مجموعة الصفات الصالحة — مشتقة من الترتيب الثابت كمصدر وحيد.</summary>
    public static readonly IReadOnlySet<string> ValidSides = new HashSet<string>(OrderedSides);

    /// <summary>العنوان العربي الظاهر للمستخدم للصفة المحددة — المجهول يُعرض خامه.</summary>
    public static string ToLabel(string side) => side switch
    {
        Executed => "الجهة العامة منفذ عليها",
        Deposit => "عرض وايداع",
        Applicant => "الجهة العامة طالبة التنفيذ",
        _ => side,
    };

    /// <summary>
    /// هل الصفة من عائلة «منفذ عليه» (Executed أو Deposit)؟ فالصفتان تشتركان في نفس
    /// البنية والحقول (السند التنفيذي، طالبو العرض/التنفيذ، الجهات، حالة الوضع المتداول/
    /// منفذ/مشطوب) معزولة عن نظام «طالبة تنفيذ».
    /// </summary>
    public static bool IsExecutedLike(string? side) => side == Executed || side == Deposit;
}
