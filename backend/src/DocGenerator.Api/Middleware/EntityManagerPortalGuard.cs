using DocGenerator.Api.Authorization;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace DocGenerator.Api.Middleware;

/// <summary>
/// عزل بنيوي لدور مندوب الجهة (د10): أي طلب API من هذا الدور خارج مسارات
/// البوابة القرائية و«من أنا/خروج» يُرفض بـ403 فورًا — لا يعتمد على تذكّر
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
                || path.StartsWithSegments("/api/auth/logout");

            if (!allowed)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "بوابة الجهة قراءة فقط (تصدير Excel متاح)" });
                return;
            }
        }

        await _next(context);
    }

    private static UserRole TryGetRole(HttpContext context) => context.User.GetRoleEnum();
}
