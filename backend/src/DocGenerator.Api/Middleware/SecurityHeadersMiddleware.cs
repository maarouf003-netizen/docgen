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
/// <c>wwwroot</c> الثابتة. ملاحظة بيئة التطوير: `connect-src 'self'` يحجب `ws:` الخاص
/// بـ `Vite HMR`، لذا يبقى وضع المراقبة (`CspReportOnly=true`) إلزاميًا في التطوير —
/// الفرض (`false`) للمخدّم الخاص (إنتاج) فقط حيث لا `HMR`.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private const string CspValue =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; font-src 'self' data:; connect-src 'self'; " +
        "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; " +
        "upgrade-insecure-requests";

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
            : "Content-Security-Policy"] = CspValue;

        await _next(context);
    }
}
