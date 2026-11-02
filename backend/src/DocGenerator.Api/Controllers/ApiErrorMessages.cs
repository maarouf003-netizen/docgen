using DocGenerator.Api.Middleware;

namespace DocGenerator.Api.Controllers;

/// <summary>
/// رسائل أخطاء العملاء على حدود HTTP: تُطهَّر من ذيول إطار العمل الإنجليزية قبل
/// وضعها في الجسم <c>{ message }</c>.
///
/// **المشكلة التي يحلها:** <see cref="ArgumentException.Message"/> يُلحق به .NET
/// ذيلًا إنجليزيًا عندما يُمرَّر اسم معامل —
/// <c>"نص عربي (Parameter 'request')"</c> — و<see cref="ArgumentOutOfRangeException"/>
/// يضيف فوقه سطرًا ثانيًا <c>Actual value was 0.</c>. هذا النص الإطاري كان يصل
/// المستخدم عبر جسم الاستجابة فيلتصق بكل رسالة عربية، ويخالف عرف هذا المستودع
/// (رسائل عرض عربية بلا إنجليزية إطارية — انظر <c>client.ts</c> و<see
/// cref="DocGenerator.Application.Common.AlertPagingException"/>).
///
/// **لماذا هنا لا في كل موضع رمي:** الذيل يضيفه الإطار عند قراءة
/// <see cref="Exception.Message"/>، فمكان إزالته الوحيد الصحيح هو حدود الاستجابة.
/// تطهير المصدر كان سيتطلب تعديل كل <c>throw</c> وإعادة الخطأ عند أي موضع جديد.
///
/// **السياسة نفسها لا تُكرَّر:** التحويل الفعلي في
/// <see cref="LogSanitizer.SanitizeForResponse"/> (جرد ذيول الإطار ← تسطيح ← تجريد
/// أسرار ← قصّ)، وهو المسار الذي يستخدمه معالج الاستثناءات العام أيضًا — فالحدّان
/// (استثناء مُعالَج في المتحكم، واستثناء وصل إلى الوسيط) يخضعان لقاعدة واحدة.
///
/// **ما لا يمسّه:** الرسائل العربية الخالصة تُعاد كما هي، وتُسطَّح الأسطر (سياسة
/// منع تزوير السطور نفسها). وتُترك رسائل الإطار الخالصة (كـ<c>Value cannot be null.</c>
/// من <see cref="ArgumentNullException"/>) بلا ترجمة عمدًا: تلك عيوب برمجة داخلية
/// لا مدخلات مستخدم، ووصولها إلى <c>400</c> يجب أن يُكتشف في الاختبارات لا أن
/// يُقنَّع برسالة عامة.
/// </summary>
public static class ApiErrorMessages
{
    /// <summary>رسالة بديلة إن صار النص فارغًا بعد التطهير (لا جسم بلا رسالة).</summary>
    public const string Fallback = "حدث خطأ في الطلب — تحقق من الحقول وأعد المحاولة";

    /// <summary>
    /// يُرجع رسالة الاستثناء جاهزة للعرض: بلا ذيول إطار العمل وبلا أسرار،
    /// وبالرسالة البديلة إن لم يبقَ نص.
    /// </summary>
    public static string From(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var message = LogSanitizer.SanitizeForResponse(error.Message).Trim();
        return message.Length == 0 ? Fallback : message;
    }
}
