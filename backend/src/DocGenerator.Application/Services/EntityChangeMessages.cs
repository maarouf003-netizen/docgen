namespace DocGenerator.Application.Services;

/// <summary>
/// النصوص المعيارية لعمليات تغيير سجل الجهات العامة (إعادة تسمية / دمج / توحيد تسمية / حلول):
/// تُركَّز هنا لتُستخدم حرفيًا في قنوات الإشعار الثلاث — وقوعات الملفات
/// (<see cref="DocumentOccurrence.Details"/>) وتنبيه المحامين وتنبيه رؤساء الأقسام —
/// ضمانًا لتطابق النصوص إثباتيًا (حسب `AGENTS.md` و`8.9` من
/// `ENTITY_REVIEW_RESTRUCTURE_PLAN.md`).
/// </summary>
public static class EntityChangeMessages
{
    /// <summary>
    /// لاحقة المرجع «{النوع} رقم {الرقم} [بتاريخ {التاريخ}]» — تُستخدم مع <see cref="Compose"/>
    /// لإلحاق «بموجب» فقط عند وجود مرسوم؛ تُرجع فارغًا عندما يكون نوع المرسوم أو رقمه خاليًا/أبيضًا
    /// (فارغًا، كما في توحيد التسمية بعد حذف حقول المرسوم من نافذته، أو ناقصًا جزئيًا — فلا يُبنى رابط مكسور).
    /// </summary>
    public static string DecreeSuffix(string decreeKind, string decreeNumber, DateTime? decreeDate)
    {
        var kind = decreeKind?.Trim();
        var number = decreeNumber?.Trim();
        // لا يُبنى رابط ناقص: الفارغ (توحيد التسمية) أو الناقص جزئيًا (نوع بلا رقم أو عكسه) → بلا لاحقة.
        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(number))
            return string.Empty;
        var datePart = decreeDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        return datePart is null
            ? $"{kind} رقم {number}"
            : $"{kind} رقم {number} بتاريخ {datePart}";
    }

    /// <summary>
    /// تراكيب نص الحدث مع لاحقة المرجع؛ عند غياب المرسوم (لاحقة فارغة) تُعاد الجملة كما هي
    /// دون «بموجب» العائمة — ونصوص الأفعال الثلاثة الملزِمة بالمرسوم لا تتأثر لأن حقولها إلزامية.
    /// </summary>
    private static string Compose(string message, string decreeKind, string decreeNumber, DateTime? decreeDate)
    {
        var suffix = DecreeSuffix(decreeKind, decreeNumber, decreeDate);
        return string.IsNullOrWhiteSpace(suffix)
            ? message
            : $"{message} بموجب {suffix}";
    }

    // ── إعادة تسمية ──

    /// <summary>وقعة تغيير الاسم على الملفات المتأثرة.</summary>
    public static string RenameOccurrence(string oldCanonical, string newCanonical, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => Compose($"تم تعديل اسم الجهة من «{oldCanonical}» إلى «{newCanonical}»", decreeKind, decreeNumber, decreeDate);

    /// <summary>التنبيه العام لكل المحامين (إعادة تسمية).</summary>
    public static string RenameLawyersAlert(string oldCanonical, string newCanonical, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => Compose($"يرجى ملاحظة أنه تم تعديل اسم \"{oldCanonical}\" الى \"{newCanonical}\"", decreeKind, decreeNumber, decreeDate);

    /// <summary>تنبيه رؤساء الأقسام (إعادة تسمية).</summary>
    public static string RenameHeadsAlert(string oldCanonical, string newCanonical, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => Compose($"تم تعديل اسم الجهة \"{oldCanonical}\" الى \"{newCanonical}\"", decreeKind, decreeNumber, decreeDate);

    // ── دمج ──

    /// <summary>وقعة الدمج على الملفات المتأثرة.</summary>
    public static string MergeOccurrence(string absorbedNames, string survivorName, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => Compose($"تم دمج \"{absorbedNames}\" مع \"{survivorName}\"", decreeKind, decreeNumber, decreeDate);

    /// <summary>التنبيه العام لكل المحامين (دمج).</summary>
    public static string MergeLawyersAlert(string absorbedNames, string survivorName, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => Compose($"يرجى ملاحظة أنه تم دمج \"{absorbedNames}\" مع \"{survivorName}\"", decreeKind, decreeNumber, decreeDate);

    /// <summary>تنبيه رؤساء الأقسام (دمج).</summary>
    public static string MergeHeadsAlert(string absorbedNames, string survivorName, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => Compose($"تم دمج \"{absorbedNames}\" مع \"{survivorName}\"", decreeKind, decreeNumber, decreeDate);

    // ── توحيد التسمية (N←1) ──

    /// <summary>وقعة توحيد التسمية على الملفات المتأثرة (تُوحَّد تسميات عدة لهوية واحدة معتمدة).</summary>
    public static string UnifyOccurrence(string unifiedNames, string canonicalName, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => Compose($"تم توحيد تسمية \"{unifiedNames}\" إلى «{canonicalName}»", decreeKind, decreeNumber, decreeDate);

    /// <summary>التنبيه العام لكل المحامين (توحيد تسمية).</summary>
    public static string UnifyLawyersAlert(string unifiedNames, string canonicalName, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => Compose($"يرجى ملاحظة أنه تم توحيد تسمية \"{unifiedNames}\" إلى «{canonicalName}»", decreeKind, decreeNumber, decreeDate);

    /// <summary>تنبيه رؤساء الأقسام (توحيد تسمية).</summary>
    public static string UnifyHeadsAlert(string unifiedNames, string canonicalName, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => Compose($"تم توحيد تسمية \"{unifiedNames}\" إلى «{canonicalName}»", decreeKind, decreeNumber, decreeDate);

    // ── حلول (الإلغاء والاستبدال) ──

    /// <summary>وقعة الحلول على الملفات المتأثرة.</summary>
    public static string AbolishOccurrence(string newCanonical, string abolishedName, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => Compose($"حلّت الجهة «{newCanonical}» محل «{abolishedName}»", decreeKind, decreeNumber, decreeDate);

    /// <summary>التنبيه العام لكل المحامين (حلول).</summary>
    public static string AbolishLawyersAlert(string newCanonical, string abolishedNames, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => Compose($"يرجى ملاحظة أنه تم حلول \"{newCanonical}\" محل \"{abolishedNames}\"", decreeKind, decreeNumber, decreeDate);

    /// <summary>تنبيه رؤساء الأقسام (حلول).</summary>
    public static string AbolishHeadsAlert(string newCanonical, string abolishedNames, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => Compose($"تم حلول \"{newCanonical}\" محل \"{abolishedNames}\"", decreeKind, decreeNumber, decreeDate);
}
