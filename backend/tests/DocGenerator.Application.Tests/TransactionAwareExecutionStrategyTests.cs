using DocGenerator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DocGenerator.Application.Tests;

/// <summary>
/// P0-03a: الإستراتيجية الواعية بالمعاملة تختار غير المعيدة **عند التنفيذ**
/// داخل <c>CurrentTransaction</c>، حتى لو كان المزوّد مضبوطًا على إعادة المحاولة.
/// </summary>
public sealed class TransactionAwareExecutionStrategyTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TransactionAwareExecutionStrategyTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task OutsideTransaction_UsesRetryingStrategy()
    {
        using var db = NewContext();
        var attempts = 0;

        var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(
            0,
            async (_, _, _) =>
            {
                attempts++;
                if (attempts == 1)
                    throw new TransientFailureException();
                return 42;
            },
            verifySucceeded: null);

        Assert.Equal(2, attempts);
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task InsideTransaction_DoesNotRetry()
    {
        using var db = NewContext();
        var attempts = 0;

        using var transaction = db.Database.BeginTransaction();
        await Assert.ThrowsAsync<TransientFailureException>(() =>
            db.Database.CreateExecutionStrategy().ExecuteAsync<int, int>(
                0,
                (_, _, _) =>
                {
                    attempts++;
                    throw new TransientFailureException();
                },
                verifySucceeded: null));

        Assert.Equal(1, attempts);
    }

    private DocGeneratorDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<DocGeneratorDbContext>()
            .UseSqlite(_connection)
            .ReplaceService<IExecutionStrategyFactory, RetryEnabledFactory>()
            .Options;
        return new DocGeneratorDbContext(options);
    }

    /// <summary>يفرض إعدادات إعادة المحاولة دون تغيير الحالة العامة في اختبارات أخرى.</summary>
    private sealed class RetryEnabledFactory : TransactionAwareExecutionStrategyFactory
    {
        public RetryEnabledFactory(ExecutionStrategyDependencies dependencies)
            : base(dependencies)
        {
        }

        protected override DatabaseRetrySettings.RetrySettings RetrySettings
            => new(MaxRetryCount: 3, MaxRetryDelaySeconds: 1);

        protected override IExecutionStrategy CreateRetryingStrategy(
            DatabaseRetrySettings.RetrySettings settings)
            => new RetryOnceStrategy(Dependencies.CurrentContext.Context);
    }

    /// <summary>إستراتيجية اختبارية تعيد العملية مرة واحدة عند الفشل العابر.</summary>
    private sealed class RetryOnceStrategy : IExecutionStrategy
    {
        private readonly NonRetryingExecutionStrategy _inner;

        public RetryOnceStrategy(DbContext context)
            => _inner = new NonRetryingExecutionStrategy(context);

        public bool RetriesOnFailure => true;

        public TResult Execute<TState, TResult>(
            TState state,
            Func<DbContext, TState, TResult> operation,
            Func<DbContext, TState, ExecutionResult<TResult>>? verifySucceeded)
            => _inner.Execute(state, operation, verifySucceeded);

        public async Task<TResult> ExecuteAsync<TState, TResult>(
            TState state,
            Func<DbContext, TState, CancellationToken, Task<TResult>> operation,
            Func<DbContext, TState, CancellationToken, Task<ExecutionResult<TResult>>>? verifySucceeded,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await _inner.ExecuteAsync(state, operation, verifySucceeded, cancellationToken);
            }
            catch (TransientFailureException)
            {
                return await _inner.ExecuteAsync(state, operation, verifySucceeded, cancellationToken);
            }
        }
    }

    private sealed class TransientFailureException : Exception;
}
