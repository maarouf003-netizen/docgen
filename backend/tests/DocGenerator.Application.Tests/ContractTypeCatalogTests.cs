using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests;

[Trait("Category", "Unit")]
public sealed class ContractTypeCatalogTests
{
    [Theory]
    [InlineData("عادي", false)]
    [InlineData(" عادي ", false)]
    [InlineData("مصرفي", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("مجهول", true)]
    public void IsBanking_UsesOnlyExplicitOrdinaryAsOrdinary(string? selector, bool expected)
    {
        Assert.Equal(expected, ContractTypeCatalog.IsBanking(selector));
    }
}
