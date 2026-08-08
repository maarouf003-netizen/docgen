namespace DocGenerator.Domain.Enums;

/// <summary>
/// المصدر الوحيد لصفات الملف الثابتة (GeneralEntitySide).
/// تُثبَّت الصفة عند إنشاء الملف ولا تتغير أثناء التعديل:
/// Applicant = ملف «الجهة العامة طالبة التنفيذ»، Executed = وضع «الجهة العامة منفذ عليها».
/// تُخزَّن القيم بالإنكليزية في قاعدة البيانات لتجنب اعتماد البحث والتصفية على النصوص العربية.
/// </summary>
public static class GeneralEntitySideCatalog
{
    public const string Applicant = "applicant";
    public const string Executed = "executed";

    public static readonly IReadOnlySet<string> ValidSides = new HashSet<string>
    {
        Applicant, Executed,
    };

    /// <summary>العنوان العربي الظاهر للمستخدم للصفة المحددة.</summary>
    public static string ToLabel(string side) => side switch
    {
        Executed => "الجهة العامة منفذ عليها",
        _ => "الجهة العامة طالبة التنفيذ",
    };
}
