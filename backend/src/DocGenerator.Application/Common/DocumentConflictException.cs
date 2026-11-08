namespace DocGenerator.Application.Common;

/// <summary>
/// تعارض يستوجب إظهار سببه للمستخدم: يستجيب المعالج الموحد (GlobalExceptionHandler)
/// له بـ 409 Conflict برسالة مفهومة في الإنتاج بدل 500 العامة. استعمالان:
/// (1) تعارض التفاؤلية أثناء النقل المتزامن — تغيّر المحامي المختص للملف بين
/// قراءته وتحديثه؛ (2) نفاد إعادة توليد رقم مراسلة فريد في الخدمة (سباق تسطيرين
/// متزامنين) — الرسالة «تعذر توليد رقم فريد…» تصل الواجهة بدل «خطأ غير متوقع».
/// المنشأ الداخلي (إن وُجد) يبقى سلسلة الاستثناء لأغراض التشخيص وحده.
/// </summary>
public sealed class DocumentConflictException : Exception
{
    public DocumentConflictException(string message) : base(message) { }

    public DocumentConflictException(string message, Exception inner) : base(message, inner) { }

    /// <summary>رمز تعارض اختياري للواجهة (مثل number_duplicate) — لا يُغيّر الرسالة.</summary>
    public string? Code { get; init; }
}

/// <summary>
/// تحذير إعادة استخدام رقم أساس (طبقة لينة): لا يُكتب شيء، ويُعاد الرمز للعميل
/// مع رسالة تحمل النطاق فقط (السنة/النوع/الدائرة) بلا معرّف/اسم/حالة الملف الآخر.
/// </summary>
public sealed class NumberReuseWarningException : Exception
{
    public NumberReuseWarningException(string message, string warningToken) : base(message)
    {
        WarningToken = warningToken;
    }

    public string WarningToken { get; }
}
