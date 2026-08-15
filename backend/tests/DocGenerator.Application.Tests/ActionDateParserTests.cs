using System.Globalization;
using DocGenerator.Application.Common;

namespace DocGenerator.Application.Tests;

public class ActionDateParserTests
{
    [Theory]
    [InlineData("1/8/2026", "2026-08-01")]
    [InlineData("01/08/2026", "2026-08-01")]
    [InlineData("15-3-2026", "2026-03-15")]
    [InlineData("2026-12-31", "2026-12-31")]
    [InlineData("1/8/26", "2026-08-01")]
    [InlineData("1/8/99", "1999-08-01")]
    [InlineData("5/8/49", "2049-08-05")]
    [InlineData("29/2/2024", "2024-02-29")]
    public void TryParse_SupportedFormats_Parses(string input, string expectedIso)
    {
        var expected = DateTime.Parse(expectedIso, CultureInfo.InvariantCulture);
        Assert.Equal(expected, ActionDateParser.TryParse(input));
    }

    [Theory]
    [InlineData("٣١/٠٢/٢٠٢٦")]
    [InlineData("ليس تاريخا")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_InvalidOrUnparsable_ReturnsNull(string? input)
    {
        Assert.Null(ActionDateParser.TryParse(input));
    }

    [Theory]
    [InlineData("١/٨/٢٠٢٦", "2026-08-01")]
    [InlineData("٠١/٠٨/٢٠٢٦", "2026-08-01")]
    [InlineData("١٥-٣-٢٠٢٦", "2026-03-15")]
    [InlineData("٢٠٢٦-١٢-٣١", "2026-12-31")]
    [InlineData("۱/۸/۹۹", "1999-08-01")]
    [InlineData("۲۹/۲/۲۰۲۴", "2024-02-29")]
    [InlineData("۳۱/۰۳/۲۰۲۶", "2026-03-31")] // الأرقام الفارسية تُطبَّع إلى ASCII ثم تُحلَّل
    public void TryParse_ArabicIndicDigits_NormalizedAndParsed(string input, string expectedIso)
    {
        var expected = DateTime.Parse(expectedIso, CultureInfo.InvariantCulture);
        Assert.Equal(expected, ActionDateParser.TryParse(input));
    }
}
