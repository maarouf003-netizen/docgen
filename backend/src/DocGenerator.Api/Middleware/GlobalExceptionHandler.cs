using System.Security.Claims;
using DocGenerator.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

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

        // الترابط: المعرّف والهوية يُسجَّلان صراحة من HttpContext لأن وسيط الإثراء
        // أسفل معالج الاستثناءات في السلسلة (نطاق LogContext يُغلق أثناء تفكيك الاستثناء).
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        _logger.LogError(exception,
            "استثناء غير معالج ({Type}) أثناء {Method} {Path} [TraceId={TraceId}, UserId={UserId}]",
            exception.GetType().Name,
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.TraceIdentifier,
            userId);

        var message = exception switch
        {
            ArgumentException or KeyNotFoundException or DocumentConflictException => exception.Message,
            DbUpdateException ex when _environment.IsDevelopment() => DescribeDbUpdateException(ex),
            DbUpdateException => "فشل حفظ التغييرات في قاعدة البيانات",
            _ when _environment.IsDevelopment() => exception.Message,
            _ => "حدث خطأ غير متوقع في الخادم",
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new { message }, cancellationToken);
        return true;
    }

    /// <summary>
    /// رسالة فعلية من غلاف فشل الحفظ: غلاف <c>DbUpdateException</c> يردّ نصًا عامًا
    /// («An error occurred while saving the entity changes...») بلا فائدة تشخيصية، بينما
    /// يحمل الجذر الداخلي القيد المخالَف أو سبب SQL الحقيقي (مثل
    /// <c>SQLite Error 19: UNIQUE constraint failed: ...</c>)، فيُمشى نحو أعمق استثناء
    /// داخلي. تُستخدم في بيئة التطوير فقط لتشخيص فوري دون كشف أي تفاصيل في الإنتاج.
    /// </summary>
    private static string DescribeDbUpdateException(DbUpdateException exception)
    {
        if (exception.InnerException is not Exception inner)
            return "فشل حفظ التغييرات في قاعدة البيانات";
        var deepest = inner;
        while (deepest.InnerException is not null)
            deepest = deepest.InnerException;
        return deepest.Message;
    }
}