using DocGenerator.Application.Common;

namespace DocGenerator.Application.Tests;

public class DigitNormalizerTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("55", "55")]
    [InlineData(" 55 ", "55")]
    [InlineData("١٢٣", "123")]
    [InlineData("٢٠٢٦", "2026")]
    public void NormalizeDigitsOrNull_MapsEmptyToNull_AndNormalizesDigits(string? input, string? expected)
    {
        Assert.Equal(expected, DigitNormalizer.NormalizeDigitsOrNull(input));
    }
}
