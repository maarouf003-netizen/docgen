using System.Text;
using System.Text.RegularExpressions;

namespace DocGenerator.Application.Common.Security;

/// <summary>
/// P2-09 — تنقية أسماء ملفات التصدير قبل تسليمها في ترويسة <c>Content-Disposition</c>.
/// <para>
/// الاعتماد على <see cref="Path.GetInvalidFileNameChars"/> وحده غير كافٍ: المجموعة
/// تعتمد على المنصة (على Linux لا تحتوي إلا <c>/</c> و<c>\0</c>)، فاسم يحتوي
/// <c>"</c> أو سطرًا جديدًا أو محرف تحكّم يمرّ سالمًا على الخادم ويصل إلى بناء
/// الترويسة. لذلك تُحظر المجموعة الصريحة **بغض النظر عن المنصة**.
/// </para>
/// <para>
/// كما تُزال محارف تحكّم الاتجاه (bidi overrides/isolates) لأنها غير مرئية وقد
/// تُستخدم لتمويه امتداد الملف (مثال: إخفاء <c>.exe</c> خلف امتداد يبدو معكوسًا)،
/// وتُقصّ الأطوال الشاذة مع الحفاظ على أزواج UTF-16 كاملة.
/// </para>
/// </summary>
public static class ExportFileName
{
    /// <summary>سقف طول الاسم بعد التنقية (بلا الامتداد الذي يضيفه المتصل).</summary>
    public const int MaxLength = 120;

    private static readonly Regex WhitespaceRun = new(@"\s+", RegexOptions.Compiled);

    private static readonly HashSet<char> ForbiddenCharacters =
    [
        // محارف غير صالحة في أسماء الملفات على Windows (وكذلك على Linux للإجماع).
        '<', '>', ':', '"', '/', '\\', '|', '?', '*',
        // محارف تحكّم الاتجاه: تضليل بصري لامتداد الملف.
        '\u202A', '\u202B', '\u202C', '\u202D', '\u202E',
        '\u2066', '\u2067', '\u2068', '\u2069',
    ];

    /// <summary>
    /// يعيد اسمًا آمنًا للاستخدام في اسم الملف والترويسة، أو <paramref name="fallback"/>
    /// إذا لم يبقَ أي محتوى صالح.
    /// </summary>
    /// <exception cref="ArgumentException">إذا كان <paramref name="fallback"/> فارغًا.</exception>
    public static string Sanitize(string? value, string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);

        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsControl(character) || ForbiddenCharacters.Contains(character))
            {
                continue;
            }

            builder.Append(character);
        }

        // طيّ المسافات المتكررة ثم تقليم الأطراف (Windows يرفض نقطة/مسافة نهائية).
        var cleaned = WhitespaceRun.Replace(builder.ToString(), " ").Trim().Trim('.', ' ');

        if (cleaned.Length > MaxLength)
        {
            cleaned = cleaned[..MaxLength];
            // لا نترك نصف زوج surrogate في الاسم.
            if (cleaned.Length > 0 && char.IsHighSurrogate(cleaned[^1]))
            {
                cleaned = cleaned[..^1];
            }

            cleaned = cleaned.TrimEnd('.', ' ');
        }

        return cleaned.Length == 0 ? fallback : cleaned;
    }
}
