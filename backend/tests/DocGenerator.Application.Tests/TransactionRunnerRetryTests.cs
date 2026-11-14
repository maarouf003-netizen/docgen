using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DocGenerator.Application.Tests;

/// <summary>
/// انحدار F-02: <see cref="TransactionRunner"/> يجب ألا يعيد تنفيذ معاملة تفاعلية
/// ولو كان المزوّد مضبوطًا على إستراتيجية معيدة للمحاولة. الاختبار يضبط إستراتيجية
/// تعيد المحاولة مرة واحدة عند استثناء عابر محقون، ويثبت أن الـdelegate يُنفَّذ مرة
/// واحدة فقط وأن الفشل يظهر للمستخدم مع تراجع كامل — لا إدراج مكرر ولا كتابة جزئية.
/// </summary>
public sealed class TransactionRunnerRetryTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public TransactionRunnerRetryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task RunAsync_WithRetryingStrategyConfigured_DoesNotRetryTheDelegate()
    {
        var options = new DbContextOptionsBuilder<DocGeneratorDbContext>()
            .UseSqlite(_connection)
            .ReplaceService<IExecutionStrategyFactory, RetryOnceExecutionStrategyFactory>()
            .Options;
        await using var db = new DocGeneratorDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var tx = new TransactionRunner(db);
        var attempts = 0;

        await Assert.ThrowsAnyAsync<Exception>(() =>
            tx.RunAsync(async token =>
            {
                attempts++;
                db.Branches.Add(new Branch { Name = "عابر", Code = "TRX" });
                await db.SaveChangesAsync(token);
                if (attempts == 1)
                    throw new TransientFailureException();
            }));

        Assert.Equal(1, attempts);
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Branches.ToListAsync());
    }

    [Fact]
    public async Task RunAsync_WithRetryingStrategyConfigured_DoesNotRetryBeforeAnyWrite()
    {
        var options = new DbContextOptionsBuilder<DocGeneratorDbContext>()
            .UseSqlite(_connection)
            .ReplaceService<IExecutionStrategyFactory, RetryOnceExecutionStrategyFactory>()
            .Options;
        await using var db = new DocGeneratorDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var tx = new TransactionRunner(db);
        var attempts = 0;

        await Assert.ThrowsAsync<TransientFailureException>(() =>
            tx.RunAsync(async token =>
            {
                attempts++;
                if (attempts == 1)
                    throw new TransientFailureException();

                db.Branches.Add(new Branch { Name = "عابر", Code = "TRX" });
                await db.SaveChangesAsync(token);
            }));

        Assert.Equal(1, attempts);
        Assert.Empty(await db.Branches.ToListAsync());
    }

    [Fact]
    public async Task OutboxExecuteInTransaction_WithRetryingStrategyConfigured_DoesNotRetry()
    {
        var options = new DbContextOptionsBuilder<DocGeneratorDbContext>()
            .UseSqlite(_connection)
            .ReplaceService<IExecutionStrategyFactory, RetryOnceExecutionStrategyFactory>()
            .Options;
        await using var db = new DocGeneratorDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var store = new HeadAlertOutboxStore(db);
        var attempts = 0;

        await Assert.ThrowsAsync<TransientFailureException>(() =>
            store.ExecuteInTransactionAsync(async token =>
            {
                attempts++;
                db.Branches.Add(new Branch { Name = "صندوق", Code = "OBX" });
                await db.SaveChangesAsync(token);
                if (attempts == 1)
                    throw new TransientFailureException();
                return true;
            }));

        Assert.Equal(1, attempts);
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Branches.ToListAsync());
    }

    [Fact]
    public async Task RunAsync_WithRetryingStrategyConfigured_CommitsOnFirstAttempt()
    {
        var options = new DbContextOptionsBuilder<DocGeneratorDbContext>()
            .UseSqlite(_connection)
            .ReplaceService<IExecutionStrategyFactory, RetryOnceExecutionStrategyFactory>()
            .Options;
        await using var db = new DocGeneratorDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var tx = new TransactionRunner(db);
        var attempts = 0;

        await tx.RunAsync(async token =>
        {
            attempts++;
            db.Branches.Add(new Branch { Name = "ناجح", Code = "OK" });
            await db.SaveChangesAsync(token);
        });

        Assert.Equal(1, attempts);
        Assert.Equal(1, await db.Branches.CountAsync());
    }

    /// <summary>
    /// إستراتيجية اختبارية تحاكي <c>EnableRetryOnFailure</c>: تعيد العملية مرة واحدة
    /// عند <see cref="TransientFailureException"/> فقط. قبل إصلاح F-02 كان
    /// <see cref="TransactionRunner"/> يستخدمها فيعيد الـdelegate وينتج صفوفًا مكررة.
    /// </summary>
    private sealed class RetryOnceExecutionStrategyFactory : IExecutionStrategyFactory
    {
        private readonly ExecutionStrategyDependencies _dependencies;

        public RetryOnceExecutionStrategyFactory(ExecutionStrategyDependencies dependencies)
            => _dependencies = dependencies;

        public IExecutionStrategy Create()
            => new RetryOnceExecutionStrategy(_dependencies.CurrentContext.Context);
    }

    private sealed class RetryOnceExecutionStrategy : IExecutionStrategy
    {
        private readonly NonRetryingExecutionStrategy _inner;

        public RetryOnceExecutionStrategy(DbContext context)
            => _inner = new NonRetryingExecutionStrategy(context);

        public bool RetriesOnFailure => true;

        public TResult Execute<TState, TResult>(
            TState state,
            Func<DbContext, TState, TResult> operation,
            Func<DbContext, TState, ExecutionResult<TResult>>? verifySucceeded)
            => throw new NotSupportedException("المسار المتزامن غير مستخدم في المعاملات");

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
