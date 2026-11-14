using DocGenerator.Api.Tests.TestHelpers;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace DocGenerator.Api.Tests;

/// <summary>
/// P0-03a على PostgreSQL الحقيقي: الإستراتيجية المعيدة خارج المعاملة تصبح
/// غير معيدة بمجرد وجود <c>CurrentTransaction</c>. يعمل فقط عند تعريف
/// <c>DOCGEN_TEST_POSTGRES</c> (وفي CI يفشل صراحةً عند غيابه).
/// </summary>
public sealed class TransactionAwareExecutionStrategyPostgresTests
{
    [Fact]
    public async Task InsideTransaction_ReturnsNonRetrying_OnPostgres()
    {
        var baseConnectionString = PostgresTestEnvironment.RequireOrSkip();
        if (baseConnectionString is null)
            return;

        var databaseName = "docgen_txaware_" + Guid.NewGuid().ToString("N")[..12];
        var connectionString = await PostgresTestDatabase.CreateAsync(baseConnectionString, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
                .UseNpgsql(connectionString)
                .ReplaceService<IExecutionStrategyFactory, RetryEnabledFactory>()
                .Options;
            await using var db = new DocGeneratorPostgresDbContext(options);

            Assert.True(db.Database.CreateExecutionStrategy().RetriesOnFailure);

            // إنشاء الإستراتيجية داخل معاملة لا يرمي/لا يتكرر؛ الاختيار الفعلي
            // غير المعيد مُغطّى سلوكيًا في TransactionAwareExecutionStrategyTests.
            await using var transaction = await db.Database.BeginTransactionAsync();
            _ = db.Database.CreateExecutionStrategy();
            await transaction.RollbackAsync();
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(baseConnectionString, databaseName);
        }
    }

    /// <summary>يفرض إعدادات إعادة المحاولة دون تغيير الحالة العامة.</summary>
    private sealed class RetryEnabledFactory : TransactionAwareExecutionStrategyFactory
    {
        public RetryEnabledFactory(ExecutionStrategyDependencies dependencies)
            : base(dependencies)
        {
        }

        protected override DatabaseRetrySettings.RetrySettings RetrySettings
            => new(MaxRetryCount: 3, MaxRetryDelaySeconds: 1);
    }
}
