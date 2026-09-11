using System.Security.Claims;
using DocGenerator.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace DocGenerator.Api.Tests;

/// <summary>
/// وسيط إثراء <c>LogContext</c>: يضغط <c>TraceId</c>/<c>UserId</c>/<c>UserRole</c> لكل طلب،
/// و<c>anonymous</c>/<c>none</c> للمجهول — يُتحقق عبر مصرف ذاكرة يلتقط حدثًا يُسجَّل داخل
/// النطاق (<c>FromLogContext</c>)، مع إعادة <c>Log.Logger</c> العام في <c>finally</c> حتى لا
/// يتسرب الإعداد لاختبارات متوازية.
/// </summary>
public class EnricherMiddlewareTests
{
    private sealed class CollectSink : ILogEventSink
    {
        public readonly List<LogEvent> Events = new();
        public void Emit(LogEvent logEvent) { lock (Events) Events.Add(logEvent); }
    }

    private static DefaultHttpContext BuildContext(bool authenticated)
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = $"trace-{Guid.NewGuid():N}";
        if (authenticated)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "7"),
                new(ClaimTypes.Role, "Manager"),
            };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
        }
        return context;
    }

    private static IReadOnlyDictionary<string, object?> EmitInsideMiddleware(HttpContext context)
    {
        var sink = new CollectSink();
        var previous = Log.Logger;
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();
        try
        {
            var middleware = new RequestLoggingEnricherMiddleware(_ =>
            {
                Log.Information("probe");
                return Task.CompletedTask;
            });
            middleware.InvokeAsync(context).GetAwaiter().GetResult();
        }
        finally
        {
            Log.Logger = previous;
        }
        Assert.Single(sink.Events);
        var result = new Dictionary<string, object?>();
        foreach (var (key, value) in sink.Events[0].Properties)
            result[key] = value?.ToString()?.Trim('"');
        return result;
    }

    [Fact]
    public void Authenticated_PushesTraceIdUserIdAndRole()
    {
        var context = BuildContext(authenticated: true);

        var props = EmitInsideMiddleware(context);

        Assert.Equal(context.TraceIdentifier, props["TraceId"]);
        Assert.Equal("7", props["UserId"]);
        Assert.Equal("Manager", props["UserRole"]);
    }

    [Fact]
    public void Anonymous_PushesAnonymousFallbacks()
    {
        var context = BuildContext(authenticated: false);

        var props = EmitInsideMiddleware(context);

        Assert.Equal(context.TraceIdentifier, props["TraceId"]);
        Assert.Equal("anonymous", props["UserId"]);
        Assert.Equal("none", props["UserRole"]);
    }
}
