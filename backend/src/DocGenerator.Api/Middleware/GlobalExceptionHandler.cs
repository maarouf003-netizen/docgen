using System.Security.Claims;
using DocGenerator.Api.Http;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Api.Middleware;

/// <summary>
/// معالج موحّد للاستثناءات غير المعالجة التي تفلت من وحدات التحكم:
/// يردّ استجابة JSON بصيغة <c>{ message }</c> المتوافقة مع الواجهة
/// (<c>frontend/src/api/client.ts</c>)، ويسجّل التفاصيل كاملة في السجلات،
/// دون كشف أي تفاصيل داخلية للمستخدم في بيئة الإنتاج.
/// يشمل شبكة أمان لتعارض القيد الفريد غير الملتقط (W1-7) عبر
/// <see cref="IDbExceptionClassifier"/>: 409 ورسالة عربية عامة بدل 500.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    /// <summary>
    /// رمز غير قياسي متعارف عليه (nginx) لانقطاع العميل قبل إرسال الرد. لا يُستخدم
    /// <see cref="StatusCodes.Status500InternalServerError"/> هنا لأن الانقطاع سلوك عميل
    /// متوقع لا عطل خادم — ويُلوّث لوحات المراقبة بأخطاء غير حقيقية.
    /// </summary>
    private const int ClientClosedRequestStatusCode = 499;

    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;
    private readonly IDbExceptionClassifier _dbErrors;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment,
        IDbExceptionClassifier dbErrors)
    {
        _logger = logger;
        _environment = environment;
        _dbErrors = dbErrors;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.Response.HasStarted)
            return false;

        // يُحسب مرة واحدة: يُستخدم للتصنيف والرسالة والتسجيل (لا استدعاء متكرر للمصنّف).
        // `DbUpdateConcurrencyException` مشتق من `DbUpdateException`، لكنه يُعامل أولًا
        // في فرع مستقل (رسالة «أعد التحميل» لا رسالة «قيمة مكررة»).
        var isUniqueViolation = exception is DbUpdateException dbFailure
            && _dbErrors.IsUniqueViolation(dbFailure);

        var statusCode = exception switch
        {
            ArgumentException => StatusCodes.Status400BadRequest,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            DocumentConflictException => StatusCodes.Status409Conflict,
            // RF-010 + المرحلة 5ب: أي تعارض تفاؤلية يفلت من الحراس الخدميين
            // (رموز التزامن `Document.Version` و`DocumentAppeal.Version`)
            // يُردّ 409 لا 500.
            DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            // W1-7: تفرد غير ملتقط (سباق على قيد فريد لم يغطّه حارس خدمي) = تعارض بيانات
            // لا عطل خادم. التصنيف من رموز المزود في `DbExceptionClassifier` (SQLite
            // 19/2067 وPostgreSQL 23505) عبر `IDbExceptionClassifier`.
            DbUpdateException when isUniqueViolation => StatusCodes.Status409Conflict,
            // الرفض الصريح وصولًا (لا خطأ خادم): أي `UnauthorizedAccessException`
            // يفلت من الخدمة يُردّ 403 بجسم `{ message }` لا 500 (R0-6 مكتمل:
            // `Portal`+`ReviewLetters`+`EntityRegistry`+`Forum`+`Correspondences`+
            // `UserManagement` لم تعد تبتلعه؛ الإبقاء الوحيد تمويه `Portal:296` الوجودي) —
            // الحراس الاستباقية (`if (...) return Forbid();`) ترد 403 فارغًا مباشرة
            // (مقبول صراحة — الواجهة تحجب الشكلين `client.ts:17`).
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            // العميل أغلق الاتصال قبل اكتمال المعالجة (`RequestAborted`) — لا عطل خادم.
            // يُقيَّد بـ`RequestAborted` عمدًا: `OperationCanceledException` من مهلة داخلية
            // (إلغاء خادم) يبقى 500 لأن سببه ليس انقطاع العميل.
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested
                => ClientClosedRequestStatusCode,
            _ => StatusCodes.Status500InternalServerError,
        };

        // الترابط: المعرّف والهوية يُسجَّلان صراحة من HttpContext لأن وسيط الإثراء
        // أسفل معالج الاستثناءات في السلسلة (نطاق LogContext يُغلق أثناء تفكيك الاستثناء).
        // رفض النطاق متوقع تشغيليًا (R0-6: المتحكمات لم تعد تبتلعه) فيُسجَّل Warning
        // لا Error — الأخطاء الحقيقية وحدها تستحق الأحمر والتنبيه.
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        if (statusCode == ClientClosedRequestStatusCode)
            // انقطاع العميل سلوك متوقع: Information لا Error/Warning (لا تلويث لوحات الأعطال).
            _logger.LogInformation(
                "انقطع اتصال العميل ({Type}) أثناء {Method} {Path} [TraceId={TraceId}, UserId={UserId}]",
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier,
                userId);
        else if (exception is UnauthorizedAccessException)
            _logger.LogWarning(exception,
                "رفض نطاق ({Type}) أثناء {Method} {Path} [TraceId={TraceId}, UserId={UserId}]",
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier,
                userId);
        else if (isUniqueViolation)
            // سباق على قيد فريد متوقع تشغيليًا (المستخدم/الطلب الصحيح أعاد المحاولة) —
            // Warning مع التفاصيل الكاملة في السجل، لا Error.
            _logger.LogWarning(exception,
                "تعارض قيد فريد ({Type}) أثناء {Method} {Path} [TraceId={TraceId}, UserId={UserId}]",
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier,
                userId);
        else if (statusCode is >= StatusCodes.Status400BadRequest and <= 499)
            // بقية الردود 4xx متوقعة (تحقّق مدخلات/غياب مرجع/تعارض عمل) لا أعطال خادم —
            // Information لا Error كي لا تلوّث لوحات المراقبة. 499 و403 وتعارض القيد
            // الفريد معالَجة أعلاه بمستوياتها المقصودة.
            _logger.LogInformation(
                "رفض طلب ({Type}) أثناء {Method} {Path} [TraceId={TraceId}, UserId={UserId}]",
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier,
                userId);
        else
            _logger.LogError(exception,
                "استثناء غير معالج ({Type}) أثناء {Method} {Path} [TraceId={TraceId}, UserId={UserId}]",
                exception.GetType().Name,
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier,
                userId);

        if (statusCode == ClientClosedRequestStatusCode)
        {
            // لا جسم ولا كتابة: القناة مغلقة أصلًا، ومحاولة `WriteAsJsonAsync` عليها
            // بـ`RequestAborted` قد ترمي استثناءً ثانيًا أثناء معالجة الأول.
            httpContext.Response.StatusCode = statusCode;
            return true;
        }

        var message = exception switch
        {
            // رسالة `EF` التقنية إنجليزية فتُستبدَل بعربية ودية ثابتة (التفاصيل في السجل أعلاه).
            DbUpdateConcurrencyException =>
                "تغيّرت البيانات أثناء الحفظ من مستخدم آخر — أعد التحميل وحاول مجددًا",
            // رسائل `4xx` عربية مقصودة لكنها قد تعكس مدخلًا خامًا (`SEC-012`)، فتُعقَّم
            // (تسطيح/تجريد/قصّ) قبل الرد، وبالرسالة البديلة إن صار النص فارغًا — السياسة
            // نفسها التي كانت في كتل `catch` المتحكمات، فلا فرق في الجسم بين المسارين.
            ArgumentException or KeyNotFoundException or DocumentConflictException
                or UnauthorizedAccessException => ApiErrorMessages.From(exception),
            // W1-7: رسالة عامة لا تكشف اسم القيد/الجدول/المزود — التفاصيل في السجل أعلاه.
            DbUpdateException when isUniqueViolation =>
                "تعارض في البيانات — القيمة مكررة، حدّث الصفحة وحاول مجددًا",
            DbUpdateException ex when _environment.IsDevelopment() => DescribeDbUpdateException(ex),
            DbUpdateException => "فشل حفظ التغييرات في قاعدة البيانات",
            // حتى رسالة التطوير لا تُعكس خامًا: التسطيح/التجريد/القصّ يمنعان تزوير
            // السطور وتسريب الأسرار إن شُغّل `Development` على خادم منشور بالخطأ.
            _ when _environment.IsDevelopment() => LogSanitizer.SanitizeForResponse(exception.Message),
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
    /// داخلي. تُستخدم في بيئة التطوير فقط لتشخيص فوري دون كشف أي تفاصيل في الإنتاج،
    /// وتمرّ بالسياسة نفسها (<see cref="LogSanitizer.SanitizeForResponse"/>) قبل الرد.
    /// </summary>
    private static string DescribeDbUpdateException(DbUpdateException exception)
    {
        if (exception.InnerException is not Exception inner)
            return "فشل حفظ التغييرات في قاعدة البيانات";
        var deepest = inner;
        while (deepest.InnerException is not null)
            deepest = deepest.InnerException;
        return LogSanitizer.SanitizeForResponse(deepest.Message);
    }
}