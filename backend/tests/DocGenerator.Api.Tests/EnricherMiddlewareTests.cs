using System.Security.Claims;
using DocGenerator.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace DocGenerator.Api.Tests;

/// <summary>
/// وسيط إثراء <c>LogContext</c>: يضغط <c>TraceId</c>/<c>UserId</c>/<c>UserRole</c> لكل طلب،
/// و<c>anonymous</c>/<c>none</c> للمجهول، ويوسم طلبات المؤشر بـ<c>CursorPaging</c> —
/// يُتحقق عبر مصرف ذاكرة يلتقط حدثًا يُسجَّل داخل النطاق (<c>FromLogContext</c>)، مع
/// إعادة <c>Log.Logger</c> العام في <c>finally</c> حتى لا يتسرب الإعداد لاختبارات متوازية.
/// </summary>
/// <remarks>
/// عزل إجباري عن التوازي: الاختبار يستبدل `Log.Logger` العام للعملية، وأي إقلاع
/// تطبيق (`ApiFactory` → `UseSerilog`) في خيط موازٍ يستبدله في النافذة نفسها
/// فيضيع حدث المسبار (فشل عابر مثبّت). شغّل هذه الفئة تسلسليًا وحدها:
/// `dotnet test --filter Category=Sequential` بعد `dotnet test --filter Category!=Sequential`.
/// </remarks>
[Trait("Category", "Sequential")]
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

    [Fact]
    public void CursorQuery_PushesCursorPagingTrue()
    {
        // وسم `PR‑6c` للرصد: يميّز طلب المؤشر من طلب الإزاحة في السجلات.
        var context = BuildContext(authenticated: true);
        context.Request.QueryString = new QueryString("?cursor=2026-11-02T09%3A30%3A15Z%7C5");

        var props = EmitInsideMiddleware(context);

        Assert.Equal("True", props["CursorPaging"]);
        // الحقول القائمة لم تتأثر بالوسم الجديد.
        Assert.Equal(context.TraceIdentifier, props["TraceId"]);
        Assert.Equal("7", props["UserId"]);
    }

    [Fact]
    public void OffsetQuery_DoesNotPushCursorPaging()
    {
        var context = BuildContext(authenticated: true);
        context.Request.QueryString = new QueryString("?page=2&pageSize=100");

        var props = EmitInsideMiddleware(context);

        Assert.False(props.ContainsKey("CursorPaging"));
    }

    [Fact]
    public void IsCursorPaging_DetectsCursorKeyOnly()
    {
        var withCursor = new DefaultHttpContext();
        withCursor.Request.QueryString = new QueryString("?cursor=abc");
        var withoutCursor = new DefaultHttpContext();
        withoutCursor.Request.QueryString = new QueryString("?page=1");

        Assert.True(RequestLoggingEnricherMiddleware.IsCursorPaging(withCursor.Request));
        Assert.False(RequestLoggingEnricherMiddleware.IsCursorPaging(withoutCursor.Request));
    }
}
