namespace DocGenerator.Api.Security;

/// <summary>
/// سياسة موحّدة لقيمة <c>Security:CspReportUri</c> — مصدر الحقيقة الوحيد الذي تستخدمه
/// <see cref="CspSetup"/> (تحقق الإقلاع) و<see cref="Middleware.SecurityHeadersMiddleware"/>
/// (بناء الترويسة)، فلا يختلف ما يُقبل عند الإقلاع عمّا يُرسل فعلًا.
///
/// **القيد:** مسار جذري-نسبي فقط يبدأ بـ<c>/</c> وليس <c>//</c>، لأن تقارير <c>CSP</c> تحمل
/// عناوين الصفحات و<c>blocked-uri</c>؛ قبول عنوان خارجي يرسل هذه البيانات إلى طرف ثالث. وتُرفض المحارف التي تكسر ترويسة <c>report-uri</c> أو قيمة
/// <c>Reporting-Endpoints</c> المقتبسة (علامة تنصيص/شرطة مائلة عكسية/فاصلة/فاصلة منقوطة)
/// والمحارف غير <c>ASCII</c>.
/// </summary>
public static class CspReportUriPolicy
{
    /// <summary>مثال القيمة الصحيحة — يُذكر في رسالة رفض الإقلاع.</summary>
    public const string Example = "/api/csp-report";

    private static readonly char[] HeaderBreakingCharacters = ['"', '\\', ',', ';'];

    /// <summary>
    /// يطبّع القيمة: <c>null</c>/الفراغ = معطّل (بلا خطأ). القيمة الصالحة تُعاد مقصوصة،
    /// وغير الصالحة تُرجع <c>false</c> مع سبب عربي يسمّي المفتاح والمثال.
    /// </summary>
    public static bool TryNormalize(string? raw, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;

        if (raw is null)
            return true;

        var value = raw.Trim();
        if (value.Length == 0)
            return true;

        if (value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            error = Invalid("تحتوي بياضًا أو محارف تحكم");
            return false;
        }

        if (!value.StartsWith('/') || value.StartsWith("//", StringComparison.Ordinal))
        {
            error = Invalid("يجب أن تكون مسارًا جذريًا-نسبيًا يبدأ بـ'/' وليس '//'");
            return false;
        }

        if (value.IndexOfAny(HeaderBreakingCharacters) >= 0)
        {
            error = Invalid("تحتوي محارف تكسر ترويسة CSP (\" أو \\ أو , أو ;)");
            return false;
        }

        if (value.Any(c => c > 0x7E))
        {
            error = Invalid("يجب أن تكون محارفها ASCII");
            return false;
        }

        if (!Uri.TryCreate(value, UriKind.Relative, out _))
        {
            error = Invalid("ليست مسارًا نسبيًا صالحًا");
            return false;
        }

        normalized = value;
        return true;
    }

    private static string Invalid(string reason) =>
        $"Security:CspReportUri غير صالح: {reason} — استخدم مثالًا مثل {Example}، أو اتركه فارغًا لتعطيل الجمع.";
}
