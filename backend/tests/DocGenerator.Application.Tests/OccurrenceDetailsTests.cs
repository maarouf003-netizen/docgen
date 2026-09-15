using DocGenerator.Application.Common;

namespace DocGenerator.Application.Tests;

/// <summary>
/// فكّ «تفاصيل الوقعة» إلى شكليه: قاموس JSON منضّم (يدوي) أو سرد نصي حر (آلي كنوع «تغيير جهة»).
/// </summary>
public class OccurrenceDetailsTests
{
    [Fact]
    public void Split_JsonObject_ReturnsDictionaryAndNullText()
    {
        var (details, text) = OccurrenceDetails.Split("{\"tarithNumber\":\"33\"}");

        Assert.NotNull(details);
        Assert.Equal("33", details["tarithNumber"]);
        Assert.Null(text);
    }

    [Fact]
    public void Split_ArabicNarrative_ReturnsTextAndNullDictionary()
    {
        const string narrative = "تم نقل قيد «وزارة التعليم» (دمشق/الفرع الرئيسي) بموجب قرار إداري رقم 123 بتاريخ 2026-08-01";

        var (details, text) = OccurrenceDetails.Split(narrative);

        Assert.Null(details);
        Assert.Equal(narrative, text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Split_NullOrWhiteSpace_ReturnsNulls(string? raw)
    {
        var (details, text) = OccurrenceDetails.Split(raw);

        Assert.Null(details);
        Assert.Null(text);
    }
}
