using System.Diagnostics;
using DocGenerator.Api.Observability;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace DocGenerator.Api.Tests;

/// <summary>
/// `P5-01` — إثراء السجل بمعرّف التتبع: لا يضيف شيئًا بلا نشاط، ويضيف
/// `TraceId`/`SpanId` الصحيحين داخل نشاط، بلا تغيير نص أي رسالة.
/// </summary>
[Collection(ApiObservabilityTestCollection.Name)]
[Trait("Category", "Unit")]
public class TraceContextEnricherTests
{
    private const string TestSourceName = "DocGenerator.Tests.TraceEnricher";

    [Fact]
    public void WithoutActivity_AddsNoProperties()
    {
        Assert.Null(Activity.Current);

        var logEvent = NewLogEvent();
        new TraceContextEnricher().Enrich(logEvent, new StubPropertyFactory());

        Assert.False(logEvent.Properties.ContainsKey(TraceContextEnricher.TraceIdPropertyName));
        Assert.False(logEvent.Properties.ContainsKey(TraceContextEnricher.SpanIdPropertyName));
    }

    [Fact]
    public void WithActivity_AddsTraceAndSpanIdentifiers()
    {
        using var listener = Listen();
        using var source = new ActivitySource(TestSourceName);
        using var activity = source.StartActivity("test");
        Assert.NotNull(activity);

        var logEvent = NewLogEvent();
        new TraceContextEnricher().Enrich(logEvent, new StubPropertyFactory());

        Assert.Equal(
            activity.TraceId.ToHexString(),
            ((ScalarValue)logEvent.Properties[TraceContextEnricher.TraceIdPropertyName]).Value);
        Assert.Equal(
            activity.SpanId.ToHexString(),
            ((ScalarValue)logEvent.Properties[TraceContextEnricher.SpanIdPropertyName]).Value);
    }

    [Fact]
    public void ExistingProperty_IsNotOverwritten()
    {
        using var listener = Listen();
        using var source = new ActivitySource(TestSourceName);
        using var activity = source.StartActivity("test");
        Assert.NotNull(activity);

        var logEvent = NewLogEvent();
        logEvent.AddPropertyIfAbsent(new LogEventProperty(
            TraceContextEnricher.TraceIdPropertyName, new ScalarValue("already-set")));

        new TraceContextEnricher().Enrich(logEvent, new StubPropertyFactory());

        Assert.Equal(
            "already-set",
            ((ScalarValue)logEvent.Properties[TraceContextEnricher.TraceIdPropertyName]).Value);
    }

    private static ActivityListener Listen()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TestSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static LogEvent NewLogEvent() => new(
        DateTimeOffset.UtcNow,
        LogEventLevel.Information,
        exception: null,
        messageTemplate: new MessageTemplate(Array.Empty<MessageTemplateToken>()),
        properties: Array.Empty<LogEventProperty>());

    private sealed class StubPropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
            => new(name, new ScalarValue(value));
    }
}
