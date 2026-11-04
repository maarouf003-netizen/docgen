using DocGenerator.Api.Security;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Middleware;

/// <summary>
/// ترويسات الأمان على كل رد (S3): تُضبط عند الدخول (قبل <c>_next</c>) فتبقى حتى مع
/// الاستثناءات المعالجة لاحقًا. أول وسيط بعد <c>UseForwardedHeaders</c> وقبل
/// <c>UseExceptionHandler</c> في <c>Program.cs</c>.
/// سياسة <c>CSP</c> أحادية الأصل ومحافظة: <c>style-src 'unsafe-inline'</c> لازم لأن المحتوى
/// الغني المعقَّم يستخدم <c>span[style]</c> (لا يجيز سكربتات)، و<c>DOMPurify</c> في الواجهة
/// خط الدفاع الأول. يُستبعد <c>COEP: require-corp</c> عمدًا حتى لا يكسر ملفات
/// <c>wwwroot</c> الثابتة. **ولا تُضاف <c>upgrade-insecure-requests</c>**: على خادم يخدم
/// عبر <c>HTTP</c> (حالة النقل الأولى على الشبكة الداخلية) تُحوِّل المتصفحات كل أصول
/// الصفحة النسبية إلى <c>https</c> فتفشل ⇒ صفحة بيضاء. تحديث البروتوكول مسؤولية <c>HSTS</c>
/// و<c>HttpsRedirection</c> صراحةً، لا سياسة <c>CSP</c>.
/// ملاحظة بيئة التطوير: `Vite` يخدم مستند `SPA` بنفسه، فلا تصل ترويسة `Kestrel` إلى الصفحة
/// أصلًا (ولا علاقة لـ`connect-src 'self'` باتصال `HMR`). بقاء `CspReportOnly=true` افتراضيًا
/// قرار نشر (مراقبة قبل الفرض) لا استثناء تقني لـ`HMR`؛ والفرض (`false`) للمخدّم الخاص.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private const string CspValue =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; font-src 'self' data:; connect-src 'self'; " +
        "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    private readonly RequestDelegate _next;
    private readonly SecurityOptions _security;
    private readonly string? _cspReportUri;

    public SecurityHeadersMiddleware(RequestDelegate next, IOptions<SecurityOptions> security)
    {
        _next = next;
        _security = security.Value;
        // التحقق النهائي عند الإقلاع في `CspSetup.Validate`؛ هنا تطبيع دفاعي فقط
        // (اختبارات الوحدة قد تُنشئ الوسيط بقيمة غير صالحة مباشرةً).
        CspReportUriPolicy.TryNormalize(_security.CspReportUri, out _cspReportUri, out _);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        headers[_security.CspReportOnly
            ? "Content-Security-Policy-Report-Only"
            : "Content-Security-Policy"] = BuildCspValue();

        // FR-010: تعريف جامع التقارير على الأصل نفسه. `report-to` هو البديل الحديث
        // لـ`report-uri` (المتقاعد في CSP Level 3) ويشترط ترويسة `Reporting-Endpoints`
        // المقتبسة؛ وتُضاف `Report-To` القديمة (JSON) للمتصفحات التي لم تدعم الجديدة.
        if (_cspReportUri is not null)
        {
            headers["Reporting-Endpoints"] = $"csp=\"{_cspReportUri}\"";
            headers["Report-To"] =
                $"{{\"group\":\"csp\",\"max_age\":31536000,\"endpoints\":[{{\"url\":\"{_cspReportUri}\"}}]}}";
        }

        // ردود API بيانات قانونية بعد المصادقة: لا تُخزَّن في متصفح مشترك ولا في وسيط
        // تخزين أمامي. تُضبط قبل `_next`، فمتحكم يضع `Cache-Control` صراحةً يتقدّم عليها
        // (كـ`ClientErrorsController` الذي يطلب `NoStore` أصلًا).
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            headers["Cache-Control"] = "no-store";

        await _next(context);
    }

    /// <summary>
    /// FR-010: إلحاق <c>report-uri</c> (احتياط المتصفحات الأقدم) و<c>report-to</c> (الحديث)
    /// عند ضبط مسار صالح. القيمة طُبِّعت في المنشئ عبر <see cref="CspReportUriPolicy"/>،
    /// والتحقق القاطع عند الإقلاع في <see cref="CspSetup.Validate"/>.
    /// </summary>
    private string BuildCspValue() =>
        _cspReportUri is null
            ? CspValue
            : CspValue + "; report-uri " + _cspReportUri + "; report-to csp";
}
