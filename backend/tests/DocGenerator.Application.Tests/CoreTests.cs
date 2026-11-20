using DocGenerator.Application.Services;

namespace DocGenerator.Application.Tests;

/// <summary>
/// منطقة زمن ثابتة للاختبارات تُمثّل منطقة النظام الإنتاجية (آسيا/دمشق) بفارق +03،
/// بلا اعتماد على tzdata على آلة التطوير: سنة القرار في الاختبارات = UTC + 3 ساعات.
/// </summary>
public static class TestClock
{
    /// <summary>لحظة مرجعية ثابتة لكل الاختبارات: 2026-08-01 09:00 UTC (= 12:00 دمشق).</summary>
    public static DateTimeOffset UtcNow { get; } = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

    public static TimeZoneInfo TimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone(
        "Asia/Damascus",
        TimeSpan.FromHours(3),
        "سوريا (توقيت ثابت)",
        "سوريا (توقيت ثابت)");

    /// <summary>ساعة ثابتة تُحقن في الخدمات بدل TestClock.Provider.</summary>
    public static TimeProvider Provider { get; } = new FixedTimeProvider(UtcNow);

    /// <summary>تاريخ اليوم في منطقة الاختبار (2026-08-01) — بديل التاريخ المحلي غير الحتمي.</summary>
    public static DateTime Today =>
        TimeZoneInfo.ConvertTime(UtcNow, TimeZone).Date;

    public static int CurrentYear => Today.Year;

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

[Trait("Category", "Unit")]
public class PasswordHasherTests
{
    [Fact]
    public void Hash_ThenVerify_Succeeds()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("123456");
        Assert.True(hasher.Verify("123456", hash));
        Assert.False(hasher.Verify("wrong", hash));
    }

    [Fact]
    public void Verify_LegacySha256Hash_Succeeds()
    {
        // صيغة قديمة بدون ملح (64 حرف hex)
        var legacy = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("123456"))).ToLowerInvariant();
        var hasher = new PasswordHasher();
        Assert.True(hasher.Verify("123456", legacy));
    }

    [Fact]
    public void Verify_EmptyOrGarbage_ReturnsFalse()
    {
        var hasher = new PasswordHasher();
        Assert.False(hasher.Verify("x", ""));
        Assert.False(hasher.Verify("x", "garbage"));
        Assert.False(hasher.Verify("x", "abc:def"));
    }
}

[Trait("Category", "Unit")]
public class NumberToWordsTests
{
    [Theory]
    [InlineData(0, "صفر")]
    [InlineData(5, "خمسة")]
    [InlineData(12, "اثنا عشر")]
    [InlineData(45, "خمسة وأربعون")]
    [InlineData(130, "مائة وثلاثون")]
    [InlineData(1500, "ألف وخمسمائة")]
    public void Convert_Various_MatchesExpected(long value, string expected)
    {
        Assert.Equal(expected, NumberToWords.Convert(value));
    }

    [Fact]
    public void Convert_LargeNumber_ProducesNonEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(NumberToWords.Convert(1_234_567)));
    }
}
