using System.Globalization;
using System.Text;

namespace DocGenerator.Application.Common;

/// <summary>
/// يحوّل أرقام Unicode العشرية غير ASCII — العربية (٠-٩) والفارسية (۰-۹) وما شابهها —
/// إلى ASCII (0-9) قبل أي تحليل رقمي أو زمني، ليقبل النص الذي يكتبه مستخدمون بأرقام عربية.
/// لا يمس أي محارف أخرى ولا الفواصل، ويمرّر النص دون نسخ إن لم يحتج تطبيعًا.
/// النطاقان الأساسيان: U+0660–U+0669 وU+06F0–U+06F9 (كلاهما في BMP فلا قلق من الأزواج البديلة).
/// </summary>
public static class ArabicDigitNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        if (!ContainsNonAsciiDigit(value))
            return value;

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            var digit = CharUnicodeInfo.GetDecimalDigitValue(c);
            builder.Append(digit >= 0 ? (char)('0' + digit) : c);
        }
        return builder.ToString();
    }

    private static bool ContainsNonAsciiDigit(string value)
    {
        foreach (var c in value)
        {
            if ((c < '0' || c > '9') && char.IsDigit(c))
                return true;
        }
        return false;
    }
}
