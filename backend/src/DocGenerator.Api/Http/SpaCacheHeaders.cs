using Microsoft.AspNetCore.StaticFiles;

namespace DocGenerator.Api.Http;

/// <summary>
/// ترويسات التخزين لملفات الواجهة المُقدَّمة من <c>wwwroot</c>:
/// <list type="bullet">
/// <item>
/// قشرة SPA (<c>index.html</c> — بما فيها مسارات العميل عبر احتياطي SPA) تُعيد
/// التحقق دائمًا: هي التي تحمل أسماء الأصول المُبصَّمة، فبقاؤها في الكاش يعني
/// تشغيل حزمة قديمة بعد النشر رغم وصول الجديدة إلى الخادم.
/// </item>
/// <item>
/// الأصول تحت <c>/assets/</c> مُبصَّمة بالمحتوى (Vite) فتُخزَّن سنة كاملة
/// <c>immutable</c> بلا خطر: تغيّر المحتوى يغيّر الاسم.
/// </item>
/// </list>
/// لا يغيّر شيئًا للملفات الأخرى (صور/خطوط)؛ تبقى على ETag/Last-Modified.
/// </summary>
public static class SpaCacheHeaders
{
    public static void Apply(StaticFileResponseContext context)
    {
        var response = context.Context.Response;
        var requestPath = context.Context.Request.Path.Value ?? string.Empty;

        if (string.Equals(context.File.Name, "index.html", StringComparison.OrdinalIgnoreCase))
        {
            response.Headers.CacheControl = "no-cache, max-age=0, must-revalidate";
            response.Headers.Pragma = "no-cache";
            response.Headers.Expires = "0";
            return;
        }

        if (requestPath.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase))
            response.Headers.CacheControl = "public, max-age=31536000, immutable";
    }
}
