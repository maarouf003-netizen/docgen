using DocGenerator.Application.Common;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات RF-014 (`ARC-004`): قبول حتمي — الصيغ الخمس فقط، بلا بديل مرن
/// ولا سنة برقمين. تفشل قبل الإصلاح (قبول عرضي) وتخضر بعده.
/// </summary>
public class RF014DateAcceptanceTests
{
    [Theory]
    [InlineData("1/8/2026")]
    [InlineData("01/08/2026")]
    [InlineData("15-3-2026")]
    [InlineData("2026-12-31")]
    [InlineData("١/٨/٢٠٢٦")]
    public void TryParse_KeptFormats_Parses(string input)
    {
        Assert.NotNull(ActionDateParser.TryParse(input));
    }

    [Theory]
    [InlineData("1/8/26")]
    [InlineData("1/8/99")]
    [InlineData("5/8/49")]
    public void TryParse_TwoDigitYear_Rejected(string input)
    {
        // كانت تُقبَل (وعتبة 2029 تجعل `30` = 1930 بصمت)؛ بعد القرار تُرفَض.
        Assert.Null(ActionDateParser.TryParse(input));
    }

    [Theory]
    // الفراغات المحيطة: `TryParseExact` بأسلوب `None` يرفضها، والبديل المرن
    // يقبلها — فهي كاشف حتمي للبديل في أي ثقافة (لا يعتمد على أسماء شهور).
    [InlineData("  1/8/2026  ")]
    [InlineData("\t1-8-2026\n")]
    [InlineData("1.8.2026")]
    public void TryParse_LooseFallback_Rejected(string input)
    {
        // كانت رهن ثقافة الخادم؛ بعد القرار مرفوضة حتميًا في كل بيئة.
        Assert.Null(ActionDateParser.TryParse(input));
    }
}
