namespace DocGenerator.Application.Common;

/// <summary>
/// تعارض تكرار على فهرس <c>DedupeKey</c> الفريد الجزئي (الخطة §0.1-11 و§6.4).
///
/// استثناء **داخلي** في طبقة التطبيق: لا يترجم إلى رمز HTTP ولا إلى <c>409</c> — لا
/// يوجد مسار HTTP نظامي. المشغّل يعيد الرسالة مرة واحدة كاملة ثم يضعها <c>failed</c>.
/// </summary>
public sealed class HeadAlertConflictException : Exception
{
    public HeadAlertConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// لا يوجد مستلمون مؤهّلون للتنبيه (فرع بلا محامين/رؤساء، أو مستلم غير فعّال).
///
/// استثناء **داخلي**: لا يترجم إلى HTTP. المشغّل يعامله كـ<code>skipped</code>
/// (<c>no_recipients</c>) لا كخطأ قابل لإعادة المحاولة.
/// </summary>
public sealed class HeadAlertNoRecipientsException : Exception
{
    public HeadAlertNoRecipientsException(string message)
        : base(message)
    {
    }
}
