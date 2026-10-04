namespace DocGenerator.Infrastructure.Persistence.Configurations;

/// <summary>
/// بناء قوائم `IN` المقتبسة لقيود `Check` (`PB-002`) — القيم عربية/رمزية
/// بلا فواصل مفردة. مصدر واحد لتُبنى القيود من ثوابت الكتالوجات لا نصوص مكررة.
/// </summary>
internal static class CheckConstraintLists
{
    internal static string InList(IEnumerable<string?> values) =>
        string.Join(", ", values.Select(v => $"'{v}'"));
}
