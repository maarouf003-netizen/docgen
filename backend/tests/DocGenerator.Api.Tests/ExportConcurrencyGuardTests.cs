using DocGenerator.Api.Security;

namespace DocGenerator.Api.Tests;

/// <summary>
/// حارس التصدير المتزامن: دخول واحد لكل مفتاح، وتحرير آمن التكرار،
/// وصيغة مفتاح ثابتة يتعاقد عليها الفلتر والاختبارات.
/// </summary>
public sealed class ExportConcurrencyGuardTests
{
    [Fact]
    public void TryEnter_FirstSucceeds_SecondFailsUntilExit()
    {
        var guard = new ExportConcurrencyGuard();

        Assert.True(guard.TryEnter("export:user:7"));
        Assert.False(guard.TryEnter("export:user:7"));

        guard.Exit("export:user:7");
        Assert.True(guard.TryEnter("export:user:7"));
    }

    [Fact]
    public void Exit_MissingKey_DoesNotThrow()
    {
        var guard = new ExportConcurrencyGuard();

        var ex = Record.Exception(() => guard.Exit("export:user:nope"));
        Assert.Null(ex);
    }

    [Fact]
    public void Keys_AreNamespacesPerSubject()
    {
        Assert.Equal("export:user:7", ExportConcurrencyGuard.KeyFor("7", "1.2.3.4"));
        Assert.Equal("export:ip:1.2.3.4", ExportConcurrencyGuard.KeyFor(null, "1.2.3.4"));
        Assert.Equal("export:ip:unknown", ExportConcurrencyGuard.KeyFor(null, null));
    }

    [Fact]
    public void DifferentSubjects_DoNotBlockEachOther()
    {
        var guard = new ExportConcurrencyGuard();

        Assert.True(guard.TryEnter("export:user:7"));
        Assert.True(guard.TryEnter("export:user:8"));
    }
}
