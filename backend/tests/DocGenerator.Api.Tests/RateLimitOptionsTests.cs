using DocGenerator.Application.Common;

namespace DocGenerator.Api.Tests;

/// <summary>
/// `P2-08`: حدود المعدل تُتحقق عند الإقلاع — لا تسطيح صامت لصفر/سالب ولا نافذة غير منطقية.
/// </summary>
[Trait("Category", "Unit")]
public class RateLimitOptionsTests
{
    [Fact]
    public void Defaults_AreValid() => new RateLimitOptions().Validate();

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void MaxLoginAttempts_OutOfRange_Refuses(int value)
        => AssertRefused(new RateLimitOptions { MaxLoginAttempts = value }, "RateLimiting:MaxLoginAttempts");

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void WindowMinutes_OutOfRange_Refuses(int value)
        => AssertRefused(new RateLimitOptions { WindowMinutes = value }, "RateLimiting:WindowMinutes");

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void PerMinuteLimits_NonPositive_Refuse(int value)
    {
        AssertRefused(new RateLimitOptions { LoginIpPerMinute = value }, "RateLimiting:LoginIpPerMinute");
        AssertRefused(new RateLimitOptions { GeneralAnonPerMinute = value }, "RateLimiting:GeneralAnonPerMinute");
        AssertRefused(new RateLimitOptions { GeneralAuthPerMinute = value }, "RateLimiting:GeneralAuthPerMinute");
        AssertRefused(new RateLimitOptions { ExpensivePerMinute = value }, "RateLimiting:ExpensivePerMinute");
        AssertRefused(new RateLimitOptions { StatsReadPerMinute = value }, "RateLimiting:StatsReadPerMinute");
        AssertRefused(new RateLimitOptions { PasswordPerMinute = value }, "RateLimiting:PasswordPerMinute");
        AssertRefused(new RateLimitOptions { CspReportPerMinute = value }, "RateLimiting:CspReportPerMinute");
    }

    private static void AssertRefused(RateLimitOptions options, string key)
    {
        var exception = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
    }
}
