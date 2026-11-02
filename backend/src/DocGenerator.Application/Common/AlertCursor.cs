using System.Globalization;

namespace DocGenerator.Application.Common;

/// <summary>
/// مؤشر ترقيم keyset لقوائم التنبيهات (الخطة §9.1، الدليل §7 البند 4): آخر صف معروض،
/// والترتيب الحتمي <c>LastContentChangedAt DESC, Id DESC</c>.
///
/// <para>**لماذا keyset لا <c>offset</c>:** الإزاحة تفترض قائمة ثابتة بين الجلبين؛
/// إصدار تنبيه جديد في الأعلى يزيح النافذة فتعود الصفحة التالية مكررات أو فارغة
/// (جذر <c>stalled</c> في <c>useAlertsFeed</c> قبل هذا التغيير). المؤشر يثبّت الموضع
/// على آخر صف رأيته فعليًا، فلا يظهر الصف مرتين ولا يُفقد صف أقدم.</para>
///
/// <para>**الصيغة السلكية:** <c>{ISO-8601-UTC}|{id}</c> — تُبنى في الواجهة من حقلي
/// الصف نفسه (<c>lastContentChangedAt</c> و<c>id</c>) وتُرسل كما هي، والخادم يعيد
/// تحليلها إلى قيمة زمنية دقيقة (بلا فقدان كسور الثواني كما يحدث عند تحويلها إلى
/// <c>Date</c> في JavaScript). أي صيغة أخرى — أو وقت بلا منطقة UTC صريحة —
/// تُرفض <c>400</c> برسالة عربية، ولا تُفسَّر ضمنيًا.</para>
/// </summary>
public sealed record AlertCursor(DateTime LastContentChangedAt, int Id)
{
    /// <summary>الفاصل بين الجزء الزمني والمعرّف في الصيغة السلكية.</summary>
    public const char WireSeparator = '|';

    /// <summary>
    /// يبني الصيغة السلكية من آخر صف معروض. تُستعمل في الاختبارات وتوثيق العقد؛
    /// الواجهة تبنيها من الحقلين كما وصلا (بلا تحويل زمني).
    /// </summary>
    public static string Format(DateTime lastContentChangedAt, int id) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{ToWireTimestamp(lastContentChangedAt)}{WireSeparator}{id}");

    /// <summary>
    /// يحلل الصيغة السلكية بصرامة: جزء زمني ISO-8601 بمنطقة <c>UTC</c> صريحة
    /// (<c>Z</c> أو <c>+00:00</c>) ومعرّف صحيح موجب. لا سقوط إلى افتراضي ولا
    /// تفسير لوقت محلي/غامض — الوقتان مختلفان فعليًا في قاعدة البيانات.
    /// </summary>
    public static bool TryParse(string? value, out AlertCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var text = value.Trim();
        var separator = text.LastIndexOf(WireSeparator);
        if (separator <= 0 || separator == text.Length - 1)
            return false;

        if (!int.TryParse(
                text.AsSpan(separator + 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var id)
            || id < 1)
            return false;

        var stampSpan = text.AsSpan(0, separator);

        // يجب أن يحمل النص منطقة صريحة: <c>Z</c> أو إزاحة صفرية (<c>+00:00</c>).
        // الفحص النصي قبل التحليل مقصود: `DateTimeOffset.TryParse` يفترض المنطقة
        // المحلية عند غيابها، فكان سيقبل وقتًا غامضًا على جهاز ضبطه UTC.
        var hasExplicitUtcZone =
            stampSpan.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
            || stampSpan.EndsWith("+00:00", StringComparison.Ordinal)
            || stampSpan.EndsWith("-00:00", StringComparison.Ordinal);
        if (!hasExplicitUtcZone)
            return false;

        if (!DateTimeOffset.TryParse(
                stampSpan,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var stamp)
            || stamp.Offset != TimeSpan.Zero)
            return false;

        cursor = new AlertCursor(stamp.UtcDateTime, id);
        return true;
    }

    /// <summary>
    /// يحلل المؤشر أو يرمي <see cref="ArgumentException"/> برسالة عربية تُترجم
    /// <c>400</c> في المتحكم (لا استثناء خام ولا رسالة إطار عمل إنجليزية).
    /// </summary>
    public static AlertCursor Parse(string value, string paramName = "cursor") =>
        TryParse(value, out var cursor)
            ? cursor!
            : throw new ArgumentException(
                "قيمة cursor غير صالحة — أعِد تحميل القائمة ثم أعد المحاولة", paramName);

    /// <summary>
    /// يحلل المؤشر **إن وُجد**: الفارغ/الأبيض ⇒ <c>null</c> (ترقيم بالإزاحة)،
    /// وغير الصالح ⇒ <see cref="ArgumentException"/> (<c>400</c>). نقطة الدخول
    /// الوحيدة للخدمة، فتبقى قواعد الشكل في طبقة واحدة.
    /// </summary>
    public static AlertCursor? ParseOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Parse(value.Trim());

    private static string ToWireTimestamp(DateTime value) =>
        (value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime())
            .ToString("O", CultureInfo.InvariantCulture);
}
