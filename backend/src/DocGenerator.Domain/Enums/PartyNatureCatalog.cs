namespace DocGenerator.Domain.Enums;

/// <summary>
/// المصدر الوحيد لطبيعة أطراف الملف: شخص طبيعي (natural) أو شخص اعتباري (legal) —
/// ولملفات وضع «الجهة العامة منفذ عليها» جهة عامة (public) مقابل شخص اعتباري (legal).
/// تُخزَّن القيم بالإنكليزية في القاعدة لتجنب اعتماد البحث والتصفية على النصوص العربية.
/// </summary>
public static class PartyNatureCatalog
{
    /// <summary>شخص طبيعي (الاسم الثلاثي وحقول الهوية).</summary>
    public const string Natural = "natural";

    /// <summary>شخص اعتباري (شركة/مؤسسة): الاسم الاعتباري ورقم التسجيل ومن يمثلها.</summary>
    public const string Legal = "legal";

    /// <summary>جهة عامة منفذ عليها (اسم الجهة + فرعها) في وضع «الجهة العامة منفذ عليها».</summary>
    public const string PublicEntity = "public";

    /// <summary>الطبيعة المسموح بها لطرف طبيعي/اعتباري (مقترض/كفيل/طالب تنفيذ).</summary>
    public static readonly IReadOnlySet<string> ValidNatures = new HashSet<string>
    {
        Natural, Legal,
    };

    /// <summary>الطبيعة المسموح بها للمنفذ عليه في وضع «الجهة العامة منفذ عليها» (جهة عامة/شخص اعتباري).</summary>
    public static readonly IReadOnlySet<string> ValidEntityNatures = new HashSet<string>
    {
        PublicEntity, Legal,
    };

    public static bool IsLegal(string? nature) => nature == Legal;

    public static string ToLabel(string? nature) => nature switch
    {
        Legal => "شخص اعتباري",
        PublicEntity => "جهة عامة",
        _ => "شخص طبيعي",
    };
}
