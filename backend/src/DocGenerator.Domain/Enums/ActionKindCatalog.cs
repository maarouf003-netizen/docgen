namespace DocGenerator.Domain.Enums;

/// <summary>
/// القيم المسموحة لحقل ActionKind في جدول أحداث تغييرات الجهات.
/// </summary>
public static class ActionKindCatalog
{
    public const string Rename = "rename";
    public const string Move = "move";
    public const string Merge = "merge";
    public const string Abolish = "abolish";
    public const string Create = "create";
    public const string Review = "review";
    public const string Import = "import";

    public static readonly IReadOnlySet<string> ValidKinds = new HashSet<string>
    {
        Rename, Move, Merge, Abolish, Create, Review, Import,
    };

    public static string ToLabel(string kind) => kind switch
    {
        Rename => "إعادة تسمية",
        Move => "نقل قيد",
        Merge => "دمج",
        Abolish => "إلغاء",
        Create => "إنشاء",
        Review => "مراجعة",
        Import => "استيراد",
        _ => kind,
    };

    public static bool IsValid(string? kind) => kind != null && ValidKinds.Contains(kind);
}
