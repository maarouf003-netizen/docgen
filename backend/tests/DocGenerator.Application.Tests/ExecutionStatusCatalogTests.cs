using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests;

public class ExecutionStatusCatalogTests
{
    [Theory]
    [InlineData("منفذ جبريا", ExecutionStatus.ExecutedForcibly)]
    [InlineData("منفذ بالتسوية", ExecutionStatus.ExecutedBySettlement)]
    [InlineData("تريث", ExecutionStatus.Deferred)]
    [InlineData("", ExecutionStatus.None)]
    [InlineData("غير معروف", ExecutionStatus.None)]
    public void Classify_MapsKnownAndUnknownStatuses(string status, ExecutionStatus expected)
    {
        Assert.Equal(expected, ExecutionStatusCatalog.Classify(status));
    }

    [Theory]
    [InlineData(ExecutionStatus.ExecutedForcibly, "منفذ جبريا")]
    [InlineData(ExecutionStatus.ExecutedBySettlement, "منفذ بالتسوية")]
    [InlineData(ExecutionStatus.Deferred, "تريث")]
    [InlineData(ExecutionStatus.None, "")]
    public void ToLabel_MapsEnumToArabicLabel(ExecutionStatus status, string expected)
    {
        Assert.Equal(expected, ExecutionStatusCatalog.ToLabel(status));
    }

    [Fact]
    public void ValidStatuses_IncludeEmptyAndAllExecutionStatuses()
    {
        Assert.Equal(
            new[] { "", "تريث", "منفذ بالتسوية", "منفذ جبريا" },
            ExecutionStatusCatalog.ValidStatuses.OrderBy(s => s, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void ValidSubStatuses_IncludeBothSubStatuses()
    {
        Assert.Equal(
            new[] { "منفذ جزئيا", "منفذ كاملا" },
            ExecutionStatusCatalog.ValidSubStatuses.OrderBy(s => s, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Classify_ToLabel_RoundTrip_IsStableForEveryValidStatus()
    {
        foreach (var status in ExecutionStatusCatalog.ValidStatuses)
        {
            Assert.Equal(status, ExecutionStatusCatalog.ToLabel(ExecutionStatusCatalog.Classify(status)));
        }
    }
}
