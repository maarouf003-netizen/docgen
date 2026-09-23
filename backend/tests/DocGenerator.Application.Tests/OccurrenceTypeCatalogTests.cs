using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests;

public class OccurrenceTypeCatalogTests
{
    [Theory]
    [InlineData("referred-to-start", "محال الى البداية")]
    [InlineData("revert", "تراجع / إلغاء")]
    public void ToLabel_MapsKnownTypes(string type, string expected)
    {
        Assert.Equal(expected, OccurrenceTypeCatalog.ToLabel(type));
    }

    [Fact]
    public void ValidTypes_IncludeReferredToStart()
    {
        Assert.Contains(OccurrenceTypeCatalog.ReferredToStart, OccurrenceTypeCatalog.ValidTypes);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("referred-to-start", true)]
    [InlineData("deferred", true)]
    [InlineData("settled", true)]
    [InlineData("forcible", true)]
    [InlineData("revert", true)]
    [InlineData("renewal", false)]
    [InlineData("struck-off", false)]
    [InlineData("recovered", false)]
    [InlineData("entity-change", false)]
    public void IsStatusChange_PinsMatrix(string? type, bool expected)
    {
        Assert.Equal(expected, OccurrenceTypeCatalog.IsStatusChange(type));
    }
}