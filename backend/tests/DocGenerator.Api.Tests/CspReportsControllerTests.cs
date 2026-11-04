using System.Text;
using System.Text.Json;
using DocGenerator.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DocGenerator.Api.Tests;

/// <summary>
/// W1 follow-up: جامع تقارير <c>CSP</c> يجب أن يسجّل **سطرًا ملخصًا واحدًا لكل طلب**
/// (لا سطرًا لكل تقرير)، وأن يجرّد الاستعلام/المقطع من العيّنة الأولى قبل التسجيل.
/// </summary>
public class CspReportsControllerTests
{
    [Fact]
    public async Task MultipleReports_LogSingleSummaryLine_WithoutQueryStrings()
    {
        const string json = """
            [
              {
                "type": "csp-violation",
                "body": {
                  "effectiveDirective": "script-src",
                  "documentURL": "https://app.example/documents/12?token=secret",
                  "blockedURL": "https://evil.example/x.js?tracking=1",
                  "disposition": "report"
                }
              },
              {
                "type": "csp-violation",
                "body": {
                  "effectiveDirective": "img-src",
                  "documentURL": "https://app.example/documents/12",
                  "blockedURL": "https://cdn.example/a.png",
                  "disposition": "report"
                }
              }
            ]
            """;

        var (result, messages) = await InvokeAsync(json);

        Assert.IsType<NoContentResult>(result);
        var message = Assert.Single(messages);
        Assert.Contains("Count=2", message, StringComparison.Ordinal);
        Assert.Contains("Truncated=False", message, StringComparison.Ordinal);
        Assert.Contains("Directives=script-src, img-src", message, StringComparison.Ordinal);
        Assert.Contains("FirstDocument=https://app.example/documents/12", message, StringComparison.Ordinal);
        Assert.DoesNotContain("token=secret", message, StringComparison.Ordinal);
        Assert.DoesNotContain("tracking=1", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LegacyReport_LogsOneLine_AndStripsQuery()
    {
        const string json = """
            {
              "csp-report": {
                "effective-directive": "style-src",
                "document-uri": "/documents/7?token=secret",
                "blocked-uri": "inline"
              }
            }
            """;

        var (result, messages) = await InvokeAsync(json);

        Assert.IsType<NoContentResult>(result);
        var message = Assert.Single(messages);
        Assert.Contains("Count=1", message, StringComparison.Ordinal);
        Assert.Contains("Directives=style-src", message, StringComparison.Ordinal);
        Assert.Contains("FirstDocument=/documents/7", message, StringComparison.Ordinal);
        Assert.DoesNotContain("token=secret", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MoreThanMaxReports_LogsOneTruncatedSummary()
    {
        var reports = Enumerable.Range(0, 25).Select(index => new
        {
            type = "csp-violation",
            body = new
            {
                effectiveDirective = "script-src",
                documentURL = $"/documents/{index}",
                blockedURL = "inline",
            },
        });
        var json = JsonSerializer.Serialize(reports);

        var (result, messages) = await InvokeAsync(json);

        Assert.IsType<NoContentResult>(result);
        var message = Assert.Single(messages);
        Assert.Contains("Count=20", message, StringComparison.Ordinal);
        Assert.Contains("Truncated=True", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonCspReports_DoNotLog()
    {
        const string json = """
            [{ "type": "deprecation", "body": { "id": "ignored" } }]
            """;

        var (result, messages) = await InvokeAsync(json);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(messages);
    }

    private static async Task<(IActionResult Result, List<string> Messages)> InvokeAsync(string json)
    {
        var logger = new CapturingLogger();
        var controller = new CspReportsController(logger);
        var body = Encoding.UTF8.GetBytes(json);
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(body);
        context.Request.ContentLength = body.Length;
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        var result = await controller.Report(CancellationToken.None);
        return (result, logger.Messages);
    }

    private sealed class CapturingLogger : ILogger<CspReportsController>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
