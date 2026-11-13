namespace DocGenerator.Infrastructure.Persistence.Configurations;

/// <summary>
/// بناء قوائم `IN` المقتبسة لقيود `Check` (`PB-002`) — القيم عربية/رمزية
/// بلا فواصل مفردة. مصدر واحد لتُبنى القيود من ثوابت الكتالوجات لا نصوص مكررة.
/// </summary>
internal static class CheckConstraintLists
{
    /// <summary>
    /// يبني قائمة `IN` من تسلسل **مرتب** صراحةً. النوع `IReadOnlyList` مقصود:
    /// تمرير `HashSet`/`IEnumerable` غير مرتب ممنوع وقت الترجمة لمنع عدم حتمية القيود.
    /// </summary>
    internal static string InList(IReadOnlyList<string?> values) =>
        string.Join(", ", values.Select(v => $"'{v}'"));
}
