using DocGenerator.Application.Common;

namespace DocGenerator.Application.Tests;

/// <summary>
/// قواعد «تذكيرات الشطب»: العتبة خمسة أشهر، وحدّها منتصف ليل اليوم بتوقيت النظام ناقص
/// خمسة أشهر كـUTC، والاستحقاق عند بلوغ الحد ضمنًا.
/// </summary>
public class StrikeOffReminderCalculatorTests
{
    [Fact]
    public void Months_IsFive() => Assert.Equal(5, StrikeOffReminderCalculator.Months);

    [Fact]
    public void DefaultPaging_IsSane()
    {
        Assert.Equal(20, StrikeOffReminderCalculator.DefaultPerPage);
        Assert.Equal(100, StrikeOffReminderCalculator.MaxPerPage);
    }

    [Fact]
    public void CutoffUtc_IsLocalMidnightFiveMonthsBack_ConvertedToUtc()
    {
        var nowLocal = new DateTime(2026, 11, 11, 15, 30, 0);

        var cutoff = StrikeOffReminderCalculator.CutoffUtc(nowLocal, TestClock.TimeZone);

        // 2026-06-11 00:00 بتوقيت +03 = 2026-06-10T21:00Z.
        Assert.Equal(new DateTime(2026, 6, 10, 21, 0, 0, DateTimeKind.Utc), cutoff);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void IsDue_IsInclusiveAtTheCutoff(int offsetSeconds, bool expected)
    {
        var cutoff = new DateTime(2026, 6, 11, 0, 0, 0, DateTimeKind.Utc);
        var lastActivity = cutoff.AddSeconds(offsetSeconds);

        Assert.Equal(expected, StrikeOffReminderCalculator.IsDue(lastActivity, cutoff));
    }
}
