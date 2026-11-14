using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Application.Tests;

/// <summary>
/// P0-03b: كل محاولة إعادة تنفيذ تحصل على نطاق DI/سياق EF جديدين. الاختبار
/// يحقن فشلًا عابرًا بعد <c>SaveChanges</c> في المحاولتين الأولى والثانية،
/// ويتحقق من أن المحاولة الثالثة وحدها تُثبَّت — لا صفوف مكررة ولا كتابة جزئية.
/// </summary>
public sealed class UnitOfWorkFactoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public UnitOfWorkFactoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<DocGeneratorDbContext>(options => options
            .UseSqlite(_connection)
            .ReplaceService<IExecutionStrategyFactory, RetryTwiceExecutionStrategyFactory>());
        services.AddScoped<IUnitOfWork, TestUnitOfWork>();
        services.AddScoped<IUnitOfWorkFactory, UnitOfWorkFactory>();
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Retryable_OnTransientFailures_UsesFreshScope_AndCommitsOnce()
    {
        using var scope = _provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();
        var attempts = 0;

        var result = await factory.ExecuteAsync(async (uow, ct) =>
        {
            attempts++;
            var testUow = (TestUnitOfWork)uow;
            testUow.Db.Branches.Add(new Branch
            {
                Name = $"فرع {attempts}",
                Code = $"U{attempts}",
            });
            await uow.SaveChangesAsync(ct);

            if (attempts <= 2)
                throw new TransientFailureException();

            return attempts;
        }, retryable: true);

        Assert.Equal(3, result);

        using var verifyScope = _provider.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.Branches.CountAsync());
    }

    [Fact]
    public async Task NonRetryable_DoesNotRetry_AndRollsBack()
    {
        using var scope = _provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();
        var attempts = 0;

        await Assert.ThrowsAsync<TransientFailureException>(() =>
            factory.ExecuteAsync<bool>(async (uow, ct) =>
            {
                attempts++;
                var testUow = (TestUnitOfWork)uow;
                testUow.Db.Branches.Add(new Branch { Name = "فرع", Code = "NR" });
                await uow.SaveChangesAsync(ct);
                throw new TransientFailureException();
            }, retryable: false));

        Assert.Equal(1, attempts);

        using var verifyScope = _provider.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        db.ChangeTracker.Clear();
        Assert.Equal(0, await db.Branches.CountAsync());
    }

    private sealed class TestUnitOfWork : IUnitOfWork
    {
        public TestUnitOfWork(DocGeneratorDbContext db) => Db = db;

        public DocGeneratorDbContext Db { get; }

        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Db.SaveChangesAsync(ct);

        public void ClearChanges() => Db.ChangeTracker.Clear();
    }

    /// <summary>إستراتيجية اختبارية تعيد المحاولة مرتين عند فشل عابر محقون.</summary>
    private sealed class RetryTwiceExecutionStrategyFactory : IExecutionStrategyFactory
    {
        private readonly ExecutionStrategyDependencies _dependencies;

        public RetryTwiceExecutionStrategyFactory(ExecutionStrategyDependencies dependencies)
            => _dependencies = dependencies;

        public IExecutionStrategy Create()
            => new RetryTwiceExecutionStrategy(_dependencies.CurrentContext.Context);
    }

    private sealed class RetryTwiceExecutionStrategy : IExecutionStrategy
    {
        private readonly NonRetryingExecutionStrategy _inner;

        public RetryTwiceExecutionStrategy(DbContext context)
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
            var failures = 0;
            while (true)
            {
                try
                {
                    return await _inner.ExecuteAsync(state, operation, verifySucceeded, cancellationToken);
                }
                catch (TransientFailureException) when (failures < 2)
                {
                    failures++;
                }
            }
        }
    }

    private sealed class TransientFailureException : Exception;
}
