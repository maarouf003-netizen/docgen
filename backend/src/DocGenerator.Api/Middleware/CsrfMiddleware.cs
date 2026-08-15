using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace DocGenerator.Api.Middleware;

/// <summary>
/// حماية CSRF دفاعًا إضافيًا فوق SameSite=Strict: كل طلب يغيّر الحالة (POST/PUT/PATCH/DELETE)
/// يجب أن يحمل ترويسة X-CSRF-Token تطابق قيمة Cookie docgen_csrf. بما أن الـ Cookie لا تُرسل عبر
/// المواقع المخالفة (SameSite=Strict) ولا يمكن لتطبيق أجنبي قراءتها أو إرفاق ترويسة مطابقة
/// دون موافقة مسبقة، فهذا يوقف أي طلب مزيّف. الدخول مستثنى (لا توجد توكن بعد) والخروج كذلك
/// (يجب أن يعمل دائمًا لحذف الجلسة).
/// </summary>
public sealed class CsrfMiddleware
{
    public const string CookieName = "docgen_csrf";
    public const string HeaderName = "X-CSRF-Token";

    private readonly RequestDelegate _next;

    public CsrfMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        if (HttpMethods.IsPost(method) || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method))
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var isLogin = path.EndsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase);
            var isLogout = path.EndsWith("/api/auth/logout", StringComparison.OrdinalIgnoreCase);
            if (!isLogin && !isLogout)
            {
                var cookie = context.Request.Cookies[CookieName];
                var header = context.Request.Headers[HeaderName].ToString();
                if (string.IsNullOrEmpty(cookie) || string.IsNullOrEmpty(header)
                    || !CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(cookie), Encoding.UTF8.GetBytes(header)))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(
                        new { message = "طلب مرفوض: ترويسة الحماية من النماذج المزيفة غير صحيحة" });
                    return;
                }
            }
        }
        await _next(context);
    }

    /// <summary>قيمة عشوائية جديدة لكل دخول (URL-safe base64 بدون رموز محجوزة في الـ Cookie).</summary>
    public static string CreateToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static CookieOptions CookieOptions(IWebHostEnvironment env, TimeSpan maxAge)
    {
        return new CookieOptions
        {
            // ليست HttpOnly حتى يقرأها التطبيق ويرسلها في الترويسة؛ الحماية الفعلية من تطابق
            // الترويسة مع الـ Cookie الذي لا يصل عبر المواقع المخالفة (SameSite=Strict).
            HttpOnly = false,
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
            // نفس عمر Cookie الجلسة (JWT) حتى يبقى الزوجان متزامنين: لو بقي CSRF جلسةً قصيرة
            // لُحذف عند إغلاق المتصفح بينما تبقى الجلسة سارية، فيرفض الخادم كل طلب يغيّر الحالة.
            MaxAge = maxAge,
        };
    }
}
