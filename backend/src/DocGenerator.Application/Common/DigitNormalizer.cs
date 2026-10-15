namespace DocGenerator.Application.Common;

/// <summary>
/// تطبيع الأرقام: ٠-٩ (عربية-هندية U+0660-U+0669) وفارسية ۰-۹ (U+06F0-U+06F9)
/// ← 0-9 لاتينية. ArabicNameNormalizer لا يحوّل الأرقام والواجهة تطبّع التواريخ
/// فقط، فيلزم هذا المساعد على FileNumber/FileYear (وموصى به مركزيًا للإنشاء/التعديل).
/// يُغلّف ArabicDigitNormalizer القائم (مصدر واحد) + Trim.
/// </summary>
public static class DigitNormalizer
{
    /// <summary>تطبيع الأرقام العربية/الفارسية إلى لاتينية + Trim. null/فارغ يُعاد كما هو.</summary>
    public static string? NormalizeDigits(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        return ArabicDigitNormalizer.Normalize(value).Trim();
    }

    /// <summary>
    /// تطبيع الأرقام مع توحيد الفارغ إلى null — لأعمدة أرقام الملفات المشمولة بفهارس
    /// فريدة جزئية تستثني NULL فقط: الصف بلا رقم يجب ألا يدخل نطاق الفهرس أصلًا
    /// (وإلا عومل `''` رقمًا فعليًا وخاطر برفض `409` كاذب).
    /// </summary>
    public static string? NormalizeDigitsOrNull(string? value)
    {
        var normalized = NormalizeDigits(value);
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
