namespace DocGenerator.Domain.Enums;

/// <summary>
/// قيم التذكير الشخصي الحر المغلقة: التكرار والألوان والحدود.
/// النصوص عربية مطابقة للغة تذكيرات الملفات (`ReminderColor`: أحمر/بنفسجي/أصفر) مع لون رابع مميز.
/// </summary>
public static class PersonalReminderCatalog
{
    public const string RecurrenceOnce = "مرة واحدة";
    public const string RecurrenceDaily = "يومي";
    public const string RecurrenceWeekly = "أسبوعي";
    public const string RecurrenceMonthly = "شهري";

    public static readonly string[] Recurrences =
    [
        RecurrenceOnce,
        RecurrenceDaily,
        RecurrenceWeekly,
        RecurrenceMonthly,
    ];

    public const string ColorRed = "أحمر";
    public const string ColorViolet = "بنفسجي";
    public const string ColorAmber = "أصفر";
    public const string ColorEmerald = "زمردي";

    public static readonly string[] Colors =
    [
        ColorRed,
        ColorViolet,
        ColorAmber,
        ColorEmerald,
    ];

    public const int TitleMaxLength = 200;
    public const int NotesMaxLength = 2000;
    public const int CompletedKeysMaxLength = 2000;

    /// <summary>سقف التذكيرات النشطة (غير المؤرشفة) للمستخدم الواحد — منع الإساءة.</summary>
    public const int MaxActivePerUser = 200;
}
