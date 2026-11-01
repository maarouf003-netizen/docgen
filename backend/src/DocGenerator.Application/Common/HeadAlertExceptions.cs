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

/// <summary>
/// مرجع مرتبط بالتنبيه (ملف/إنابة/استئناف/دائرة/فرع/منشئ) لم يعد موجودًا وقت المعالجة —
/// حُذف بين التسجيل والمعالجة، أو كان غير صالح أصلًا.
///
/// استثناء **داخلي**: لا يترجم إلى HTTP. المشغّل يعامله كـ<code>skipped</code>
/// (<c>missing_reference</c>) بلا إعادة ولا <c>failed</c> (الخطة §8.1).
/// </summary>
public sealed class HeadAlertMissingReferenceException : Exception
{
    public HeadAlertMissingReferenceException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// مستلم التعاقب (تجاوز <c>TargetUserId</c>) غير فعّال أو مفقود ولا خلف معروف له.
///
/// استثناء **داخلي**: المشغّل يعامله كـ<code>skipped</code> (<c>recipient_inactive</c>)
/// بدل التخمين أو الإعادة (الخطة §8.1).
/// </summary>
public sealed class HeadAlertRecipientInactiveException : Exception
{
    public HeadAlertRecipientInactiveException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// ترقيم صفحات غير صالح في أسطح التنبيهات (`P3-6`): يترجمه المتحكم إلى <c>400</c>
/// عبر <see cref="ArgumentException"/>، ورسالته عربية **نظيفة** بلا ذيل معامل.
///
/// يُستخدم بدل <see cref="ArgumentOutOfRangeException"/> مع اسم معامل، لأن .NET
/// يُلحق بـ<see cref="Exception.Message"/> سطرًا إنجليزيًا (<c>Parameter name: Page</c>)
/// يصل المستخدم عبر <c>{ message }</c>، وهو ما يخالف عرف الرسائل العربية في هذا المستودع.
/// </summary>
public sealed class AlertPagingException : ArgumentException
{
    public AlertPagingException(string message)
        : base(message)
    {
    }
}
