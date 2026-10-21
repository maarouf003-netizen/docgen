using System.Globalization;
using System.Text;

namespace DocGenerator.Application.Services;

/// <summary>
/// سياسة طول نصوص التنبيهات ومعالجتها — مركزية وواعية بـUnicode (الخطة §7.3).
///
/// القواعد:
/// <list type="bullet">
/// <item>لا قصّ صامت: التخزين بلا سقف عمود، والسقوف إمّا ترفض كتابة صريحة.</item>
/// <item>المعاينة تُحسب بنقاط Unicode (code points) لا بوحدات UTF-16، فلا تُقطع
/// <c>surrogate pair</c> ولا تُترك علامة تجميع معلقة.</item>
/// <item><c>MessageLength</c> = عدد نقاط Unicode، مطابق لـ<code>length()</code> في SQLite وPostgreSQL.</item>
/// </list>
/// </summary>
public static class HeadAlertTextLimits
{
    /// <summary>سقف المعاينة المعروضة في القوائم (نقاط Unicode).</summary>
    public const int PreviewMaxElements = 280;

    /// <summary>
    /// طول البادئة المجلوبة من القاعدة (<c>substr</c>/<c>substring</c>) — ضعف السقف زائد واحد،
    /// لضمان بناء معاينة كاملة داخل الذاكرة دون جلب النص كاملًا.
    /// </summary>
    public const int SqlPrefixLength = 561;

    /// <summary>حدّ النص اليدوي (على نص المستخدم قبل إضافة البادئة).</summary>
    public const int ManualMessageLimit = 16_000;

    /// <summary>سقف الأمان للنصوص النظامية — تجاوزه خطأ صريح يُسجَّل، لا بتر.</summary>
    public const int SafetyMessageLimit = 64_000;

    /// <summary>علامة القصّ (محرف واحد ضمن السقف).</summary>
    public const string Ellipsis = "…";

    /// <summary>رمز وصل الصفر (ZWJ) — يُقصّ من نهاية المعاينة حتى لا يبقى تسلسل إيموجي معلّق.</summary>
    private const int ZeroWidthJoiner = 0x200D;

    /// <summary>
    /// يبني معاينة العرض: يعيد النص كاملًا إن لم يتجاوز السقف، وإلا أعاد بادئة محدودة
    /// مع علامة «…» ضمن السقف. العملية على نقاط Unicode، فلا تُقطع <c>surrogate pair</c>
    /// ولا تُترك علامة تجميع (combining mark) معلّقة في نهاية المقطوع.
    /// </summary>
    public static (string Preview, bool Truncated) BuildPreview(string? message, int maxElements = PreviewMaxElements)
    {
        if (maxElements <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxElements), "سقف المعاينة يجب أن يكون أكبر من صفر");

        var text = message ?? string.Empty;
        if (text.Length == 0)
            return (string.Empty, false);

        var total = CountCodePoints(text);
        if (total <= maxElements)
            return (text, false);

        var limit = Math.Max(0, maxElements - 1);
        var runes = new List<Rune>(limit);
        foreach (var rune in text.EnumerateRunes())
        {
            if (runes.Count >= limit)
                break;
            runes.Add(rune);
        }

        TrimDanglingMarks(runes);

        var builder = new StringBuilder(runes.Count + 1);
        foreach (var rune in runes)
            builder.Append(rune.ToString());
        builder.Append(Ellipsis);

        return (builder.ToString(), true);
    }

    /// <summary>عدد نقاط Unicode في النص — مطابق لـ<code>length()</code> في SQLite وPostgreSQL.</summary>
    public static int CountCodePoints(string? message)
    {
        if (string.IsNullOrEmpty(message))
            return 0;
        var count = 0;
        foreach (var _ in message.EnumerateRunes())
            count++;
        return count;
    }

    /// <summary>
    /// يتحقق من سقف الأمان للنص — يرمي <see cref="ArgumentException"/> صريحة بدل البتر.
    /// </summary>
    public static string EnsureWithinSafety(string? message)
    {
        var text = message ?? string.Empty;
        var length = CountCodePoints(text);
        if (length > SafetyMessageLimit)
            throw new ArgumentException(
                $"نص التنبيه يتجاوز الحد الأقصى المسموح ({SafetyMessageLimit} حرفًا) —الطول {length}",
                nameof(message));
        return text;
    }

    /// <summary>يتحقق من حدّ النص اليدوي (على نص المستخدم قبل البادئة).</summary>
    public static string EnsureWithinManualLimit(string? message)
    {
        var text = message ?? string.Empty;
        var length = CountCodePoints(text);
        if (length > ManualMessageLimit)
            throw new ArgumentException(
                $"نص التنبيه اليدوي يتجاوز الحد الأقصى المسموح ({ManualMessageLimit} حرفًا) — الطول {length}",
                nameof(message));
        return text;
    }

    /// <summary>معاينة قصيرة للتدقيق (120 حرفًا افتراضيًا) — واعية بـUnicode.</summary>
    public static string PreviewForAudit(string? message, int maxElements = 120)
        => BuildPreview(message, maxElements).Preview;

    /// <summary>يحذف علامات التجميع المعلّقة وآخر رمز وصل الصفر من نهاية المقطع.</summary>
    private static void TrimDanglingMarks(List<Rune> runes)
    {
        while (runes.Count > 0)
        {
            var last = runes[^1];
            var category = Rune.GetUnicodeCategory(last);
            var isDangling = category is UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark
                || last.Value == ZeroWidthJoiner;
            if (!isDangling)
                break;
            runes.RemoveAt(runes.Count - 1);
        }
    }
}
