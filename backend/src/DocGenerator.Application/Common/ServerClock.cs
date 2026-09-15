using System.Diagnostics;

namespace DocGenerator.Application.Common;

/// <summary>
/// مصدر الزمن المركزي: كل «قرار سنة» في النظام (سنة التدوير، سنة الإعادة، الرقم الفعّال،
/// تواريخ الرسائل، أسماء ملفات التصدير) يأخذ من الساعة المحقونة (TimeProvider) ومنطقة
/// الزمن (TimeZoneInfo) المقررة في الإعدادات لا من DateTime.Today/Now المباشر، فتنعكس
/// سنة الخادم ومنطقته على الواجهة عبر GET /api/meta/current-year.
/// </summary>
public static class ServerClock
{
    /// <summary>الوقت الحالي في منطقة النظام المقررة.</summary>
    public static DateTime Now(TimeProvider clock, TimeZoneInfo zone)
        => TimeZoneInfo.ConvertTime(clock.GetUtcNow().UtcDateTime, TimeZoneInfo.Utc, zone);

    /// <summary>سنة «قرار السنة» الحالية في منطقة النظام — المصدر الوحيد للسنة الجارية.</summary>
    public static int CurrentYear(TimeProvider clock, TimeZoneInfo zone)
        => Now(clock, zone).Year;

    /// <summary>تاريخ اليوم الحالي في منطقة النظام بصيغة اليوم/الشهر/السنة.</summary>
    public static string TodayString(TimeProvider clock, TimeZoneInfo zone, string format = "dd/MM/yyyy")
        => Now(clock, zone).ToString(format, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// تحويل معرّف منطقة الزمن إلى TimeZoneInfo بأولوية IANA ثم معرف Windows
    /// (البديل ذاته عند غياب قاعدة tzdata)، فاحتياطي ثابت بفارق +03 مطلقًا.
    /// </summary>
    public static TimeZoneInfo ResolveTimeZone(string? id)
    {
        var candidates = new[] { id, "Asia/Damascus", "Syria Standard Time" }
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate!);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        // احتياطي بنيوي: منطقة ثابتة +03 (سوريا) حتى على الأنظمة بلا أي قاعدة مناطق زمنية.
        var fallback = TimeZoneInfo.CreateCustomTimeZone(
            "Asia/Damascus",
            TimeSpan.FromHours(3),
            "سوريا (توقيت ثابت)",
            "سوريا (توقيت ثابت)");
        Debug.WriteLine("ServerClock: لا منطقة زمنية معروفة — رجع الاحتياطي الثابت (+03).");
        return fallback;
    }
}