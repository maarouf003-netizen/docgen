using System.Security.Claims;
using Serilog.Context;

namespace DocGenerator.Api.Middleware;

/// <summary>
/// يضغط معرّف الطلب وهوية المستخدم في LogContext لكي تظهر كحقول منظمة في كل سطر سجل،
/// ويوسم طلبات الترقيم **بالمؤشر**.
///
/// وسم <c>CursorPaging</c> يُميّز في السجلات طلب المؤشر من طلب الإزاحة عند تشخيص
/// «صف ظهر ناقصًا»: بدونه لا يُعرف من السجل أي وضع ترقيم استُخدم (الخطة §9.1).
/// يُضاف فقط عند وجود <c>cursor</c> في الاستعلام، فغيابه يعني «إزاحة/صفحة أولى».
/// </summary>
public sealed class RequestLoggingEnricherMiddleware
{
    private readonly RequestDelegate _next;

    public RequestLoggingEnricherMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var role = context.User.FindFirstValue(ClaimTypes.Role) ?? "none";

        using (LogContext.PushProperty("TraceId", context.TraceIdentifier))
        using (LogContext.PushProperty("UserId", userId))
        using (LogContext.PushProperty("UserRole", role))
        {
            if (!IsCursorPaging(context.Request))
            {
                await _next(context);
                return;
            }

            using (LogContext.PushProperty("CursorPaging", true))
            {
                await _next(context);
            }
        }
    }

    /// <summary>هل الطلب يستخدم الترقيم بالمؤشر؟ (وجود <c>cursor</c> في الاستعلام.)</summary>
    public static bool IsCursorPaging(HttpRequest request) =>
        request.Query.ContainsKey("cursor");
}
