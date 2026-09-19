using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests;

public class ExecutionStatusCatalogTests
{
    [Theory]
    [InlineData("منفذ جبريا", ExecutionStatus.ExecutedForcibly)]
    [InlineData("منفذ بالتسوية", ExecutionStatus.ExecutedBySettlement)]
    [InlineData("تريث", ExecutionStatus.Deferred)]
    [InlineData("منفذ إنابة", ExecutionStatus.DelegationExecuted)]
    [InlineData("مسترد", ExecutionStatus.Recovered)]
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
    [InlineData(ExecutionStatus.DelegationExecuted, "منفذ إنابة")]
    [InlineData(ExecutionStatus.Recovered, "مسترد")]
    [InlineData(ExecutionStatus.None, "")]
    public void ToLabel_MapsEnumToArabicLabel(ExecutionStatus status, string expected)
    {
        Assert.Equal(expected, ExecutionStatusCatalog.ToLabel(status));
    }

    [Fact]
    public void ValidStatuses_IncludeEmptyAndAllExecutionStatuses()
    {
        Assert.Equal(
            new[] { "", "تريث", "مسترد", "منفذ إنابة", "منفذ بالتسوية", "منفذ جبريا" },
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

    /// <summary>
    /// تثبيت جدول الانتقالات كاملًا بالقيم السلكية الحرفية: أي تغيير مستقبلي في
    /// <see cref="ExecutionStatusCatalog.AllowedStatusChanges"/> يجب أن يكسر هذا الاختبار
    /// عمدًا (مصدر الحقيقة الوحيد لآلة الحالات — تُحسم به C1 خلفيةً في Status.cs:76).
    /// </summary>
    public static IEnumerable<object[]> TransitionTable() => new List<object[]>
    {
        new object[] { "تحت رفع", new[] { "تريث", "منفذ بالتسوية" } },
        new object[] { "متداول", new[] { "تريث", "منفذ بالتسوية", "منفذ جبريا", "مشطوب" } },
        new object[] { "تريث", new[] { "منفذ بالتسوية" } },
        new object[] { "منفذ بالتسوية", Array.Empty<string>() },
        new object[] { "منفذ جبريا", Array.Empty<string>() },
        new object[] { "مشطوب", Array.Empty<string>() },
        new object[] { "مسترد", Array.Empty<string>() },
        new object[] { "منفذ إنابة", Array.Empty<string>() },
        new object[] { "", Array.Empty<string>() },
        new object[] { "غير معروف", Array.Empty<string>() },
    };

    [Theory]
    [MemberData(nameof(TransitionTable))]
    public void AllowedStatusChanges_PinsFullTransitionTable(string currentState, string[] expected)
    {
        var actual = ExecutionStatusCatalog.AllowedStatusChanges(currentState);

        Assert.Equal(
            expected.OrderBy(s => s, StringComparer.Ordinal),
            actual.OrderBy(s => s, StringComparer.Ordinal));
    }

    /// <summary>
    /// C1: «الشطب لا يكون إلا لملف متداول» — «مشطوب» لا يُبلغ إلا من «متداول» حصرًا،
    /// فيحسم <c>IsAllowedStatusChange</c> (Status.cs:76) شطب أي مناب غير متداول حتى عبر API مباشر.
    /// </summary>
    [Fact]
    public void IsAllowedStatusChange_StruckOff_OnlyFromCirculating()
    {
        var states = new[]
        {
            "تحت رفع", "متداول", "تريث", "منفذ بالتسوية", "منفذ جبريا",
            "مشطوب", "مسترد", "منفذ إنابة", "", "غير معروف",
        };

        foreach (var state in states)
            Assert.Equal(
                state == ExecutionStatusCatalog.StateCirculating,
                ExecutionStatusCatalog.IsAllowedStatusChange(state, ExecutionStatusCatalog.StateStruckOff));
    }

    /// <summary>تثبيت مصفوفة التراجع: التراجع إلى متداول من تريث/المنفذين فقط (مسار Revert المستقل).</summary>
    [Theory]
    [InlineData("تريث", true)]
    [InlineData("منفذ بالتسوية", true)]
    [InlineData("منفذ جبريا", true)]
    [InlineData("متداول", false)]
    [InlineData("مشطوب", false)]
    [InlineData("مسترد", false)]
    [InlineData("منفذ إنابة", false)]
    [InlineData("تحت رفع", false)]
    [InlineData("", false)]
    public void CanRevert_PinsRevertMatrix(string currentState, bool expected)
    {
        Assert.Equal(expected, ExecutionStatusCatalog.CanRevert(currentState));
    }
}
