using DocGenerator.Application.Services;
using Xunit;

namespace DocGenerator.Application.Tests;

/// <summary>
/// W1/B3: تحليل المبلغ المحصل حتمي — النقطة فقط فاصل عشري، والفاصلة تُرفض
/// (كانت `"1,5"` تُقرأ 15)، والتحقق المالي المركزي يرفض الكسور والسالب.
/// </summary>
public sealed class DocumentStatusRulesAmountParsingTests
{
    [Theory]
    [InlineData("15", 15)]
    [InlineData("1500.50", 1500.50)]
    [InlineData("0", 0)]
    [InlineData("١٥٠٠٫٥", 1500.5)]
    [InlineData("١٬٠٠٠", 1000)]
    public void ParseCollectedAmount_AcceptsDeterministicFormats(string raw, double expected)
    {
        var parsed = DocumentStatusRules.ParseCollectedAmount(raw);

        Assert.Equal((decimal)expected, parsed);
    }

    [Theory]
    [InlineData("1,5")]
    [InlineData("1,500.50")]
    [InlineData("1 500")]
    [InlineData("1500.555")]
    [InlineData("-5")]
    [InlineData("abc")]
    public void ParseCollectedAmount_RejectsAmbiguousOrInvalidFormats(string raw)
    {
        Assert.Throws<ArgumentException>(() => DocumentStatusRules.ParseCollectedAmount(raw));
    }

    [Fact]
    public void ParseCollectedAmount_EmptyIsNull()
    {
        Assert.Null(DocumentStatusRules.ParseCollectedAmount(null));
        Assert.Null(DocumentStatusRules.ParseCollectedAmount("   "));
    }
}
