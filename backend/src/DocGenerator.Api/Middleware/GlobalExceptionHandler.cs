using DocGenerator.Application.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace DocGenerator.Api.Middleware;

/// <summary>
/// معالج موحّد للاستثناءات غير المعالجة التي تفلت من وحدات التحكم:
/// يردّ استجابة JSON بصيغة <c>{ message }</c> المتوافقة مع الواجهة
/// (<c>frontend/src/api/client.ts</c>)، ويسجّل التفاصيل كاملة في السجلات،
/// دون كشف أي تفاصيل داخلية للمستخدم في بيئة الإنتاج.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted)
            return false;

        var statusCode = exception switch
        {
            ArgumentException => StatusCodes.Status400BadRequest,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            DocumentConflictException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        _logger.LogError(exception,
            "استثناء غير معالج ({Type}) أثناء {Method} {Path}",
            exception.GetType().Name,
            httpContext.Request.Method,
            httpContext.Request.Path);

        var message = exception switch
        {
            ArgumentException or KeyNotFoundException or DocumentConflictException => exception.Message,
            _ when _environment.IsDevelopment() => exception.Message,
            _ => "حدث خطأ غير متوقع في الخادم",
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new { message }, cancellationToken);
        return true;
    }
}
