using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Common;

/// <summary>
/// حساب موعد استحقاق التذكير لإجراء (ملف أو استئناف) — المصدر الوحيد للخوارزمية
/// المشتركة بين بطاقة تذكيرات الملفات وتذكيرات الاستئنافات:
/// تاريخ الإجراء + مدة التذكير، وإن غاب التاريخ فتاريخ إنشاء الإجراء + المدة.
/// </summary>
public static class ActionReminderCalculator
{
    /// <summary>تاريخ الاستحقاق = تاريخ الإجراء + مدة التذكير، وإن غاب فتاريخ الإنشاء + المدة.</summary>
    public static DateTime ComputeDueDate(string? actionDate, string? duration, DateTime createdAt)
    {
        var baseDate = ActionDateParser.TryParse(actionDate) ?? createdAt;
        return baseDate.Date.AddDays(DurationDays(duration));
    }

    /// <summary>مدة التذكير بالأيام بالخيارات المعروفة في الواجهة، وغير المعروف صفر.</summary>
    public static int DurationDays(string? duration) => duration switch
    {
        "3 أيام" => 3,
        "أسبوع" => 7,
        "أسبوعين" => 14,
        "شهر" => 30,
        _ => 0,
    };
}
