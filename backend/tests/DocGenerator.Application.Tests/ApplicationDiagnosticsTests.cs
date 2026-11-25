using System.Collections.Concurrent;
using System.Diagnostics;
using DocGenerator.Application.Common.Observability;
using DocGenerator.Application.Common.Pipeline;

namespace DocGenerator.Application.Tests;

/// <summary>
/// `P5-01` — نشاط المعالجات: بلا مستمع لا نشاط ولا تغيّر سلوك؛ ومع مستمع يُنشأ
/// نشاط واحد لكل تنفيذ بوسوم اسم الطلب/المعالج/النتيجة، ويُعاد رمي نفس الاستثناء.
/// </summary>
[Collection(ObservabilityTestCollection.Name)]
[Trait("Category", "Unit")]
public class ApplicationDiagnosticsTests
{
    private sealed record EchoCommand(string Value) : ICommand<string>;

    private sealed class EchoCommandHandler : CommandHandlerBase<EchoCommand, string>
    {
        public EchoCommandHandler()
            : base(Array.Empty<IPipelineBehavior<EchoCommand, string>>())
        {
        }

        public Activity? ObservedActivity { get; private set; }

        protected override Task<string> HandleCoreAsync(EchoCommand command, CancellationToken ct)
        {
            ObservedActivity = Activity.Current;
            return Task.FromResult(command.Value);
        }
    }

    private sealed record ThrowingCommand : ICommand<bool>;

    private sealed class ThrowingCommandHandler : CommandHandlerBase<ThrowingCommand, bool>
    {
        private readonly Exception _failure;

        public ThrowingCommandHandler(Exception failure)
            : base(Array.Empty<IPipelineBehavior<ThrowingCommand, bool>>())
            => _failure = failure;

        public Activity? ObservedActivity { get; private set; }

        protected override Task<bool> HandleCoreAsync(ThrowingCommand command, CancellationToken ct)
        {
            ObservedActivity = Activity.Current;
            throw _failure;
        }
    }

    private sealed record EchoQuery(int Value) : IQuery<int>;

    private sealed class EchoQueryHandler : QueryHandlerBase<EchoQuery, int>
    {
        public EchoQueryHandler()
            : base(Array.Empty<IPipelineBehavior<EchoQuery, int>>())
        {
        }

        public Activity? ObservedActivity { get; private set; }

        protected override Task<int> HandleCoreAsync(EchoQuery query, CancellationToken ct)
        {
            ObservedActivity = Activity.Current;
            return Task.FromResult(query.Value + 1);
        }
    }

    private static ActivityListener Listen(Action<Activity>? stopped = null)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ApplicationDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stopped?.Invoke(activity),
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Fact]
    public async Task Command_WithoutListener_ExecutesNormallyAndCreatesNoActivity()
    {
        Assert.False(ApplicationDiagnostics.ActivitySource.HasListeners());

        var handler = new EchoCommandHandler();
        var result = await handler.HandleAsync(new EchoCommand("ok"));

        Assert.Equal("ok", result);
        Assert.Null(handler.ObservedActivity);
    }

    [Fact]
    public async Task ListenerThatDropsSampling_StillExecutesNormallyWithNoActivity()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ApplicationDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.None,
        };
        ActivitySource.AddActivityListener(listener);

        var handler = new EchoCommandHandler();
        var result = await handler.HandleAsync(new EchoCommand("ok"));

        Assert.Equal("ok", result);
        Assert.Null(handler.ObservedActivity);
    }

    [Fact]
    public async Task Command_WithListener_CreatesActivityWithRequestHandlerAndOutcomeTags()
    {
        Activity? stopped = null;
        using var listener = Listen(activity => stopped = activity);

        var handler = new EchoCommandHandler();
        var result = await handler.HandleAsync(new EchoCommand("ok"));

        Assert.Equal("ok", result);
        Assert.NotNull(handler.ObservedActivity);
        Assert.Same(handler.ObservedActivity, stopped);
        Assert.Equal("EchoCommand", stopped!.OperationName);
        Assert.Equal("EchoCommand", stopped.GetTagItem(ApplicationDiagnostics.RequestTypeTag));
        Assert.Equal("EchoCommandHandler", stopped.GetTagItem(ApplicationDiagnostics.HandlerTag));
        Assert.Equal(ApplicationDiagnostics.SuccessOutcome, stopped.GetTagItem(ApplicationDiagnostics.OutcomeTag));
        Assert.Equal(ActivityStatusCode.Ok, stopped.Status);
        Assert.Equal(
            new[]
            {
                ApplicationDiagnostics.HandlerTag,
                ApplicationDiagnostics.OutcomeTag,
                ApplicationDiagnostics.RequestTypeTag,
            },
            stopped.TagObjects.Select(tag => tag.Key).OrderBy(key => key, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Command_ThatThrows_RecordsExceptionStatusAndRethrowsSameInstance()
    {
        Activity? stopped = null;
        using var listener = Listen(activity => stopped = activity);

        var failure = new InvalidOperationException("boom-secret-value");
        var handler = new ThrowingCommandHandler(failure);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(new ThrowingCommand()));

        Assert.Same(failure, thrown);
        Assert.NotNull(handler.ObservedActivity);
        Assert.Same(handler.ObservedActivity, stopped);
        Assert.Equal(ApplicationDiagnostics.ExceptionOutcome, stopped!.GetTagItem(ApplicationDiagnostics.OutcomeTag));
        Assert.Equal(ActivityStatusCode.Error, stopped.Status);
        // PII: لا يُسجَّل نص الاستثناء في أي وسم.
        Assert.DoesNotContain(
            stopped.TagObjects,
            tag => string.Equals(tag.Value as string, failure.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Query_WithListener_IsInstrumentedWithSameContract()
    {
        Activity? stopped = null;
        using var listener = Listen(activity => stopped = activity);

        var handler = new EchoQueryHandler();
        var result = await handler.HandleAsync(new EchoQuery(41));

        Assert.Equal(42, result);
        Assert.NotNull(handler.ObservedActivity);
        Assert.Same(handler.ObservedActivity, stopped);
        Assert.Equal("EchoQuery", stopped!.OperationName);
        Assert.Equal("EchoQuery", stopped.GetTagItem(ApplicationDiagnostics.RequestTypeTag));
        Assert.Equal("EchoQueryHandler", stopped.GetTagItem(ApplicationDiagnostics.HandlerTag));
        Assert.Equal(ApplicationDiagnostics.SuccessOutcome, stopped.GetTagItem(ApplicationDiagnostics.OutcomeTag));
    }

    [Fact]
    public async Task ConcurrentCommands_CreateIndependentActivitiesWithUniqueIdentifiers()
    {
        var stopped = new ConcurrentBag<Activity>();
        using var listener = Listen(activity => stopped.Add(activity));

        var handlers = Enumerable.Range(0, 32).Select(_ => new EchoCommandHandler()).ToArray();
        var results = await Task.WhenAll(
            handlers.Select((handler, index) => handler.HandleAsync(new EchoCommand(index.ToString()))));

        Assert.Equal(Enumerable.Range(0, 32).Select(index => index.ToString()), results);
        Assert.Equal(32, stopped.Count);
        Assert.Equal(32, stopped.Select(activity => activity.TraceId).Distinct().Count());
        Assert.Equal(32, stopped.Select(activity => activity.SpanId).Distinct().Count());
        Assert.All(stopped, activity => Assert.Equal(
            ApplicationDiagnostics.SuccessOutcome,
            activity.GetTagItem(ApplicationDiagnostics.OutcomeTag)));
        Assert.All(handlers, handler => Assert.NotNull(handler.ObservedActivity));
    }
}
