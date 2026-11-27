using System.Security.Cryptography;
using System.Text;
using DocGenerator.Api.Http;
using DocGenerator.Api.Security;
using Microsoft.AspNetCore.Http;

namespace DocGenerator.Api.Middleware;

/// <summary>
/// حماية CSRF دفاعًا إضافيًا فوق SameSite=Strict: كل طلب يغيّر الحالة (POST/PUT/PATCH/DELETE)
/// يجب أن يحمل ترويسة X-CSRF-Token تطابق قيمة Cookie docgen_csrf. بما أن الـ Cookie لا تُرسل عبر
/// المواقع المخالفة (SameSite=Strict) ولا يمكن لتطبيق أجنبي قراءتها أو إرفاق ترويسة مطابقة
/// دون موافقة مسبقة، فهذا يوقف أي طلب مزيّف. الدخول مستثنى من مطابقة التوكن (لا توجد توكن بعد)
/// والخروج كذلك (يجب أن يعمل دائمًا لحذف الجلسة) — لكن الاثنين يخضعان لفحص الأصل أدناه.
/// </summary>
public sealed class CsrfMiddleware
{
    public const string CookieName = "docgen_csrf";
    public const string HeaderName = "X-CSRF-Token";

    /// <summary>ترويسة المتصفحات الحديثة التي تصنّف مصدر الطلب — الأقوى في كشف الطلبات المزيّفة.</summary>
    public const string FetchSiteHeader = "Sec-Fetch-Site";

    private const string LoginPath = "/api/auth/login";
    private const string LogoutPath = "/api/auth/logout";

    private readonly RequestDelegate _next;

    public CsrfMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        if (HttpMethods.IsPost(method) || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method))
        {
            // B2/W2: فحص الأصل يسبق استثناءات التوكن. استثناء الدخول من مطابقة التوكن
            // لا يجوز أن يفتح تثبيت جلسة (Login CSRF): متصفح الضحية يرسل Origin/Sec-Fetch-Site
            // للطلب المزوّر فتُرفض، بينما عملاء API (curl/اختبارات) بلا هذه الترويسات يمرّون.
            if (!HasTrustedBrowserOrigin(context.Request))
            {
                await ApiProblem.WriteAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    ApiErrorCodes.CsrfRejected,
                    "طلب مرفوض: مصدر الطلب غير موثوق");
                return;
            }

            var path = context.Request.Path;
            // مطابقة مسار تامة (لا `EndsWith`) حتى لا يفتح مسار مستقبلي ينتهي
            // بـ`/api/auth/login` باب الاستثناء سهوًا (W2/F13).
            var isLogin = path.Equals(LoginPath, StringComparison.OrdinalIgnoreCase);
            var isLogout = path.Equals(LogoutPath, StringComparison.OrdinalIgnoreCase);
            // تقارير CSP يرسلها المتصفح تلقائيًا بلا جلسة/كوكي CSRF (وقد لا يرسل أي كوكي)،
            // والنقطة عامة/محدودة المعدل/بلا أثر كتابي خارج السجل — فتُستثنى حصرًا بمسارها
            // الدقيق (مع شرطة أخيرة اختيارية) حتى لا يفتح الاستثناء أي مسار آخر.
            var isCspReport =
                string.Equals(path.Value, CspReportUriPolicy.RequiredPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path.Value, CspReportUriPolicy.RequiredPath + "/", StringComparison.OrdinalIgnoreCase);
            if (!isLogin && !isLogout && !isCspReport)
            {
                var cookie = context.Request.Cookies[CookieName];
                var header = context.Request.Headers[HeaderName].ToString();
                if (string.IsNullOrEmpty(cookie) || string.IsNullOrEmpty(header)
                    || !CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(cookie), Encoding.UTF8.GetBytes(header)))
                {
                    await ApiProblem.WriteAsync(
                        context,
                        StatusCodes.Status403Forbidden,
                        ApiErrorCodes.CsrfRejected,
                        "طلب مرفوض: ترويسة الحماية من النماذج المزيفة غير صحيحة");
                    return;
                }
            }
        }
        await _next(context);
    }

    /// <summary>
    /// هل الطلب صادر عن نفس أصل التطبيق (أو عن عميل غير متصفح)؟
    /// <para>
    /// الأولوية لـ`Sec-Fetch-Site` إن وُجد (المتصفحات الحديثة): يُقبل `same-origin` و`none`
    /// (تنقّل مباشر/إشارة مرجعية) فقط، ويُرفض `same-site` أيضًا لأن النطاقات الفرعية قد
    /// تكون تحت سيطرة طرف آخر. ثم `Origin`، ثم `Referer`؛ وغياب الثلاثة (curl/اختبارات/عملاء
    /// API) لا يُرفض لأن الحماية الفعلية تبقى مطابقة توكن CSRF للطلبات غير المستثناة.
    /// </para>
    /// </summary>
    private static bool HasTrustedBrowserOrigin(HttpRequest request)
    {
        var fetchSite = request.Headers[FetchSiteHeader].ToString();
        if (!string.IsNullOrEmpty(fetchSite))
            return fetchSite.Equals("same-origin", StringComparison.OrdinalIgnoreCase)
                || fetchSite.Equals("none", StringComparison.OrdinalIgnoreCase);

        var origin = request.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(origin))
            return MatchesRequestOrigin(request, origin);

        var referer = request.Headers.Referer.ToString();
        if (!string.IsNullOrEmpty(referer))
            return MatchesRequestOrigin(request, referer);

        return true;
    }

    private static bool MatchesRequestOrigin(HttpRequest request, string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;
        if (!string.Equals(uri.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.Equals(uri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase))
            return false;

        // المقارنة بالمنفذ الفعّال: `Origin` يذكر المنفذ دائمًا، و`Host` يحذفه عندما
        // يكون افتراضيًا للمخطط (‏https ⇒ 443)، فلا يُرفض طلب سليم بسبب اختلاف الشكل.
        var requestPort = request.Host.Port ?? (request.IsHttps ? 443 : 80);
        return uri.Port == requestPort;
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
