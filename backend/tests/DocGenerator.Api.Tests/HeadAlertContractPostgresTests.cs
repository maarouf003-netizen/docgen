using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace DocGenerator.Api.Tests;

/// <summary>
/// تغطية حيّة لفروع الرفض الثلاثة في <c>M3</c> على PostgreSQL — الفرع الذي لا يغطيه
/// أي اختبار محلي (لا قاعدة Postgres على أجهزة التطوير) ولا خطوة SQLite.
///
/// يُشغَّل فقط عند تعريف <c>DOCGEN_TEST_POSTGRES</c> (كاختبار Postgres القائم في
/// <see cref="DatabaseInitializerTests"/>). ينشئ قاعدة **معزولة خاصة به** بدل
/// استعمال قاعدة الاختبار المشتركة: اختبار Postgres الآخر ينفّذ
/// <c>EnsureDeleted</c> وقد يعمل بالتوازي (فئات xUnit تتوازى).
///
/// المسار المثبت: هجرة إلى <c>Backfill_Pg</c> ← إدخال صف بـ<c>Kind = NULL</c>
/// و<c>LastContentChangedAt = NULL</c> ← رفض <c>Contract_Pg</c> برسالة الحارس مع بقاء
/// المخطط/البيانات ← إصلاح تدريجي يثبت الفروع الثلاثة (بعد إصلاح <c>Kind</c> يبقى
/// رفض <c>LastContentChangedAt = NULL</c>، وبعد إصلاح الطابع يبقى رفض النوع الأبيض) ←
/// تطبيق ناجح ← قيد القاعدة يرفض الفارغ والـ<c>NULL</c> بعد التشديد.
/// </summary>
public sealed class HeadAlertContractPostgresTests
{
    /// <summary>آخر هجرة قبل التشديد (SQLite/Postgres لهما معرّفان مختلفان).</summary>
    private const string BackfillMigration = "20261021232825_HeadAlertTaxonomy_Backfill_Pg";

    /// <summary>هجرة التشديد (Postgres).</summary>
    private const string ContractMigration = "20261026152238_HeadAlertTaxonomy_Contract_Pg";

    [Fact]
    public async Task Contract_RejectsNullKind_ThenAppliesCleanly()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("DOCGEN_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
            return;

        var databaseName = "docgen_pr3_" + Guid.NewGuid().ToString("N")[..12];
        var connectionString = await CreateDatabaseAsync(baseConnectionString, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var db = new DocGeneratorPostgresDbContext(options);

            await db.Database.MigrateAsync(BackfillMigration);

            var branch = new Branch { Name = "دمشق", Code = "PR3-" + Guid.NewGuid().ToString("N")[..8] };
            db.Branches.Add(branch);
            await db.SaveChangesAsync();
            var user = new User
            {
                Username = "pr3-" + Guid.NewGuid().ToString("N")[..8],
                FullName = "محامي اختبار M3",
                Role = UserRole.Lawyer,
                BranchId = branch.Id,
                PasswordHash = "x",
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            // الصف المخالف يُدرج بـSQL خام: كيان PR-3 لا يمثّل Kind = NULL بعد التشديد.
            var alertId = await InsertLegacyAlertAsync(db, branch.Id, user.Id);

            var failure = await Assert.ThrowsAsync<PostgresException>(
                () => db.Database.MigrateAsync(ContractMigration));
            Assert.Equal("P0001", failure.SqlState);
            Assert.Contains("Kind = NULL", failure.MessageText, StringComparison.Ordinal);

            // الفشل قبل أي تغيير: العمودان ما زالا nullable، ولا قيد ولا سجل هجرة.
            Assert.Equal("YES", await ColumnNullabilityAsync(db, "Kind"));
            Assert.Equal("YES", await ColumnNullabilityAsync(db, "LastContentChangedAt"));
            Assert.False(await CheckConstraintExistsAsync(db));
            Assert.False(await MigrationAppliedAsync(db, ContractMigration));

            // لا تحويل صامت: القيمتان المخالفتان كما هما (الإدراج الخام لم يضبط أياً منهما).
            var storedKind = await db.Database
                .SqlQueryRaw<string?>("SELECT \"Kind\" AS \"Value\" FROM \"HeadAlerts\" WHERE \"Id\" = {0}", alertId)
                .SingleAsync();
            Assert.Null(storedKind);
            var nullTimestampRows = await db.Database
                .SqlQueryRaw<long>(
                    """
                    SELECT COUNT(*)::bigint AS "Value"
                    FROM "HeadAlerts"
                    WHERE "Id" = {0} AND "LastContentChangedAt" IS NULL
                    """,
                    alertId)
                .SingleAsync();
            Assert.Equal(1L, nullTimestampRows);

            // المرحلة 1: إصلاح Kind وحده — يبقى الرفض قائمًا على LastContentChangedAt = NULL،
            // فيثبت الاختبار فرع الحارس الثاني بعد الأول.
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"HeadAlerts\" SET \"Kind\" = 'legacy' WHERE \"Id\" = {0}", alertId);
            var timestampFailure = await Assert.ThrowsAsync<PostgresException>(
                () => db.Database.MigrateAsync(ContractMigration));
            Assert.Equal("P0001", timestampFailure.SqlState);
            Assert.Contains("LastContentChangedAt = NULL", timestampFailure.MessageText, StringComparison.Ordinal);
            Assert.False(await MigrationAppliedAsync(db, ContractMigration));

            // المرحلة 2: إصلاح الطابع (بنفس بديل PR-2 الدلالي: ?? CreatedAt) وإبقاء نوع
            // أبيض — يثبت فرع الحارس الثالث.
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"HeadAlerts\" SET \"LastContentChangedAt\" = \"CreatedAt\", \"Kind\" = '   '"
                + " WHERE \"Id\" = {0}",
                alertId);
            var emptyKindFailure = await Assert.ThrowsAsync<PostgresException>(
                () => db.Database.MigrateAsync(ContractMigration));
            Assert.Equal("P0001", emptyKindFailure.SqlState);
            Assert.Contains("فارغ أو أبيض", emptyKindFailure.MessageText, StringComparison.Ordinal);
            Assert.False(await MigrationAppliedAsync(db, ContractMigration));

            // المرحلة 3: النوع الصحيح والطابع مضبوط — يمرّ التشديد.
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"HeadAlerts\" SET \"Kind\" = 'legacy' WHERE \"Id\" = {0}", alertId);
            await db.Database.MigrateAsync(ContractMigration);

            Assert.Equal("NO", await ColumnNullabilityAsync(db, "Kind"));
            Assert.Equal("NO", await ColumnNullabilityAsync(db, "LastContentChangedAt"));
            Assert.True(await CheckConstraintExistsAsync(db));
            Assert.True(await MigrationAppliedAsync(db, ContractMigration));

            // القيد يرفض الفارغ، والعمود يرفض NULL (لا Default دائم).
            var emptyKind = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                "UPDATE \"HeadAlerts\" SET \"Kind\" = '' WHERE \"Id\" = {0}", alertId));
            Assert.Equal("23514", emptyKind.SqlState);

            var nullLastChanged = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                "UPDATE \"HeadAlerts\" SET \"LastContentChangedAt\" = NULL WHERE \"Id\" = {0}", alertId));
            Assert.Equal("23502", nullLastChanged.SqlState);

            var nullKind = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                "UPDATE \"HeadAlerts\" SET \"Kind\" = NULL WHERE \"Id\" = {0}", alertId));
            Assert.Equal("23502", nullKind.SqlState);
        }
        finally
        {
            await DropDatabaseAsync(baseConnectionString, databaseName);
        }
    }

    /// <summary>
    /// إدراج صف بالشكل القديم (بلا <c>Kind</c> ولا <c>LastContentChangedAt</c>) عبر SQL خام —
    /// كما في اختبار SQLite.
    /// </summary>
    private static async Task<int> InsertLegacyAlertAsync(
        DocGeneratorPostgresDbContext db, int branchId, int createdById)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "HeadAlerts"
                ("BranchId", "CreatedById", "TargetType", "Message", "CreatedAt", "IsArchived")
            VALUES ({0}, {1}, {3}, 'صف حارس M3 على PostgreSQL', {2}, false)
            """,
            branchId, createdById, DateTime.UtcNow, (int)HeadAlertTargetType.Branch);

        return await db.Database
            .SqlQueryRaw<int>("SELECT MAX(\"Id\") AS \"Value\" FROM \"HeadAlerts\"")
            .SingleAsync();
    }

    private static async Task<string?> ColumnNullabilityAsync(DocGeneratorPostgresDbContext db, string column)
    {
        return await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT is_nullable AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'HeadAlerts'
                  AND column_name = {0}
                """,
                column)
            .SingleAsync();
    }

    private static async Task<bool> CheckConstraintExistsAsync(DocGeneratorPostgresDbContext db)
    {
        var count = await db.Database
            .SqlQueryRaw<long>(
                """
                SELECT COUNT(*)::bigint AS "Value"
                FROM pg_constraint
                WHERE conname = 'CK_HeadAlerts_Kind_NotEmpty'
                """)
            .SingleAsync();
        return count > 0;
    }

    private static async Task<bool> MigrationAppliedAsync(
        DocGeneratorPostgresDbContext db, string migrationId)
    {
        var count = await db.Database
            .SqlQueryRaw<long>(
                "SELECT COUNT(*)::bigint AS \"Value\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = {0}",
                migrationId)
            .SingleAsync();
        return count > 0;
    }

    /// <summary>
    /// ينشئ قاعدة معزولة باسم عشوائي ويعيد سلسلة اتصالها. <c>Pooling=false</c> مقصود:
    /// إسقاط القاعدة في النهاية يتطلب ألا تبقى اتصالات خاملة مفتوحة، وبديله
    /// <c>NpgsqlConnection.ClearAllPools()</c> يمسّ مجمّعات **كل** الاختبارات
    /// المتوازية (ومنها <see cref="DatabaseInitializerTests"/>) — أثر عام غير مقبول.
    /// </summary>
    private static async Task<string> CreateDatabaseAsync(
        string baseConnectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString) { Pooling = false };
        var maintenance = new NpgsqlConnectionStringBuilder(baseConnectionString) { Database = "postgres" };
        await using (var connection = new NpgsqlConnection(maintenance.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await command.ExecuteNonQueryAsync();
        }

        builder.Database = databaseName;
        return builder.ConnectionString;
    }

    /// <summary>يُسقط القاعدة المعزولة (الاتصالات مغلقة فعليًا لأن التجميع معطّل).</summary>
    private static async Task DropDatabaseAsync(string baseConnectionString, string databaseName)
    {
        var maintenance = new NpgsqlConnectionStringBuilder(baseConnectionString) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(maintenance.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\"";
        await command.ExecuteNonQueryAsync();
    }
}
