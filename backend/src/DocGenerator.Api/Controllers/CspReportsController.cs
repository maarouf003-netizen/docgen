using System.Text.Json;
using DocGenerator.Api.Middleware;
using DocGenerator.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DocGenerator.Api.Controllers;

/// <summary>
/// جامع تقارير <c>CSP</c> على الأصل نفسه (FR-010 / W1): يقبل صيغة <c>report-uri</c>
/// القديمة (<c>application/csp-report</c>) وصيغة <c>Reporting API</c> الحديثة
/// (<c>application/reports+json</c>)، ويسجّل الحقول التشخيصية فقط بعد تطهيرها.
///
/// **لماذا عامة بلا مصادقة:** المتصفح يرسل التقرير تلقائيًا، وقد لا يرسل جلسة ولا كوكي
/// <c>CSRF</c>؛ لذلك تُستثنى من <see cref="CsrfMiddleware"/> بمسارها الدقيق. الحماية
/// من الإغراق: حد معدل لكل <c>IP</c> + سقف حجم الجسم + سقف عدد التقارير + قصّ الحقول.
/// **لا تُسجَّل الحمولة الخام** ولا <c>script-sample</c> (قد يحمل كودًا/بيانات)، وتُجرَّد
/// الاستعلامات/المقاطع من العناوين (<see cref="LogSanitizer"/>).
/// </summary>
[ApiController]
[Route("api/csp-report")]
public sealed class CspReportsController : ControllerBase
{
    private const int MaxBodyBytes = 16 * 1024;
    private const int MaxReports = 20;
    private const int MaxFieldLength = 500;

    private readonly ILogger<CspReportsController> _logger;

    public CspReportsController(ILogger<CspReportsController> logger) => _logger = logger;

    [HttpPost]
    [AllowAnonymous]
    [Consumes("application/csp-report", "application/reports+json", "application/json")]
    [EnableRateLimiting(RateLimitingSetup.CspReportPolicy)]
    [RequestSizeLimit(MaxBodyBytes)]
    [ResponseCache(NoStore = true)]
    public async Task<IActionResult> Report(CancellationToken ct)
    {
        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(
                Request.Body,
                new JsonDocumentOptions { MaxDepth = 8 },
                ct);
        }
        catch (JsonException)
        {
            return BadRequest(new { message = "تقرير CSP غير صالح" });
        }

        using (document)
        {
            var reports = ExtractReports(document.RootElement);
            var logged = 0;
            foreach (var report in reports)
            {
                if (logged == MaxReports)
                    break;
                if (!IsCspViolation(report))
                    continue;

                LogViolation(report);
                logged++;
            }

            if (reports.Count > MaxReports)
                _logger.LogWarning(
                    "CSP report batch truncated: received {Received}, logged {Logged}",
                    reports.Count,
                    logged);
        }

        return NoContent();
    }

    /// <summary>
    /// يفكّ الصيغتين: كائنًا يحمل <c>csp-report</c> (القديمة)، أو مصفوفة تقارير
    /// <c>Reporting API</c>، أو كائن تقرير مباشرًا (بعض الوكلاء).
    /// </summary>
    private static List<JsonElement> ExtractReports(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object)
                .ToList();

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("csp-report", out var legacy)
                && legacy.ValueKind == JsonValueKind.Object)
                return [legacy];
            return [root];
        }

        return [];
    }

    /// <summary>تقارير <c>Reporting API</c> قد تحمل أنواعًا أخرى (deprecation/intervention) — تُتجاهل.</summary>
    private static bool IsCspViolation(JsonElement report) =>
        !report.TryGetProperty("type", out var type)
        || type.ValueKind != JsonValueKind.String
        || string.Equals(type.GetString(), "csp-violation", StringComparison.OrdinalIgnoreCase);

    private void LogViolation(JsonElement report)
    {
        var body = report.TryGetProperty("body", out var payload)
            && payload.ValueKind == JsonValueKind.Object
                ? payload
                : report;

        var directive = Field(body, "effective-directive", "effectiveDirective");
        if (directive == "-")
            directive = Field(body, "violated-directive", "violatedDirective");

        _logger.LogWarning(
            "CSP violation: Directive={Directive}, Blocked={BlockedUri}, Document={DocumentUri}, " +
            "Disposition={Disposition}, Status={StatusCode}, Source={SourceFile}",
            directive,
            Field(body, "blocked-uri", "blockedURL"),
            Field(body, "document-uri", "documentURL"),
            Field(body, "disposition"),
            Field(body, "status-code", "statusCode"),
            Field(body, "source-file", "sourceFile"));
    }

    /// <summary>يقرأ أول حقل موجود من الأسماء المدعومة، ويطهّره ويقصّه؛ الغائب = <c>-</c>.</summary>
    private static string Field(JsonElement body, params string[] names)
    {
        foreach (var name in names)
        {
            if (!body.TryGetProperty(name, out var value))
                continue;
            if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                continue;

            var raw = value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : value.GetRawText();
            var clean = LogSanitizer.SanitizeForLog(
                StripQueryAndFragment(raw ?? string.Empty), MaxFieldLength)?.Trim();
            return string.IsNullOrEmpty(clean) ? "-" : clean;
        }

        return "-";
    }

    /// <summary>يحذف الاستعلام/المقطع من العناوين (قد يحمل توكنات أو معرّفات حساسة).</summary>
    private static string StripQueryAndFragment(string value)
    {
        var cut = value.IndexOfAny(['?', '#']);
        return cut < 0 ? value : value[..cut];
    }
}
