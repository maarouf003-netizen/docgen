using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DocGenerator.Application.Common;

/// <summary>
/// تطبيع الاسم الثلاثي العربي ليُعتمد كمعيار للمقارنة في الدخول والتفرد:
/// يحوّل أ/إ/آ إلى ا، وة إلى ه، وى إلى ي، ويزيل التشكيل والتطويل، ويوحّد المسافات،
/// ويحوّل إلى أحرف صغيرة. قاعدة بيانات الاسم تُخزَّن مطبّعة بهذه القاعدة عند الإنشاء،
/// لذا يمكن مقارنتها بـ = مباشرة في SQL.
/// </summary>
public static class ArabicNameNormalizer
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>تطبيع الاسم للمقارنة وللتخزين القياسي. يعيد سلسلة فارغة للمدخلات الفارغة.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value.Trim().ToLowerInvariant();
        text = text.Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا');
        text = text.Replace('ة', 'ه');
        text = text.Replace('ى', 'ي');

        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            // التشكيل والعلامات غير المزجية تُحذف، وكذلك التطويل (ـ).
            if (char.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            if (ch == 'ـ')
                continue;
            builder.Append(ch);
        }

        return WhitespaceRegex.Replace(builder.ToString(), " ").Trim();
    }
}
