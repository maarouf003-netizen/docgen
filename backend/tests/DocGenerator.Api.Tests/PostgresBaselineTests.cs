using DocGenerator.Api.Tests.TestHelpers;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Api.Tests;

/// <summary>
/// حراسة سلسلة هجرات PostgreSQL بعد P0-02: تطبّق Baseline ثم الهجرات التراكمية
/// على قاعدة فارغة معزولة، وتتحقق من السجل ومن بقاء القيود/الفهارس الحرجة
/// ومن نظافة النموذج.
/// يعمل فقط عند تعريف <c>DOCGEN_TEST_POSTGRES</c> (وفي CI مع
/// <c>DOCGEN_REQUIRE_POSTGRES=1</c> يفشل صراحةً عند غيابه).
/// </summary>
public sealed class PostgresBaselineTests
{
    [Fact]
    public async Task Migrations_ApplyToEmptyDatabase_AndCreateFinalSchema()
    {
        var baseConnectionString = PostgresTestEnvironment.RequireOrSkip();
        if (baseConnectionString is null)
            return;

        var databaseName = "docgen_baseline_" + Guid.NewGuid().ToString("N")[..12];
        var connectionString = await PostgresTestDatabase.CreateAsync(baseConnectionString, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var db = new DocGeneratorPostgresDbContext(options);

            await db.Database.MigrateAsync();

            Assert.False(db.Database.HasPendingModelChanges());

            var migrations = await db.Database
                .SqlQueryRaw<string>("SELECT \"MigrationId\" AS \"Value\" FROM \"__EFMigrationsHistory\"")
                .ToListAsync();
            Assert.Equal(2, migrations.Count);
            Assert.Contains(migrations, m => m.Contains("InitialPostgresBaseline", StringComparison.Ordinal));
            Assert.Contains(migrations, m => m.Contains("AddIdempotencyResponseMetadata", StringComparison.Ordinal));

            var statusColumn = await db.Database
                .SqlQueryRaw<long>(
                    """
                    SELECT COUNT(*)::bigint AS "Value"
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'IdempotencyKeys'
                      AND column_name = 'StatusCode'
                    """)
                .SingleAsync();
            Assert.Equal(1L, statusColumn);

            foreach (var table in new[] { "Users", "Documents", "HeadAlerts", "DocumentInterventions" })
            {
                var count = await db.Database
                    .SqlQueryRaw<long>(
                        """
                        SELECT COUNT(*)::bigint AS "Value"
                        FROM information_schema.tables
                        WHERE table_schema = 'public' AND table_name = {0}
                        """,
                        table)
                    .SingleAsync();
                Assert.Equal(1L, count);
            }

            var indexDefinition = await db.Database
                .SqlQueryRaw<string>(
                    "SELECT indexdef AS \"Value\" FROM pg_indexes WHERE indexname = 'UX_HeadAlerts_DedupeKey_Active'")
                .SingleAsync();
            Assert.Contains("DedupeKey", indexDefinition, StringComparison.Ordinal);
            Assert.Contains("IsArchived", indexDefinition, StringComparison.Ordinal);
            Assert.Contains("false", indexDefinition, StringComparison.OrdinalIgnoreCase);

            var constraintCount = await db.Database
                .SqlQueryRaw<long>(
                    """
                    SELECT COUNT(*)::bigint AS "Value"
                    FROM pg_constraint
                    WHERE conname IN ('CK_HeadAlerts_Kind_NotEmpty', 'CK_Documents_FileNumberRequiresCircuit')
                    """)
                .SingleAsync();
            Assert.Equal(2L, constraintCount);

            var triggerCount = await db.Database
                .SqlQueryRaw<long>(
                    """
                    SELECT COUNT(*)::bigint AS "Value"
                    FROM pg_trigger
                    WHERE tgname IN (
                        'trg_auditlogs_no_update', 'trg_auditlogs_no_delete',
                        'trg_documentfieldchanges_no_update', 'trg_documentfieldchanges_no_delete')
                    """)
                .SingleAsync();
            Assert.Equal(4L, triggerCount);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(baseConnectionString, databaseName);
        }
    }
}
