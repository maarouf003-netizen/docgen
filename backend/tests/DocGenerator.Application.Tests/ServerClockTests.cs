using DocGenerator.Application.Common;
using Microsoft.Extensions.Time.Testing;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات مصدر الزمن المركزي: حدود السنة (آخر لحظة في سنة وأول لحظة في التالية وفق
/// منطقة النظام المقررة، هنا ثابتة +03)، صيغة TodayString، وتحويل منطقة الزمن.
/// </summary>
public class ServerClockTests
{
    [Fact]
    public void CurrentYear_InDamascusLocal_BeforeYearEnd_ReturnsCurrentYear()
    {
        // UTC 2026-12-31 20:59 = دمشق 2026-12-31 23:59 (لا تزال 2026).
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 12, 31, 20, 59, 0, TimeSpan.Zero));

        Assert.Equal(2026, ServerClock.CurrentYear(clock, TestClock.TimeZone));
    }

    [Fact]
    public void CurrentYear_InDamascusLocal_AfterYearEnd_ReturnsNextYear()
    {
        // UTC 2026-12-31 21:01 = دمشق 2027-01-01 00:01 (دخلت 2027).
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 12, 31, 21, 1, 0, TimeSpan.Zero));

        Assert.Equal(2027, ServerClock.CurrentYear(clock, TestClock.TimeZone));
    }

    [Fact]
    public void Now_ReturnsUtcConvertedToConfiguredZone()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 12, 31, 21, 1, 0, TimeSpan.Zero));

        var now = ServerClock.Now(clock, TestClock.TimeZone);

        Assert.Equal(new DateTime(2027, 1, 1, 0, 1, 0), now);
    }

    [Fact]
    public void TodayString_FormatsInDayMonthYear_WithInvariantDigits()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 12, 31, 21, 1, 0, TimeSpan.Zero));

        Assert.Equal("01/01/2027", ServerClock.TodayString(clock, TestClock.TimeZone));
        Assert.Equal("2027-01-01", ServerClock.TodayString(clock, TestClock.TimeZone, "yyyy-MM-dd"));
    }

    [Fact]
    public void ResolveTimeZone_UnknownOrNullId_AlwaysReturnsZone()
    {
        Assert.NotNull(ServerClock.ResolveTimeZone(null));
        Assert.NotNull(ServerClock.ResolveTimeZone("No/Such/Zone"));
    }

    [Fact]
    public void ResolveTimeZone_KnownId_ReturnsZoneWithExpectedOffset()
    {
        var utc = ServerClock.ResolveTimeZone("UTC");

        Assert.NotNull(utc);
        Assert.Equal(TimeSpan.Zero, utc.GetUtcOffset(DateTimeOffset.UtcNow));
    }
}