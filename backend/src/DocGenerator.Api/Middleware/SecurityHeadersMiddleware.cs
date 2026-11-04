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

    public SecurityHeadersMiddleware(RequestDelegate next, IOptions<SecurityOptions> security)
    {
        _next = next;
        _security = security.Value;
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

        // ردود API بيانات قانونية بعد المصادقة: لا تُخزَّن في متصفح مشترك ولا في وسيط
        // تخزين أمامي. تُضبط قبل `_next`، فمتحكم يضع `Cache-Control` صراحةً يتقدّم عليها
        // (كـ`ClientErrorsController` الذي يطلب `NoStore` أصلًا).
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            headers["Cache-Control"] = "no-store";

        await _next(context);
    }

    /// <summary>
    /// FR-010: إلحاق `report-uri` عند ضبطه لبدء الجمع المركزي قبل الفرض.
    /// تُقبل **مسارات جذرية-نسبية فقط** تبدأ بـ`/` (لا رابط مطلق ولا `//host` بروتوكول-نسبي):
    /// `report-uri` يخصّ الأصل نفسه، وقبول عنوان خارجي يجعل المتصفح يرسل مسارات الصفحات
    /// وتفاصيل المخالفات إلى طرف ثالث. أي قيمة أخرى تُتجاهل بصمت حفاظًا على السلامة.
    /// </summary>
    private string BuildCspValue()
    {
        var uri = _security.CspReportUri?.Trim();
        if (string.IsNullOrEmpty(uri))
            return CspValue;
        if (uri.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            return CspValue;
        // `StartsWith("/")` يمنع المخططات المطلقة، و`StartsWith("//")` يمنع المراجع
        // البروتوكول-نسبية التي يقبلها `Uri.TryCreate(..., Relative)` أيضًا.
        if (!uri.StartsWith('/') || uri.StartsWith("//", StringComparison.Ordinal))
            return CspValue;
        if (!Uri.TryCreate(uri, UriKind.Relative, out _))
            return CspValue;
        return CspValue + "; report-uri " + uri;
    }
}
