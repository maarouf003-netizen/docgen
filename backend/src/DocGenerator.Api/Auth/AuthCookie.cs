using DocGenerator.Application.Common;
using Microsoft.AspNetCore.Http;

namespace DocGenerator.Api.Auth;

/// <summary>
/// توكن الجلسة في Cookie مصادقة من نوع HttpOnly؛ لا يقرؤه أي سكربت (حماية XSS) ولا
/// يُرسَل إلى مواقع أخرى (SameSite=Strict = حماية CSRF للموقع أحادي الأصل)، فيغني عن
/// ترويسة Authorization وعن تخزين التوكن في localStorage.
/// </summary>
public static class AuthCookie
{
    public const string Name = "docgen_token";

    public static CookieOptions BuildOptions(IWebHostEnvironment env, JwtOptions jwt)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
            // عمر التوكن وعمر الـ Cookie متزامنان؛ انتهى أحدهما تعيّن إعادة الدخول.
            MaxAge = TimeSpan.FromMinutes(Math.Max(1, jwt.ExpiryMinutes)),
        };
    }

    /// <summary>خيارات مطابقة عند الحذف (نفس Path/SameSite/Secure) حتى يتعرف المتصفح على الـ Cookie.</summary>
    public static CookieOptions DeleteOptions(IWebHostEnvironment env)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/",
        };
    }
}
