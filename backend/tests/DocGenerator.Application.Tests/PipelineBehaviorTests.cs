using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Tests;

/// <summary>
/// عقد خط الأنابيب (P4-01a): ترتيب السلوكيات، تحقق يمنع الآثار الجانبية،
/// ومعاملة/تدقيق لا يعملان إلا للطلبات الموسومة صراحةً.
/// </summary>
public class PipelineBehaviorTests
{
    private sealed record PlainCommand(List<string> Trace) : ICommand<string>;

    private sealed record FullCommand(List<string> Trace) : ICommand<string>, ITransactionalRequest, IAuditedRequest
    {
        public string AuditAction => "test_action";
        public int? AuditDocumentId => 7;
        public string? AuditDocumentType => "test";
        public string? AuditDetails => "تفاصيل";
    }

    private sealed class TestHandler<TCommand> : CommandHandlerBase<TCommand, string>
        where TCommand : ICommand<string>
    {
        private readonly List<string> _trace;

        public TestHandler(List<string> trace, IEnumerable<IPipelineBehavior<TCommand, string>> behaviors)
            : base(behaviors) => _trace = trace;

        protected override Task<string> HandleCoreAsync(TCommand command, CancellationToken ct)
        {
            _trace.Add("core");
            return Task.FromResult("ok");
        }
    }

    private sealed class TestQueryHandler : QueryHandlerBase<TestQuery, int>
    {
        public TestQueryHandler() : base(Array.Empty<IPipelineBehavior<TestQuery, int>>()) { }

        protected override Task<int> HandleCoreAsync(TestQuery query, CancellationToken ct)
            => Task.FromResult(query.Value + 1);
    }

    private sealed record TestQuery(int Value) : IQuery<int>;

    private sealed class FakeTransactionRunner(List<string> trace) : ITransactionRunner
    {
        public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
        {
            trace.Add("tx-start");
            try
            {
                return await action(ct);
            }
            finally
            {
                trace.Add("tx-end");
            }
        }

        public async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
        {
            trace.Add("tx-start");
            try
            {
                await action(ct);
            }
            finally
            {
                trace.Add("tx-end");
            }
        }

        public Task<T> RunAsync<T>(
            Func<IUnitOfWork, CancellationToken, Task<T>> action, bool retryable, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeAuditLogger(List<string> trace) : IAuditLogger
    {
        public Task LogAsync(string? userName, string actionType, int? documentId = null,
            string? documentType = null, string? details = null, CancellationToken ct = default)
        {
            trace.Add("audit");
            return Task.CompletedTask;
        }

        public Task LogManyAsync(IReadOnlyList<AuditLogEntry> entries, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task LogDocumentChangeAsync(string? userName, string actionType, int documentId,
            string? documentType, string details, IReadOnlyList<DocumentFieldChange> changes,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeValidator(List<string> trace, List<string> errors) : IRequestValidator<FullCommand>
    {
        public IReadOnlyList<string> Validate(FullCommand request)
        {
            trace.Add("validate");
            return errors;
        }
    }

    private static (TestHandler<FullCommand> Handler, List<string> Trace) BuildFullPipeline(
        List<string>? errors = null)
    {
        var trace = new List<string>();
        var behaviors = new IPipelineBehavior<FullCommand, string>[]
        {
            new ValidationBehavior<FullCommand, string>(new[] { new FakeValidator(trace, errors ?? new List<string>()) }),
            new TransactionBehavior<FullCommand, string>(new FakeTransactionRunner(trace)),
            new AuditBehavior<FullCommand, string>(new FakeAuditLogger(trace)),
        };
        return (new TestHandler<FullCommand>(trace, behaviors), trace);
    }

    [Fact]
    public async Task FullPipeline_RunsValidationThenTransactionThenAuditInOrder()
    {
        var (handler, trace) = BuildFullPipeline();

        var result = await handler.HandleAsync(new FullCommand(trace));

        Assert.Equal("ok", result);
        Assert.Equal(new[] { "validate", "tx-start", "core", "audit", "tx-end" }, trace);
    }

    [Fact]
    public async Task ValidationFailure_StopsBeforeAnySideEffect()
    {
        var (handler, trace) = BuildFullPipeline(new List<string> { "خطأ أول", "خطأ ثان" });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(new FullCommand(trace)));

        Assert.Contains("خطأ أول", ex.Message, StringComparison.Ordinal);
        Assert.Contains("خطأ ثان", ex.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { "validate" }, trace);
    }

    [Fact]
    public async Task UnmarkedCommand_SkipsTransactionAndAudit()
    {
        var trace = new List<string>();
        var behaviors = new IPipelineBehavior<PlainCommand, string>[]
        {
            new TransactionBehavior<PlainCommand, string>(new FakeTransactionRunner(trace)),
            new AuditBehavior<PlainCommand, string>(new FakeAuditLogger(trace)),
        };
        var handler = new TestHandler<PlainCommand>(trace, behaviors);

        var result = await handler.HandleAsync(new PlainCommand(trace));

        Assert.Equal("ok", result);
        Assert.Equal(new[] { "core" }, trace);
    }

    [Fact]
    public async Task QueryHandlerBase_ExecutesCore()
    {
        var result = await new TestQueryHandler().HandleAsync(new TestQuery(41));

        Assert.Equal(42, result);
    }
}
