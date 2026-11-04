namespace DocGenerator.Api.Security;

/// <summary>
/// سياسة موحّدة لقيمة <c>Security:CspReportUri</c> — مصدر الحقيقة الوحيد الذي تستخدمه
/// <see cref="CspSetup"/> (تحقق الإقلاع) و<see cref="Middleware.SecurityHeadersMiddleware"/>
/// (بناء الترويسة)، فلا يختلف ما يُقبل عند الإقلاع عمّا يُرسل فعلًا.
///
/// **القيد:** المسار المدمج <see cref="RequiredPath"/> حرفيًا فقط. تقارير <c>CSP</c> تحمل
/// عناوين الصفحات و<c>blocked-uri</c>، فقبول أي مسار آخر — ولو كان same-origin — يوجّه
/// البيانات إلى مسار قد لا يكون جامعًا (فتُفقد بصمت) أو قد يكون مسارًا حساسًا. والتحقق
/// الحرفي يمنع أيضًا حقن الترويسات ومحارف كسر <c>Reporting-Endpoints</c>.
/// </summary>
public static class CspReportUriPolicy
{
    /// <summary>مسار التوجيه الداخلي (بلا شرطة بادئة) — يستخدمه <c>CspReportsController</c>.</summary>
    public const string ReportRoute = "api/csp-report";

    /// <summary>المسار المدمج الوحيد لجامع التقارير (مطابق لمسار <c>CspReportsController</c>).</summary>
    public const string RequiredPath = "/" + ReportRoute;

    /// <summary>مثال القيمة الصحيحة — يُذكر في رسالة رفض الإقلاع.</summary>
    public const string Example = RequiredPath;

    /// <summary>سقف طول القيمة قبل التحقق (يمنع تضخيم ترويسة <c>Reporting-Endpoints</c>).</summary>
    public const int MaximumLength = 512;

    /// <summary>
    /// يطبّع القيمة: <c>null</c>/الفراغ = معطّل (بلا خطأ). القيمة الصالحة يجب أن تساوي
    /// <see cref="RequiredPath"/> حرفيًا (بعد قصّ البياض المحيط)؛ وغير ذلك يُرجع <c>false</c>
    /// مع سبب عربي يسمّي المفتاح والمثال.
    /// </summary>
    public static bool TryNormalize(string? raw, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;

        if (raw is null)
            return true;

        if (raw.Length > MaximumLength)
        {
            error = Invalid($"تتجاوز {MaximumLength} حرفًا");
            return false;
        }

        var value = raw.Trim();
        if (value.Length == 0)
            return true;

        if (!string.Equals(value, RequiredPath, StringComparison.Ordinal))
        {
            error = Invalid($"يجب أن تكون المسار المدمج {RequiredPath} حرفيًا (لا رابطًا مطلقًا ولا مسارًا آخر)");
            return false;
        }

        normalized = value;
        return true;
    }

    private static string Invalid(string reason) =>
        $"Security:CspReportUri غير صالح: {reason} — استخدم {Example}، أو اتركه فارغًا لتعطيل الجمع.";
}
