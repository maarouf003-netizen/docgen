using System.Globalization;
using DocGenerator.Application.Common;

namespace DocGenerator.Application.DTOs;

/// <summary>
/// التذكير الشخصي الحر — التواريخ نصوص `yyyy-MM-dd` (قاعدة `Date Fields Rule`):
/// تُقبل صيغة `ISO` عند الإرسال (التاريخ من التقويم مباشرة لا كنص حر).
/// </summary>
public record PersonalReminderDto(
    int Id,
    string Title,
    string? Notes,
    string DueDate,
    string? Color,
    string Recurrence,
    string? RecurrenceEnd,
    bool IsArchived,
    IReadOnlyList<string> CompletedOccurrenceKeys,
    DateTime CreatedAt);

/// <summary>إنشاء تذكير شخصي — العنوان والتاريخ والتكرار إلزامية، والبقية اختيارية.</summary>
public record CreatePersonalReminderRequest(
    string Title,
    string? Notes,
    string DueDate,
    string? Color,
    string Recurrence,
    string? RecurrenceEnd);

/// <summary>تعديل تذكير شخصي — كل الحقول اختيارية التحديث، والأرشفة صريحة.</summary>
public record UpdatePersonalReminderRequest(
    string? Title,
    string? Notes,
    string? DueDate,
    string? Color,
    string? Recurrence,
    string? RecurrenceEnd,
    bool? IsArchived);

/// <summary>تعليم تكرارٍ ما منجزًا أو إعادته — التاريخ مفتاح `yyyy-MM-dd` صارم.</summary>
public record SetOccurrenceRequest(
    string OccurrenceDate,
    bool Done);

public static class PersonalReminderDates
{
    /// <summary>تنسيق تاريخ الاستجابة `yyyy-MM-dd` (InvariantCulture).</summary>
    public static string Format(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// تحليل تاريخ بنفس عقد تواريخ النظام (`ActionDateParser`: الصيغ السبع + تطبيع
    /// الأرقام العربية) — يرمي `ArgumentException` بالرسالة المعتمدة عند الفشل.
    /// </summary>
    public static DateTime ParseDay(string? value, string fieldName)
    {
        var parsed = ActionDateParser.TryParse(value);
        if (parsed is null)
            throw new ArgumentException($"{fieldName} غير صالح — استخدم مثال: 1/8/2026");
        return parsed.Value.Date;
    }
}
