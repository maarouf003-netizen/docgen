using DocGenerator.Application.Common;

namespace DocGenerator.Application.Tests;

public class ArabicDigitNormalizerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void Normalize_EmptyOrNull_PassesThrough(string? input, string expected)
    {
        Assert.Equal(expected, ArabicDigitNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("123", "123")]
    [InlineData("12.5", "12.5")]
    [InlineData("1/8/2026", "1/8/2026")]
    [InlineData("أحمد 12 شارع", "أحمد 12 شارع")]
    public void Normalize_AsciiOnly_Unchanged(string input, string expected)
    {
        Assert.Equal(expected, ArabicDigitNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("٠١٢٣٤٥٦٧٨٩", "0123456789")]
    [InlineData("١/٨/٢٠٢٦", "1/8/2026")]
    [InlineData("١٢٥٬٥٠٠", "125٬500")] // لا يمس فواصل الأرقام العربية (تعالج في موضع المبالغ فقط)
    public void Normalize_ArabicIndicDigits_ConvertedToAscii(string input, string expected)
    {
        Assert.Equal(expected, ArabicDigitNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("۰۱۲۳۴۵۶۷۸۹", "0123456789")]
    [InlineData("۱/۸/۲۰۲۶", "1/8/2026")]
    public void Normalize_PersianDigits_ConvertedToAscii(string input, string expected)
    {
        Assert.Equal(expected, ArabicDigitNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("٢٠٢٦ - أ", "2026 - أ")]
    [InlineData("ص٢٤٥", "ص245")]
    public void Normalize_MixedText_OnlyDigitsConverted(string input, string expected)
    {
        Assert.Equal(expected, ArabicDigitNormalizer.Normalize(input));
    }

    [Fact]
    public void Normalize_AsciiOnly_ReturnsSameReference()
    {
        const string input = "1/8/2026";
        Assert.Same(input, ArabicDigitNormalizer.Normalize(input));
    }
}
