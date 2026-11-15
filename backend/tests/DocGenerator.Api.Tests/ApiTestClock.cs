namespace DocGenerator.Api.Tests;

/// <summary>
/// ساعة اختبار ثابتة لتكامل الـAPI (P1-02b): نفس مرجع اختبارات التطبيق —
/// 2026-08-01 09:00 UTC (= 12:00 دمشق) — فلا تعتمد النتائج على يوم التشغيل.
/// </summary>
public static class ApiTestClock
{
    public static DateTimeOffset UtcNow { get; } = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

    public static TimeProvider Provider { get; } = new FixedTimeProvider(UtcNow);

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone(
        "Asia/Damascus",
        TimeSpan.FromHours(3),
        "سوريا (توقيت ثابت)",
        "سوريا (توقيت ثابت)");

    public static DateTime Today =>
        TimeZoneInfo.ConvertTime(UtcNow, Zone).Date;

    public static int CurrentYear => Today.Year;

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
