using DocGenerator.Application.Common;

namespace DocGenerator.Application.Tests;

/// <summary>
/// قيود بناء ملخص الإضافات المحلية (`LocalAdditionSummary`) — الوحدة النقية التي تغذّي
/// عمود `DocumentDelegation.LocalAdditionLastMessage`. الغرض: تثبيت **عقد** الملخص
/// (الأحدث أولًا، سقف العمود، دلالة كل علامة قص) بعيدًا عن مسار HTTP، لأن هذا العمود
/// يُحفظ على `varchar(1000)` في PostgreSQL وسقوطُ الحد يعني رفض حفظ فعلي.
/// </summary>
public class LocalAdditionSummaryTests
{
    private static string Part(char filler, int length) => new(filler, length);

    [Fact]
    public void Build_EmptyBatch_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, LocalAdditionSummary.Build([]));
    }

    [Fact]
    public void Build_WithinLimit_ReturnsPartsJoinedInInputOrder()
    {
        var summary = LocalAdditionSummary.Build(["أُضيف الوريث أحمد", "أُضيف الممثل الشرعي للمقترض"]);

        Assert.Equal("أُضيف الوريث أحمد؛ أُضيف الممثل الشرعي للمقترض", summary);
        Assert.DoesNotContain(LocalAdditionSummary.OmittedMark, summary);
    }

    [Fact]
    public void Build_ManyParts_KeepsNewestAndMarksOmissionAtStart()
    {
        // 40 جزءًا × ~90 نقطة ≫ السقف: الأحدث (آخر الإدخال) يبقى، والأقدم يُسقط.
        var parts = Enumerable.Range(1, 40).Select(i => $"أُضيف الوريث {Part('م', 60)}{i:00}").ToList();

        var summary = LocalAdditionSummary.Build(parts);

        Assert.StartsWith(LocalAdditionSummary.OmittedMark, summary);
        Assert.Contains($"{Part('م', 60)}40", summary);
        Assert.DoesNotContain($"{Part('م', 60)}01", summary);
        Assert.True(HeadAlertTextLimits.CountCodePoints(summary) <= LocalAdditionSummary.MessageLimit);
    }

    [Fact]
    public void Build_SinglePartOverLimit_TruncatesItsTailWithClosingMark()
    {
        // عنصر واحد يتجاوز السقف: يُقصّ **نهايته** بعلامة ختامية، ولا تُستخدم علامة البداية
        // (لأنها تعني «عناصر أقدم أُسقطت» لا «هذا النص مقطوع»).
        var summary = LocalAdditionSummary.Build([Part('ك', 1500)]);

        Assert.False(summary.StartsWith(LocalAdditionSummary.OmittedMark, StringComparison.Ordinal));
        Assert.EndsWith("…", summary, StringComparison.Ordinal);
        Assert.True(HeadAlertTextLimits.CountCodePoints(summary) <= LocalAdditionSummary.MessageLimit);
    }

    [Fact]
    public void Build_PartBetweenReservedAndHardLimit_IsReturnedWholeWithoutMarks()
    {
        // المنطقة الحدّية: يفيض عن المتاح بعد حجز علامة البداية (998) لكنه يتسع للعمود (1000)
        // ⇒ يُعاد كاملًا بلا أي علامة (لا إسقاط ولا قصّ).
        var exact = Part('س', LocalAdditionSummary.MessageLimit);

        var summary = LocalAdditionSummary.Build([exact]);

        Assert.Equal(exact, summary);
        Assert.DoesNotContain("…", summary);
    }

    [Fact]
    public void Build_SurrogatePairs_AreNeverSplit()
    {
        // إيموجي = زوج بديل (وحدتان UTF-16)؛ القصّ يجب ألا يترك نصف زوج معلّقًا.
        var parts = Enumerable.Range(0, 400).Select(_ => "😀").ToList();

        var summary = LocalAdditionSummary.Build(parts);

        Assert.True(HeadAlertTextLimits.CountCodePoints(summary) <= LocalAdditionSummary.MessageLimit);
        for (var i = 0; i < summary.Length; i++)
        {
            if (char.IsHighSurrogate(summary[i]))
                Assert.True(i + 1 < summary.Length && char.IsLowSurrogate(summary[i + 1]),
                    "زوج بديل مقطوع في نهاية الملخص");
            if (char.IsLowSurrogate(summary[i]))
                Assert.True(i > 0 && char.IsHighSurrogate(summary[i - 1]),
                    "زوج بديل مقطوع في بداية الملخص");
        }
    }
}
