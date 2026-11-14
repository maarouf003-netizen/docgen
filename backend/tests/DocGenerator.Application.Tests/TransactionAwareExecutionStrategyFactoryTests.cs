using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace DocGenerator.Application.Tests;

/// <summary>
/// P0-03a: الإستراتيجية الافتراضية (RetryMaxCount=0) غير معيدة، ويمكن استبدال
/// مصنع EF دون اتصال بقاعدة. اختبار المعاملة الحيّة على PostgreSQL في
/// <c>TransactionAwareExecutionStrategyPostgresTests</c> (CI).
/// </summary>
public sealed class TransactionAwareExecutionStrategyFactoryTests
{
    [Fact]
    public void DefaultConfiguration_ReturnsNonRetrying()
    {
        var options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=unused;Username=unused")
            .ReplaceService<IExecutionStrategyFactory, TransactionAwareExecutionStrategyFactory>()
            .Options;

        using var db = new DocGeneratorPostgresDbContext(options);
        Assert.False(db.Database.CreateExecutionStrategy().RetriesOnFailure);
    }
}
