using DocGenerator.Application.Common;

namespace DocGenerator.Application.Tests;

public class UsernameInputTests
{
    [Theory]
    [InlineData("recoveryadmin\u2069", "recoveryadmin")]
    [InlineData("\u200Brecovery\u200F", "recovery")]
    [InlineData("\uFEFFlawyer1\u2066", "lawyer1")]
    [InlineData("  lawyer1  ", "lawyer1")]
    [InlineData("محامي دمشق", "محامي دمشق")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_RemovesInvisibleFormattingOnly(string? input, string expected)
    {
        Assert.Equal(expected, UsernameInput.Normalize(input));
    }
}
