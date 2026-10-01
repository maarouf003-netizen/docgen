using System.Security.Claims;
using DocGenerator.Api.Authorization;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Middleware;

/// <summary>
/// عزل بنيوي لدور مندوب الجهة: أي طلب API من هذا الدور خارج مسارات
/// البوابة المسموحة و«من أنا/خروج» يُرفض بـ403 فورًا — لا يعتمد على تذكّر
/// كل متحكم قائم أو لاحق بفحص الدور.
/// </summary>
public sealed class EntityManagerPortalGuard
{
    private readonly RequestDelegate _next;

    public EntityManagerPortalGuard(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.Request.Path.StartsWithSegments("/api")
            && TryGetRole(context) == UserRole.EntityManager)
        {
            var path = context.Request.Path;
            var allowed =
                path.StartsWithSegments("/api/portal")
                || path.StartsWithSegments("/api/auth/me")
                || path.StartsWithSegments("/api/auth/logout")
                // الدور القرائي يجب أن يُبلغ عن الأعطال: نقطة client-errors موثقة ومخنوقة لكل مستخدم.
                || path.StartsWithSegments("/api/client-errors")
                // «سنة النظام» قرائية عامة للواجهة (سنة التدوير/الإعادة تظهر منها لجميع الأدوار).
                || path.StartsWithSegments("/api/meta");

            if (!allowed)
            {
                // RF-005: توثيق الرفض (مرشح لمسح المعرفات) — مخنوق لكل مستخدم حتى لا
                // يتحول التسجيل نفسه لمتجه فيض على جدول التدقيق.
                await LogForbiddenAsync(context, path);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "غير مسموح خارج مسارات بوابة الجهة" });
                return;
            }
        }

        await _next(context);
    }

    /// <summary>
    /// صف تدقيق واحد لكل مستخدم كل 5 دقائق كحد أقصى. التسجيل لا يكسر الحارس أبدًا —
    /// أي عطل فيه يُبتلَع عمدًا بعد الرفض (الأمن أولًا: الرفض مضمون، والتوثيق أفضل جهد).
    /// </summary>
    private static async Task LogForbiddenAsync(HttpContext context, PathString path)
    {
        try
        {
            var cache = context.RequestServices.GetService<IMemoryCache>();
            var audit = context.RequestServices.GetService<IAuditLogger>();
            if (cache is null || audit is null)
                return;
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0";
            var key = $"portal_forbidden_{userId}";
            if (cache.TryGetValue(key, out _))
                return;
            cache.Set(key, 1, TimeSpan.FromMinutes(5));
            var userName = context.User.FindFirstValue(ClaimTypes.Name) ?? "unknown";
            await audit.LogAsync(userName, "portal_forbidden",
                details: $"رفض وصول مندوب خارج البوابة: {path}", ct: context.RequestAborted);
        }
        catch
        {
            // التسجيل أفضل جهد — لا يعطل الرفض ولا يُسرِّب استثناءً.
        }
    }

    private static UserRole TryGetRole(HttpContext context) => context.User.GetRoleEnum();
}
