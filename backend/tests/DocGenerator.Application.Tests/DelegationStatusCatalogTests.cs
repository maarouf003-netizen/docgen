using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests;

public class DelegationStatusCatalogTests
{
    [Fact]
    public void ValidStatuses_IncludeAllLifecycleStages()
    {
        Assert.Equal(
            new[] { "بانتظار رئيس القسم", "محالة", "مسجلة أصولًا", "منفذ إنابة" },
            DelegationStatusCatalog.ValidStatuses.OrderBy(s => s, StringComparer.Ordinal).ToArray());
    }

    [Theory]
    [InlineData(DelegationStatusCatalog.PendingHead)]
    [InlineData(DelegationStatusCatalog.Assigned)]
    [InlineData(DelegationStatusCatalog.Registered)]
    [InlineData(DelegationStatusCatalog.Executed)]
    public void EveryConstant_IsAValidStatus(string status)
    {
        Assert.Contains(status, DelegationStatusCatalog.ValidStatuses);
    }
}
