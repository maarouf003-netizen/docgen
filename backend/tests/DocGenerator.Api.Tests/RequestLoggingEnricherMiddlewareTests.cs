using DocGenerator.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace DocGenerator.Api.Tests;

/// <summary>
/// وسم <c>CursorPaging</c> في سجلات الطلبات (`PR‑6c`): يميّز طلب المؤشر من طلب
/// الإزاحة عند تشخيص «صف ظهر ناقصًا»؛ يُضاف فقط عند وجود <c>cursor</c> في الاستعلام.
/// </summary>
public class RequestLoggingEnricherMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithCursorQuery_PushesCursorPagingProperty()
    {
        var sink = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();
        var previous = Log.Logger;
        Log.Logger = logger;
        try
        {
            var context = new DefaultHttpContext();
            context.Request.QueryString = new QueryString("?cursor=2026-11-02T09%3A30%3A15Z%7C5");

            await new RequestLoggingEnricherMiddleware(_ =>
            {
                Log.Information("probe");
                return Task.CompletedTask;
            }).InvokeAsync(context);

            var probe = Assert.Single(sink.Events);
            var cursorPaging = Assert.IsType<ScalarValue>(probe.Properties["CursorPaging"]);
            Assert.Equal(true, cursorPaging.Value);
            // الحقول القائمة لم تتأثر.
            Assert.True(probe.Properties.ContainsKey("TraceId"));
            Assert.True(probe.Properties.ContainsKey("UserId"));
            Assert.True(probe.Properties.ContainsKey("UserRole"));
        }
        finally
        {
            Log.Logger = previous;
        }
    }

    [Fact]
    public async Task InvokeAsync_WithoutCursorQuery_DoesNotPushCursorPagingProperty()
    {
        var sink = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();
        var previous = Log.Logger;
        Log.Logger = logger;
        try
        {
            var context = new DefaultHttpContext();
            context.Request.QueryString = new QueryString("?page=2&pageSize=100");

            await new RequestLoggingEnricherMiddleware(_ =>
            {
                Log.Information("probe");
                return Task.CompletedTask;
            }).InvokeAsync(context);

            var probe = Assert.Single(sink.Events);
            Assert.False(probe.Properties.ContainsKey("CursorPaging"));
        }
        finally
        {
            Log.Logger = previous;
        }
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

    /// <summary>مستقبِل أحداث سجل في الذاكرة لفحص الخصائص المدفوعة في <c>LogContext</c>.</summary>
    private sealed class CollectingSink : ILogEventSink
    {
        private readonly List<LogEvent> _events = new();

        public IReadOnlyList<LogEvent> Events
        {
            get
            {
                lock (_events)
                    return _events.ToList();
            }
        }

        public void Emit(LogEvent logEvent)
        {
            lock (_events)
                _events.Add(logEvent);
        }
    }
}
