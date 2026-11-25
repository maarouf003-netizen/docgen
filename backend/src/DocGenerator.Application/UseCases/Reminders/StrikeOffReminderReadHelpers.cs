using DocGenerator.Application.Common;

namespace DocGenerator.Application.UseCases.Reminders;

/// <summary>
/// تطبيع ترقيم قوائم «تذكيرات الشطب» — نُقل حرفيًا من خدمة التذكيرات بعد إفراغها:
/// صفحة ≥ 1 وحجم صفحة داخل [1, MaxPerPage] بلا رفض طلب كامل. مصدر واحد للاستعلامات الثلاثة.
/// </summary>
internal static class StrikeOffReminderReadHelpers
{
    /// <summary>تطبيع الترقيم: صفحة ≥ 1 وحجم صفحة داخل [1, MaxPerPage] — بلا رفض طلب كامل.</summary>
    internal static (int Page, int PerPage) NormalizePaging(int page, int perPage)
    {
        var safePage = page < 1 ? 1 : page;
        var safePerPage = perPage < 1
            ? StrikeOffReminderCalculator.DefaultPerPage
            : Math.Min(perPage, StrikeOffReminderCalculator.MaxPerPage);
        return (safePage, safePerPage);
    }
}
