using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات وحدة لسياسة «سارية/حاجبة/نهائية» (D2): التعريف الواسع لـ S1 مقابل
/// الضيق لـ C، مع تثبيت الثوابت حرفيًا (لا لفظ «منفذ إنابة» الملتبس).
/// </summary>
public class DelegationActivityPolicyTests
{
    private static DocumentDelegation Delegation(string status, Document? target = null) => new()
    {
        SourceDocumentId = 1,
        CreatedById = 1,
        DelegatedCourt = "دائرة تنفيذ حلب",
        Status = status,
        TargetDocument = target,
    };

    [Theory]
    [InlineData(DelegationStatusCatalog.PendingHead)]
    [InlineData(DelegationStatusCatalog.Assigned)]
    [InlineData(DelegationStatusCatalog.Registered)]
    public void IsLifecycleActive_NonExecuted_True(string status)
    {
        Assert.True(DelegationActivityPolicy.IsLifecycleActive(Delegation(status)));
    }

    [Fact]
    public void IsLifecycleActive_Executed_False()
    {
        Assert.False(DelegationActivityPolicy.IsLifecycleActive(Delegation(DelegationStatusCatalog.Executed)));
    }

    [Theory]
    [InlineData(ExecutionStatusCatalog.StateStruckOff)]
    [InlineData(ExecutionStatusCatalog.Recovered)]
    [InlineData(ExecutionStatusCatalog.DelegationExecuted)]
    public void IsTargetTerminal_FinalExecStatus_True(string execStatus)
    {
        Assert.True(DelegationActivityPolicy.IsTargetTerminal(new Document { ExecStatus = execStatus }));
    }

    [Fact]
    public void IsTargetTerminal_ExecutedSideStruckOff_True()
    {
        Assert.True(DelegationActivityPolicy.IsTargetTerminal(
            new Document { ExecutedStatus = ExecutedStatusCatalog.StruckOff }));
    }

    [Theory]
    [InlineData("")]
    [InlineData(ExecutionStatusCatalog.Deferred)]
    public void IsTargetTerminal_TradingOrDeferred_False(string execStatus)
    {
        Assert.False(DelegationActivityPolicy.IsTargetTerminal(new Document { ExecStatus = execStatus }));
    }

    [Fact]
    public void IsAssetBlocking_PendingWithoutTarget_True()
    {
        Assert.True(DelegationActivityPolicy.IsAssetBlocking(Delegation(DelegationStatusCatalog.PendingHead)));
    }

    [Fact]
    public void IsAssetBlocking_RegisteredWithTradingTarget_True()
    {
        var d = Delegation(DelegationStatusCatalog.Registered, new Document { ExecStatus = "" });
        Assert.True(DelegationActivityPolicy.IsAssetBlocking(d));
    }

    [Fact]
    public void IsAssetBlocking_RegisteredWithTerminalTarget_False()
    {
        var d = Delegation(DelegationStatusCatalog.Registered,
            new Document { ExecStatus = ExecutionStatusCatalog.Recovered });
        Assert.False(DelegationActivityPolicy.IsAssetBlocking(d));
    }

    [Fact]
    public void IsAssetBlocking_Executed_FalseEvenWithTradingTarget()
    {
        var d = Delegation(DelegationStatusCatalog.Executed, new Document { ExecStatus = "" });
        Assert.False(DelegationActivityPolicy.IsAssetBlocking(d));
    }

    [Fact]
    public void WideVsNarrow_RegisteredWithTerminalTarget_ActiveButNotBlocking()
    {
        // جوهر D2: «سارية» أوسع من «حاجبة» — هذه الحالة حية لـ S1 وغير حاجبة لـ C.
        var d = Delegation(DelegationStatusCatalog.Registered,
            new Document { ExecStatus = ExecutionStatusCatalog.StateStruckOff });
        Assert.True(DelegationActivityPolicy.IsLifecycleActive(d));
        Assert.False(DelegationActivityPolicy.IsAssetBlocking(d));
    }
}
