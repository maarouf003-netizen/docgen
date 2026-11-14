using System.Text.Json;
using DocGenerator.Api.Filters;
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
/// الاستعلامات/المقاطع من العناوين (<see cref="LogSanitizer"/>). ويُسجَّل **سطر ملخص واحد
/// لكل طلب** (العدد + التوجيهات + أول عيّنة) بدل سطر لكل تقرير — لا يغرق السجل عند
/// اجتماع عشرات المخالفات في صفحة واحدة.
/// </summary>
[ApiController]
[Route(CspReportUriPolicy.ReportRoute)]
public sealed class CspReportsController : ControllerBase
{
    private const int MaxBodyBytes = 16 * 1024;
    private const int MaxReports = 20;
    private const int MaxFieldLength = 500;
    private const int MaxDirectives = 5;
    private const int MaxDirectiveSummaryLength = 300;

    private readonly ILogger<CspReportsController> _logger;

    public CspReportsController(ILogger<CspReportsController> logger) => _logger = logger;

    [HttpPost]
    [SkipIdempotency]
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
            var cspReports = new List<JsonElement>(Math.Min(reports.Count, MaxReports + 1));
            foreach (var report in reports)
            {
                if (!IsCspViolation(report))
                    continue;

                cspReports.Add(report);
                if (cspReports.Count == MaxReports + 1)
                    break;
            }

            if (cspReports.Count > 0)
                LogSummary(cspReports);
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

    /// <summary>
    /// سطر واحد لكل طلب: العدد (بعد سقف <see cref="MaxReports"/>)، وهل قُصَّت الدفعة،
    /// وأول 5 توجيهات مختلفة، وأول عيّنة تشخيصية بعد التطهير. لا تُسجَّل بقية العيّنات
    /// عمدًا — السطر الملخص يكفي للرصد، والتفاصيل الكاملة تبقى في المتصفح للمطوّر.
    /// </summary>
    private void LogSummary(List<JsonElement> reports)
    {
        var truncated = reports.Count > MaxReports;
        if (truncated)
            reports.RemoveAt(reports.Count - 1);

        var directives = new List<string>();
        foreach (var report in reports)
        {
            var directive = DirectiveOf(ReportBody(report));
            if (directive == "-" || directives.Contains(directive, StringComparer.Ordinal))
                continue;

            directives.Add(directive);
            if (directives.Count == MaxDirectives)
                break;
        }

        var directiveSummary = directives.Count == 0
            ? "-"
            : LogSanitizer.Clip(string.Join(", ", directives), MaxDirectiveSummaryLength);

        var first = ReportBody(reports[0]);
        _logger.LogWarning(
            "CSP violations: Count={Count}, Truncated={Truncated}, Directives={Directives}, " +
            "FirstBlocked={BlockedUri}, FirstDocument={DocumentUri}, FirstDisposition={Disposition}, " +
            "FirstStatus={StatusCode}, FirstSource={SourceFile}",
            reports.Count,
            truncated,
            directiveSummary,
            Field(first, "blocked-uri", "blockedURL"),
            Field(first, "document-uri", "documentURL"),
            Field(first, "disposition"),
            Field(first, "status-code", "statusCode"),
            Field(first, "source-file", "sourceFile"));
    }

    private static JsonElement ReportBody(JsonElement report) =>
        report.TryGetProperty("body", out var payload)
            && payload.ValueKind == JsonValueKind.Object
                ? payload
                : report;

    private static string DirectiveOf(JsonElement body)
    {
        var directive = Field(body, "effective-directive", "effectiveDirective");
        return directive == "-"
            ? Field(body, "violated-directive", "violatedDirective")
            : directive;
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
                LogSanitizer.StripQueryAndFragment(raw ?? string.Empty), MaxFieldLength)?.Trim();
            return string.IsNullOrEmpty(clean) ? "-" : clean;
        }

        return "-";
    }
}
