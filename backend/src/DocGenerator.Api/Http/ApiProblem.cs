using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Http;

/// <summary>
/// جسم خطأ موحّد لكل استجابات الخطأ التي يكتبها التطبيق (متحكمات، وسائط، مرشحات،
/// ومعالج الاستثناءات العام): وثيقة متوافقة مع <see href="https://www.rfc-editor.org/rfc/rfc9457">
/// RFC 9457 (Problem Details for HTTP APIs)</see> مع امتدادات عربية ثابتة.
///
/// <para><b>لماذا صنف مستقل لا <c>ProblemDetails</c> الإطاري:</b> الضبط الدقيق للحقول
/// المطلوبة يمنع ظهور مفاتيح فارغة (<c>title</c>/<c>detail</c>/<c>instance</c>) كانت
/// ستُضاف بـ<c>null</c>؛ والعقد الملزم (قرار المالك 2026-11-03) ينصّ على جسم عربي
/// بلا عنوان/تفصيل إنجليزيين. الصنف هنا يعرّف الشكل على السلك صراحةً.</para>
///
/// <para><b>المفاتيح:</b> <c>type</c> و<c>status</c> و<c>code</c> و<c>message</c> دائمًا؛
/// و<c>field</c> و<c>errors</c> و<c>warningToken</c> و<c>traceId</c> عند الحاجة فقط.
/// <c>message</c> هو المصدر الوحيد للعرض (الواجهة تقرأه في <c>client.ts</c>)، و<c>code</c>
/// للقرار البرمجي، و<c>field</c> للربط بحقل النموذج.</para>
/// </summary>
public sealed class ApiProblem
{
    /// <summary>نوع الوسائط المعياري لوثائق الأخطاء.</summary>
    public const string ContentType = "application/problem+json";

    /// <summary>النوع الافتراضي في RFC 9457 عندما لا يوجد نوع موثّق أخصّ.</summary>
    public const string DefaultType = "about:blank";

    private ApiProblem()
    {
    }

    [JsonPropertyName("type")]
    public string Type { get; init; } = DefaultType;

    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("code")]
    public string Code { get; init; } = ApiErrorCodes.ServerError;

    [JsonPropertyName("message")]
    public string Message { get; init; } = ApiErrorMessages.Fallback;

    /// <summary>مسار حقل الطلب المعني (camelCase) عند ارتباط الخطأ بحقل واحد.</summary>
    [JsonPropertyName("field")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Field { get; init; }

    /// <summary>أخطاء عدة حقول (تحقق ربط النموذج) بمفاتيح camelCase.</summary>
    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    /// <summary>رمز تحذير إعادة استخدام رقم الأساس — يُستهلك في <c>numberWarning.ts</c>.</summary>
    [JsonPropertyName("warningToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WarningToken { get; init; }

    /// <summary>معرّف التتبع للمطابقة مع السجلات (يُملأ من <see cref="HttpContext"/>).</summary>
    [JsonPropertyName("traceId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; init; }

    /// <summary>
    /// يبني الوثيقة من سياق الطلب: يضيف <c>traceId</c> تلقائيًا، ويستخدم
    /// <paramref name="code"/> الصريح أو الرمز الافتراضي لحالة HTTP.
    /// </summary>
    public static ApiProblem Create(
        HttpContext? context,
        int status,
        string? code = null,
        string? message = null,
        string? field = null,
        IReadOnlyDictionary<string, string[]>? errors = null,
        string? warningToken = null) => new()
        {
            Status = status,
            Code = string.IsNullOrWhiteSpace(code) ? DefaultCodeFor(status) : code,
            Message = string.IsNullOrWhiteSpace(message) ? ApiErrorMessages.Fallback : message,
            Field = string.IsNullOrWhiteSpace(field) ? null : field,
            Errors = errors is { Count: > 0 } ? errors : null,
            WarningToken = string.IsNullOrWhiteSpace(warningToken) ? null : warningToken,
            TraceId = string.IsNullOrWhiteSpace(context?.TraceIdentifier) ? null : context!.TraceIdentifier,
        };

    /// <summary>الرمز الافتراضي المشتق من حالة HTTP (يُتجاوز برمز أدق عند الحاجة).</summary>
    public static string DefaultCodeFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => ApiErrorCodes.ValidationFailed,
        StatusCodes.Status401Unauthorized => ApiErrorCodes.Unauthorized,
        StatusCodes.Status403Forbidden => ApiErrorCodes.Forbidden,
        StatusCodes.Status404NotFound => ApiErrorCodes.NotFound,
        StatusCodes.Status409Conflict => ApiErrorCodes.Conflict,
        StatusCodes.Status423Locked => ApiErrorCodes.LockedOut,
        StatusCodes.Status429TooManyRequests => ApiErrorCodes.RateLimited,
        _ => ApiErrorCodes.ServerError,
    };

    /// <summary>
    /// يحوّل مسار <c>ModelState</c> (بأسماء خصائص C#) إلى مسار طلب camelCase.
    /// يُطبَّع كل مقطع منفصلًا لأن المفاتيح قد تكون مركّبة (<c>Request.FileNumber</c>)
    /// أو مفهرسة (<c>Items[0].Name</c>).
    /// </summary>
    public static string NormalizeFieldName(string? modelStateKey)
    {
        if (string.IsNullOrWhiteSpace(modelStateKey))
            return "request";

        // مفاتيح ربط JSON تأتي بمسار "$" أو "$.field"؛ الأول جسم الطلب كله،
        // والثاني حقل أعلى المستوى — لا يُعرض "$" كاسم حقل للمستخدم.
        var key = modelStateKey.Trim();
        if (key == "$")
            return "request";
        if (key.StartsWith("$.", StringComparison.Ordinal))
            key = key[2..];

        return string.Join('.', key.Split('.').Select(segment =>
            segment.Length == 0 ? segment : char.ToLowerInvariant(segment[0]) + segment[1..]));
    }

    /// <summary>
    /// نتيجة MVC جاهزة للإرجاع من متحكم: تحمل الحالة و<see cref="ContentType"/>،
    /// فلا يبني أي متحكم جسم خطأ يدويًا.
    /// </summary>
    public static ObjectResult Result(
        HttpContext? context,
        int status,
        string? code = null,
        string? message = null,
        string? field = null,
        IReadOnlyDictionary<string, string[]>? errors = null,
        string? warningToken = null) =>
        new(Create(context, status, code, message, field, errors, warningToken))
        {
            StatusCode = status,
            ContentTypes = { ContentType },
        };

    /// <summary>
    /// يكتب وثيقة خطأ مباشرة على الاستجابة (لوسائط/مرشحات لا تمرّ بـMVC):
    /// يضبط الحالة ونوع الوسائط قبل الكتابة.
    /// </summary>
    public static Task WriteAsync(
        HttpContext context,
        int status,
        string? code = null,
        string? message = null,
        string? field = null,
        string? warningToken = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(
            Create(context, status, code, message, field, warningToken: warningToken),
            options: null,
            contentType: ContentType,
            cancellationToken: cancellationToken);
    }
}
