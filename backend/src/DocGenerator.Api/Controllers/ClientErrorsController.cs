using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DocGenerator.Application.Common.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Controllers;

/// <summary>
/// نقطة إبلاغ أخطاء الواجهة: موثّقة حصرًا ([Authorize] — مستخدمو التطبيق فقط، لا توجد نقطة
/// استقبال عامة)، مصادَق عليها تلقائيًا عبر Cookie الـ CSRF دون أي استثناء في CsrfMiddleware،
/// ومخنوقة لكل مستخدم (IMemoryCache) لمنع الفيض. الرد 202 بلا صدى، والتسجيل منظّم بلا أي
/// بيانات حساسة (لا كلمات مرور، لا محتوى مستندات، لا بيانات شخصية).
/// </summary>
[ApiController]
[Route("api/client-errors")]
public class ClientErrorsController : ControllerBase
{
    private readonly ILogger<ClientErrorsController> _logger;
    private readonly IMemoryCache _cache;
    private readonly LoggingOptions _options;
    private const string CachePrefix = "clienterr_";

    public ClientErrorsController(
        ILogger<ClientErrorsController> logger,
        IMemoryCache cache,
        IOptions<LoggingOptions> options)
    {
        _logger = logger;
        _cache = cache;
        _options = options.Value;
    }

    [HttpPost]
    [Authorize]
    [ResponseCache(NoStore = true)]
    public IActionResult Report([FromBody] ClientErrorReport report)
    {
        if (report is null || !ModelState.IsValid)
            return ValidationProblem(ModelState);

        // قيود الحجم قابلة للضبط من الإعدادات (Logging:File) — ترصّ هنا قبل النقر، لا القطع التلقائي فقط.
        // المسار بلا استعلام/مقتطف (قد يحمل الاستعلام توكنات أو بيانات شخصية) ثم قصّ.
        report.Message = Clip(report.Message, _options.ClientErrorPayloadSizeLimit);
        report.Stack = report.Stack is null ? null : Clip(report.Stack, _options.ClientErrorStackLimit);
        report.Url = Clip(StripQuery(report.Url ?? ""), 500);
        report.Component = Clip(report.Component ?? "", 100);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var countKey = $"{CachePrefix}{userId}";
        var current = _cache.Get<int>(countKey);
        if (current >= _options.ClientErrorPerMinuteLimit)
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { message = "تم تجاوز حد الإبلاغات المسموحة في هذه الدقيقة" });

        _cache.Set(countKey, current + 1, TimeSpan.FromMinutes(1));

        // رسالة المستخدم محتملة أن تحوي نصًا حرًا، لذا تُسجَّل كخاصية منظمة (Message) إن لم تكن
        // مطابقة لقالب واحد معروف، ويُسجَّل المسار (بلا استعلام) والمكوّن معرّفيًا فقط.
        _logger.LogWarning(
            "Client error: {Message}, Component: {Component}, Path: {Url}",
            report.Message,
            report.Component ?? "",
            report.Url ?? "");

        return Accepted(new { ok = true });
    }

    private static string Clip(string value, int max)
    {
        if (value.Length <= max) return value;
        return value[..max];
    }

    private static string StripQuery(string url)
    {
        var cut = url.IndexOfAny(['?', '#']);
        return cut < 0 ? url : url[..cut];
    }
}

/// <summary>حمولة إبلاغ خطأ الواجهة (المستعرض). سقوف البنية حزام دفاع فوق قصّ الإعدادات.</summary>
public sealed class ClientErrorReport
{
    [Required(ErrorMessage = "الرسالة إلزامية")]
    [MaxLength(2048, ErrorMessage = "الرسالة لا تتجاوز 2048 حرفًا")]
    public string Message { get; set; } = "";

    [MaxLength(8192)]
    public string? Stack { get; set; }

    [MaxLength(100)]
    public string? Component { get; set; }

    [MaxLength(500)]
    public string? Url { get; set; }
}