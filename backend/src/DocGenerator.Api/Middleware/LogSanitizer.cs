using System.Text.RegularExpressions;

namespace DocGenerator.Api.Middleware;

/// <summary>
/// سياسة قصّ/تجريد موحدة لقناتي السجلات (`ClientErrorsController` + `GlobalExceptionHandler` —
/// `SEC-012`): أي نص حر قبل التسجيل أو الرد يُسطَّح (ضد تزوير السطور)، تُجرَّد منه الأسرار
/// (بريد/اعتمادات بصيغة مفتاح=قيمة/توكنات `Bearer`)، ثم يُقصّ لسقفه.
/// يقتصر التجريد على الأنماط غير الملتبسة قصدًا: لا تُمسّ الأرقام المجرّدة (المبالغ والأرقام
/// القضائية تُشخَّص بها الأعطال) ولا الأسماء (تظهر في رسائل `4xx` العربية قصدًا).
/// </summary>
public static partial class LogSanitizer
{
    /// <summary>سقف رسالة `4xx` المنعكسة للمستخدم (تُقصّ الأطول — لا رسائل شرعية بهذا الطول).</summary>
    public const int ResponseMessageLimit = 1000;

    private const string RedactedEmail = "[بريد محجوب]";
    private const string RedactedSecret = "$1=[محجوب]";

    // بريد بصيغة مبسطة بلا ارتداد كارثي: مستخدم@نطاق.لاحقة.
    [GeneratedRegex(@"[\w.+-]+@[\w-]+(?:\.[\w-]+)+", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    // اعتماد بصيغة مفتاح=قيمة أو مفتاح:قيمة (إنكليزية وعربية) — القيمة أول مقطع بلا فراغ.
    [GeneratedRegex(
        @"(?i)(password|passwd|pwd|secret|token|api[_-]?key|authorization|session|كلمة المرور|كلمة السر|الرمز السري|التوكن)\s*[:=]\s*[^\s,;]+",
        RegexOptions.CultureInvariant)]
    private static partial Regex CredentialRegex();

    // توكن `Bearer` في ترويسة مسرّبة بنص خطأ.
    [GeneratedRegex(@"(?i)Bearer\s+[A-Za-z0-9\-._~+/=]+", RegexOptions.CultureInvariant)]
    private static partial Regex BearerRegex();

    // ذيل .NET الحديث الذي يضيفه الإطار إلى `ArgumentException.Message` عند تمرير اسم معامل.
    [GeneratedRegex(@"\s*\(Parameter\s+'[^']*'\)", RegexOptions.CultureInvariant)]
    private static partial Regex ParameterSuffixRegex();

    // ذيل .NET Framework القديم (احتياطي): سطر `Parameter name: request`.
    [GeneratedRegex(@"(?m)^\s*Parameter name:.*$", RegexOptions.CultureInvariant)]
    private static partial Regex LegacyParameterLineRegex();

    // سطر القيمة الفعلية الذي يضيفه `ArgumentOutOfRangeException`.
    [GeneratedRegex(@"(?m)^\s*Actual value was\b.*$", RegexOptions.CultureInvariant)]
    private static partial Regex ActualValueLineRegex();

    /// <summary>تسطيح السطور (`\r`/`\n` ← مسافة) ضد تزوير السطور في السجل النصي.</summary>
    public static string Flatten(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ');

    /// <summary>
    /// يجرد ذيول إطار العمل الإنجليزية من رسالة استثناء قبل عرضها للمستخدم:
    /// <c>(Parameter 'x')</c> و<c>Actual value was …</c> (وسطر <c>Parameter name:</c> القديم).
    /// هذه الذيل يضيفها .NET عند قراءة <see cref="Exception.Message"/> — لا كاتب الرسالة —
    /// فإزالتها هنا تغطي كل المسارات (المتحكمات ومعالج الاستثناءات العام) بلا تعديل
    /// كل موضع رمي. لا تمسّ نصًا عربيًا خالصًا.
    /// </summary>
    public static string StripFrameworkSuffixes(string value) =>
        ActualValueLineRegex()
            .Replace(
                LegacyParameterLineRegex().Replace(
                    ParameterSuffixRegex().Replace(value, string.Empty),
                    string.Empty),
                string.Empty)
            .TrimEnd();

    /// <summary>تجريد الأسرار غير الملتبسة (بريد/اعتمادات/`Bearer`) — النص بلا أسرار يُعاد كما هو.</summary>
    public static string Redact(string value)
    {
        // `Bearer` أولًا: وإلا ابتلعته قاعدة مفتاح=قيمة (`Authorization: Bearer` ← القيمة `Bearer`)
        // وبقي التوكن نفسه مكشوفًا.
        value = BearerRegex().Replace(value, "Bearer [محجوب]");
        value = EmailRegex().Replace(value, RedactedEmail);
        value = CredentialRegex().Replace(value, RedactedSecret);
        return value;
    }

    /// <summary>
    /// يحذف الاستعلام/المقطع من عنوان قبل تسجيله (قد يحمل توكنًا أو معرّفًا حساسًا).
    /// سياسة مشتركة بين إبلاغ أخطاء الواجهة وجامع تقارير <c>CSP</c>.
    /// </summary>
    public static string StripQueryAndFragment(string value)
    {
        var cut = value.IndexOfAny(['?', '#']);
        return cut < 0 ? value : value[..cut];
    }

    /// <summary>قصّ صارم للسقف (نفس سلوك `Clip` السابق: قطع بلا علامة).</summary>
    public static string Clip(string value, int max) =>
        value.Length <= max ? value : value[..max];

    /// <summary>نص حر للسجل: تسطيح ← تجريد ← قصّ (يقبل `null` للمكدس الغائب).</summary>
    public static string? SanitizeForLog(string? value, int max) =>
        value is null ? null : Clip(Redact(Flatten(value)), max);

    /// <summary>
    /// رسالة `4xx` للرد: **جرد ذيول الإطار** ← تسطيح ← تجريد أسرار ← قصّ
    /// (لا تنعكس المدخلات الخام ولا الذيل الإنجليزية أبدًا).
    /// </summary>
    public static string SanitizeForResponse(string message) =>
        Clip(Redact(Flatten(StripFrameworkSuffixes(message))), ResponseMessageLimit);
}
