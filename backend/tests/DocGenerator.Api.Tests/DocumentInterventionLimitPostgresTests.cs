using DocGenerator.Api.Tests.TestHelpers;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DocGenerator.Api.Tests;

/// <summary>
/// انحدار C-04: حد المتدخلين (50) لا يُتجاوز تحت التزامن.
///
/// <para>الاختبار حتمي لا يعتمد على مصادفة التوقيت: يمسك معاملة أولى قفل صف الملف
/// في <c>PostgreSQL</c>، ثم يُطلق طلب إضافة ثانٍ ويتأكد من انتظاره قفل الصف
/// (<c>pg_blocking_pids</c> يُظهر جلسة محجوبة)، ثم يُدخل المتدخل الخمسين في المعاملة الأولى
/// ويُثبّتها. عند تحرير القفل يقرأ الطلب الثاني العدّ الطازج (50) فيرفض. لو عاد العدّ إلى خارج
/// المعاملة أو بلا قفل لتجاوز الحدّ ولنجح الاختبار خطأً في رفضه.</para>
///
/// <para>مُقيَّد بـ<c>DOCGEN_TEST_POSTGRES</c> كسائر اختبارات <c>PostgreSQL</c>؛
/// وفي <c>CI</c> يُلزَم التنفيذ عبر <c>DOCGEN_REQUIRE_POSTGRES=1</c>.</para>
/// </summary>
public sealed class DocumentInterventionLimitPostgresTests
{
    [Fact]
    public async Task AddAsync_AtLimit_BlocksOnRowLock_AndRejectsFreshCount()
    {
        var baseConnectionString = PostgresTestEnvironment.RequireOrSkip();
        if (baseConnectionString is null)
            return;

        var databaseName = "docgen_int_limit_" + Guid.NewGuid().ToString("N")[..12];
        var connectionString = await PostgresTestDatabase.CreateAsync(baseConnectionString, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            int documentId;
            int userId;
            await using (var seed = new DocGeneratorPostgresDbContext(options))
            {
                await seed.Database.MigrateAsync();
                (documentId, userId) = await SeedAtLimitAsync(seed);
            }

            await using var holder = new DocGeneratorPostgresDbContext(options);
            await using var contender = new DocGeneratorPostgresDbContext(options);
            await using var observer = new DocGeneratorPostgresDbContext(options);
            var service = CreateService(contender);

            await using var holderTransaction = await holder.Database.BeginTransactionAsync();
            await using (var lockCommand = holder.Database.GetDbConnection().CreateCommand())
            {
                lockCommand.Transaction = holderTransaction.GetDbTransaction();
                lockCommand.CommandText = "SELECT 1 FROM \"Documents\" WHERE \"Id\" = @id FOR UPDATE";
                var parameter = lockCommand.CreateParameter();
                parameter.ParameterName = "id";
                parameter.Value = documentId;
                lockCommand.Parameters.Add(parameter);
                await lockCommand.ExecuteScalarAsync();
            }

            var addTask = service.AddAsync(
                documentId, NaturalRequest(), userId, "مستخدم اختبار حد المتدخلين");

            await WaitForBlockedRowLockAsync(observer, addTask);

            // المتدخل الخمسون يُكتب في المعاملة الماسكة للقفل: الطلب الثاني يجب أن يرى 50 بعد التحرير.
            await holder.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"DocumentInterventions\" (\"DocumentId\", \"Nature\", \"CreatedById\", \"CreatedAt\", \"UpdatedAt\") VALUES ({documentId}, {PartyNatureCatalog.Natural}, {userId}, {DateTime.UtcNow}, {DateTime.UtcNow})");
            await holderTransaction.CommitAsync();

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => addTask);
            Assert.Contains(InterventionLimitsCatalog.MaxPerDocument.ToString(), ex.Message);

            await using var verify = new DocGeneratorPostgresDbContext(options);
            Assert.Equal(
                InterventionLimitsCatalog.MaxPerDocument,
                await verify.DocumentInterventions.CountAsync(i => i.DocumentId == documentId));
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(baseConnectionString, databaseName);
        }
    }

    private static async Task<(int DocumentId, int UserId)> SeedAtLimitAsync(
        DocGeneratorPostgresDbContext db)
    {
        var branch = new Branch { Name = "فرع اختبار الحد", Code = "PGLIM" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var user = new User
        {
            Username = "pg_limit_user",
            FullName = "فاحص حد المتدخلين",
            Role = UserRole.Lawyer,
            BranchId = branch.Id,
            PasswordHash = "x",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var document = new Document
        {
            BranchId = branch.Id,
            CreatedById = user.Id,
            BorrowerName = "مدين اختبار",
            GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
            IsDraft = false,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        for (var i = 0; i < InterventionLimitsCatalog.MaxPerDocument - 1; i++)
        {
            db.DocumentInterventions.Add(new DocumentIntervention
            {
                DocumentId = document.Id,
                Nature = PartyNatureCatalog.Natural,
                Name = $"متدخل {i}",
                CreatedById = user.Id,
            });
        }
        await db.SaveChangesAsync();

        return (document.Id, user.Id);
    }

    /// <summary>
    /// ينتظر ظهور جلسة عميل محجوبة في قاعدة الاختبار الحالية — إثبات أن الطلب الثاني
    /// وصل إلى <c>FOR UPDATE</c> وعلق عليه، لا أنه لم يبدأ بعد. أقفال الصفوف لا تظهر
    /// عادةً في <c>pg_locks</c>؛ لذا يُستخدم <c>pg_blocking_pids</c>.
    /// </summary>
    private static async Task WaitForBlockedRowLockAsync(
        DocGeneratorPostgresDbContext observer,
        Task addTask)
    {
        var connection = observer.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (addTask.IsCompleted)
            {
                // إظهار الاستثناء الأصلي إن وُجد، أو الفشل لأن الطلب لم ينتظر القفل أصلًا.
                await addTask;
                Assert.Fail("اكتمل طلب الإضافة قبل تحرير قفل الصف — العدّ ليس داخل المعاملة/بلا قفل.");
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT count(*)
                FROM pg_stat_activity
                WHERE datname = current_database()
                  AND backend_type = 'client backend'
                  AND cardinality(pg_blocking_pids(pid)) > 0
                """;
            var waiting = Convert.ToInt32(await command.ExecuteScalarAsync());
            if (waiting > 0)
                return;

            await Task.Delay(50);
        }

        Assert.Fail("لم يصل طلب الإضافة إلى انتظار قفل صف الملف خلال المهلة.");
    }

    private static DocumentInterventionService CreateService(DocGeneratorPostgresDbContext db) =>
        new(
            new DocumentRepository(db),
            new Repository<PublicEntity>(db),
            new Repository<PublicEntityGroup>(db),
            new UnitOfWork(db),
            new TransactionRunner(db),
            new NoopAuditLogger());

    private static DocumentInterventionUpsertRequest NaturalRequest() => new()
    {
        Nature = PartyNatureCatalog.Natural,
        ReceiptNumber = "123",
        ReceiptDate = "1/8/2026",
        AcceptanceDate = "28/7/2026",
        Subject = "طلب تدخل اختبار الحد",
        Name = "متدخل",
        Father = "والد",
        Family = "عائلة",
        NationalId = "01020304",
        AddressType = "عنوان إقامة",
        Address = "دمشق",
    };

    private sealed class NoopAuditLogger : IAuditLogger
    {
        public Task LogAsync(string? userName, string actionType, int? documentId = null,
            string? documentType = null, string? details = null, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task LogManyAsync(IReadOnlyList<AuditLogEntry> entries, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task LogDocumentChangeAsync(string? userName, string actionType, int documentId,
            string? documentType, string details, IReadOnlyList<DocumentFieldChange> changes,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
