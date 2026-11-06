namespace DocGenerator.Application.Common;

/// <summary>
/// ملخّص «الإضافات المحلية» الذي يُخزَّن على صف الإنابة
/// (<c>DocumentDelegation.LocalAdditionLastMessage</c>): منطق نقي خارج خدمة الإنابة ليكون
/// قابلًا للاختبار المباشر (سقف العمود، ترتيب الأحدث، موضع علامة القص).
///
/// **عقد دلالي ملزم:** المدخل أجزاء دفعة واحدة بترتيب الإدخال (الأحدث آخرًا)، والمخرج:
/// (1) يحتفظ بأحدث ما يتسع له العمود ويُسقط الأقدم، مع علامة في **البداية**؛
/// (2) إن لم يتسع العمود لعنصر واحد كامل (نص واحد ضخم) قُصّ ذلك العنصر نفسه من **نهايته**
/// بعلامة ختامية. العلامتان مختلفتان عمدًا: علامة البداية تعني «عناصر أقدم أُسقطت»،
/// والختامية تعني «هذا النص نفسه مقطوع» — ولا تُستعمل إحداهما مكان الأخرى.
/// الطول يُقاس بنقاط Unicode تطابقًا مع <c>varchar(1000)</c> في PostgreSQL.
/// </summary>
internal static class LocalAdditionSummary
{
    /// <summary>سقف النص — مطابق لطول العمود في السياقين (`varchar(1000)` في PostgreSQL).</summary>
    internal const int MessageLimit = 1000;

    /// <summary>فاصل أجزاء الدفعة نفسها (وريث/ممثل) في الملخص وفي نص التنبيه.</summary>
    internal const string Separator = "؛ ";

    /// <summary>علامة «هناك أقدم مما يظهر» في بداية الملخص (الأحدث أهم).</summary>
    internal const string OmittedMark = "… ";

    /// <summary>
    /// يبني الملخص من أجزاء الدفعة. يعيد نصًا فارغًا لدفعة فارغة (لا استثناء: الدفعة الفارغة
    /// ليست خطأ برمجيًا — المستدعي يتحقق من العدد أصلًا).
    /// </summary>
    public static string Build(IReadOnlyList<string> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Count == 0)
            return string.Empty;

        // نُبقي مساحةً لعلامة البداية دائمًا: إن اتّسع كل شيء لا تُستخدم، وإن أُسقط جزء
        // فهي محسوبة مسبقًا فلا يتجاوز الناتج حدّ العمود.
        var keepWithin = MessageLimit - HeadAlertTextLimits.CountCodePoints(OmittedMark);
        var kept = new List<string>(parts.Count);
        var length = 0;
        for (var i = parts.Count - 1; i >= 0; i--)
        {
            var separator = kept.Count == 0 ? 0 : HeadAlertTextLimits.CountCodePoints(Separator);
            var partLength = HeadAlertTextLimits.CountCodePoints(parts[i]);
            if (length + separator + partLength > keepWithin)
                break;
            kept.Insert(0, parts[i]);
            length += separator + partLength;
        }

        if (kept.Count == 0)
            return TruncateSinglePart(parts[^1]);

        var joined = string.Join(Separator, kept);
        return kept.Count == parts.Count ? joined : OmittedMark + joined;
    }

    /// <summary>
    /// عنصر واحد يتجاوز ما تبقّى بعد حجز علامة البداية: يُعاد عبر باني المعاينة الواعي بـUnicode
    /// بقصّ **نهايته** وعلامة ختامية. حالتان: تجاوز السقف الكامل (فيُقصّ) أو وقوعه بين
    /// <c>keepWithin</c> والسقف (فيُعاد كاملًا لأنه يتسع للعمود ولا يحتاج علامة).
    /// </summary>
    private static string TruncateSinglePart(string part)
        => HeadAlertTextLimits.BuildPreview(part, MessageLimit).Preview;
}
