namespace DocGenerator.Application.Services;

/// <summary>
/// النصوص المعيارية لعمليات تغيير سجل الجهات العامة (إعادة تسمية / دمج / حلول):
/// تُركَّز هنا لتُستخدم حرفيًا في قنوات الإشعار الثلاث — وقوعات الملفات
/// (<see cref="DocumentOccurrence.Details"/>) وتنبيه المحامين وتنبيه رؤساء الأقسام —
/// ضمانًا لتطابق النصوص إثباتيًا (حسب `AGENTS.md` و`8.9` من
/// `ENTITY_REVIEW_RESTRUCTURE_PLAN.md`).
/// </summary>
public static class EntityChangeMessages
{
    /// <summary>سقف لاحقة المرجع «بموجب {النوع} رقم {الرقم} بتاريخ {التاريخ}» — موحّد بين النبيه والوقعة والسجل.</summary>
    public static string DecreeSuffix(string decreeKind, string decreeNumber, DateTime? decreeDate)
    {
        var datePart = decreeDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        return datePart is null
            ? $"{decreeKind} رقم {decreeNumber}"
            : $"{decreeKind} رقم {decreeNumber} بتاريخ {datePart}";
    }

    // ── إعادة تسمية ──

    /// <summary>وقعة تغيير الاسم على الملفات المتأثرة.</summary>
    public static string RenameOccurrence(string oldCanonical, string newCanonical, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => $"تم تعديل اسم الجهة من «{oldCanonical}» إلى «{newCanonical}» بموجب {DecreeSuffix(decreeKind, decreeNumber, decreeDate)}";

    /// <summary>التنبيه العام لكل المحامين (إعادة تسمية).</summary>
    public static string RenameLawyersAlert(string oldCanonical, string newCanonical, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => $"يرجى ملاحظة أنه تم تعديل اسم \"{oldCanonical}\" الى \"{newCanonical}\" بموجب {DecreeSuffix(decreeKind, decreeNumber, decreeDate)}";

    /// <summary>تنبيه رؤساء الأقسام (إعادة تسمية).</summary>
    public static string RenameHeadsAlert(string oldCanonical, string newCanonical, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => $"تم تعديل اسم الجهة \"{oldCanonical}\" الى \"{newCanonical}\" بموجب {DecreeSuffix(decreeKind, decreeNumber, decreeDate)}";

    // ── دمج ──

    /// <summary>وقعة الدمج على الملفات المتأثرة.</summary>
    public static string MergeOccurrence(string absorbedNames, string survivorName, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => $"تم دمج \"{absorbedNames}\" مع \"{survivorName}\" بموجب {DecreeSuffix(decreeKind, decreeNumber, decreeDate)}";

    /// <summary>التنبيه العام لكل المحامين (دمج).</summary>
    public static string MergeLawyersAlert(string absorbedNames, string survivorName, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => $"يرجى ملاحظة أنه تم دمج \"{absorbedNames}\" مع \"{survivorName}\" بموجب {DecreeSuffix(decreeKind, decreeNumber, decreeDate)}";

    /// <summary>تنبيه رؤساء الأقسام (دمج).</summary>
    public static string MergeHeadsAlert(string absorbedNames, string survivorName, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => $"تم دمج \"{absorbedNames}\" مع \"{survivorName}\" بموجب {DecreeSuffix(decreeKind, decreeNumber, decreeDate)}";

    // ── توحيد التسمية (N←1) ──

    /// <summary>وقعة توحيد التسمية على الملفات المتأثرة (تُوحَّد تسميات عدة لهوية واحدة معتمدة).</summary>
    public static string UnifyOccurrence(string unifiedNames, string canonicalName, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => $"تم توحيد تسمية \"{unifiedNames}\" إلى «{canonicalName}» بموجب {DecreeSuffix(decreeKind, decreeNumber, decreeDate)}";

    /// <summary>التنبيه العام لكل المحامين (توحيد تسمية).</summary>
    public static string UnifyLawyersAlert(string unifiedNames, string canonicalName, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => $"يرجى ملاحظة أنه تم توحيد تسمية \"{unifiedNames}\" إلى «{canonicalName}» بموجب {DecreeSuffix(decreeKind, decreeNumber, decreeDate)}";

    /// <summary>تنبيه رؤساء الأقسام (توحيد تسمية).</summary>
    public static string UnifyHeadsAlert(string unifiedNames, string canonicalName, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => $"تم توحيد تسمية \"{unifiedNames}\" إلى «{canonicalName}» بموجب {DecreeSuffix(decreeKind, decreeNumber, decreeDate)}";

    // ── حلول (الإلغاء والاستبدال) ──

    /// <summary>وقعة الحلول على الملفات المتأثرة.</summary>
    public static string AbolishOccurrence(string newCanonical, string abolishedName, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => $"حلّت الجهة «{newCanonical}» محل «{abolishedName}» بموجب {DecreeSuffix(decreeKind, decreeNumber, decreeDate)}";

    /// <summary>التنبيه العام لكل المحامين (حلول).</summary>
    public static string AbolishLawyersAlert(string newCanonical, string abolishedNames, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => $"يرجى ملاحظة أنه تم حلول \"{newCanonical}\" محل \"{abolishedNames}\" بموجب {DecreeSuffix(decreeKind, decreeNumber, decreeDate)}";

    /// <summary>تنبيه رؤساء الأقسام (حلول).</summary>
    public static string AbolishHeadsAlert(string newCanonical, string abolishedNames, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => $"تم حلول \"{newCanonical}\" محل \"{abolishedNames}\" بموجب {DecreeSuffix(decreeKind, decreeNumber, decreeDate)}";
}
