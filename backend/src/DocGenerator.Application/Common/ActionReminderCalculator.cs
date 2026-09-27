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
        => TryComputeDueDate(actionDate, duration, createdAt).DueDate;

    /// <summary>
    /// يحسب تاريخ الاستحقاق مع وسم الجودة: مشتبه عندما يكون تاريخ الإجراء غير فارغ ولا يُحلَّل،
    /// أو المدة غير فارغة وغير معروفة — أي عندما سقط الحساب إلى احتياط كان صامتًا (تاريخ
    /// الإنشاء أو صفر أيام). الكتابة الجديدة مرفوضة عند الإدخال، فالوسم يكشف البيانات القديمة.
    /// </summary>
    public static (DateTime DueDate, bool IsSuspect) TryComputeDueDate(string? actionDate, string? duration, DateTime createdAt)
    {
        // تطبيع واحد للمقارنة والحساب معًا: مدة بمسافات زائدة كانت تُحكم «سليمة» (بعد القصّ)
        // وتُحسب `0` أيام (بلا قصّ) — تناقض صامت بين الوسم والناتج.
        var trimmedDate = actionDate?.Trim();
        var trimmedDuration = duration?.Trim();
        var parsed = ActionDateParser.TryParse(trimmedDate);
        var suspect = (!string.IsNullOrWhiteSpace(trimmedDate) && parsed is null)
            || (!string.IsNullOrWhiteSpace(trimmedDuration) && !ValidDurations.Contains(trimmedDuration));
        var baseDate = parsed ?? createdAt;
        return (baseDate.Date.AddDays(DurationDays(trimmedDuration)), suspect);
    }

    /// <summary>نوعا الإجراء/الملاحظة المقبولان في الواجهة — المصدر الوحيد للحقيقة.</summary>
    public static readonly IReadOnlySet<string> ValidActionTypes = new HashSet<string>
    {
        "action", "note",
    };

    /// <summary>
    /// يتحقق من نوع الإجراء عند الكتابة. الفارغ مباح (يُفترض `action` عند المستدعي)، وغير
    /// الصالح يُرفض — بدل تخزين نوع ميت لا تعرفه مسارات العرض والبذور.
    /// </summary>
    public static void ValidateActionType(string? value, string fieldName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return;
        if (!ValidActionTypes.Contains(trimmed))
            throw new ArgumentException($"{fieldName} غير صالح");
    }

    /// <summary>مدد التذكير المقبولة في الواجهة — المصدر الوحيد للحقيقة كتابةً وقراءةً.</summary>
    public static readonly IReadOnlySet<string> ValidDurations = new HashSet<string>
    {
        "3 أيام", "أسبوع", "أسبوعين", "شهر",
    };

    /// <summary>ألوان التذكير المقبولة في الواجهة — المصدر الوحيد للحقيقة كتابةً وقراءةً.</summary>
    public static readonly IReadOnlySet<string> ValidColors = new HashSet<string>
    {
        "أحمر", "بنفسجي", "أصفر",
    };

    /// <summary>
    /// يتحقق من مدة التذكير ولونه عند الكتابة. الفارغ مباح (بلا تذكير)، وغير الصالح يُرفض
    /// برسالة عربية — بدل تخزين قيمة ميتة تُفسَّر `0` أيام (استحقاق فوري كاذب) وقت القراءة.
    /// </summary>
    public static void ValidateReminder(string? duration, string? color)
    {
        var trimmedDuration = duration?.Trim();
        var trimmedColor = color?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedDuration) && !ValidDurations.Contains(trimmedDuration))
            throw new ArgumentException("مدة تذكير غير صالحة");
        if (!string.IsNullOrWhiteSpace(trimmedColor) && !ValidColors.Contains(trimmedColor))
            throw new ArgumentException("لون تذكير غير صالح");
    }

    /// <summary>
    /// يتحقق من تاريخ إجراء غير فارغ عند الكتابة. الفارغ مباح (يُفسَّر وقت القراءة بتاريخ
    /// الإنشاء قصدًا)، وغير الصالح يُرفض برسالة تحمل اسم الحقل — بدل تخزين نص ميت يسقط
    /// بصمت إلى تاريخ الإنشاء وقت القراءة.
    /// </summary>
    public static void ValidateActionDate(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        if (ActionDateParser.TryParse(value) is null)
            throw new ArgumentException($"{fieldName} غير صالح — استخدم مثال: 1/8/2026");
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
