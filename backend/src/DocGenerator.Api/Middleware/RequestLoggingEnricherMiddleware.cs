using System.Security.Claims;
using Serilog.Context;

namespace DocGenerator.Api.Middleware;

/// <summary>
/// يضغط معرّف الطلب وهوية المستخدم في LogContext لكي تظهر كحقول منظمة في كل سطر سجل.
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
            await _next(context);
        }
    }
}
