using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Api.Tests.TestServices;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests;

/// <summary>
/// تهيئة قاعدة التطوير/الإنتاج على <c>PostgreSQL</c> فقط (‏`ADR-0001`): المخطط يُبنى
/// بهجرات EF الحقيقية، والإنتاج لا يرحّل تلقائيًا، والبذر يتبع البيئة. لا مسار
/// <c>EnsureCreated</c>/<c>SQLite</c> بعد `P1-08`؛ ومسار Baseline مغطّى أيضًا في
/// <see cref="PostgresBaselineTests"/>.
/// </summary>
public class DatabaseInitializerTests
{
    /// <summary>
    /// قاعدة معزولة لكل حالة: مهاجَرة مسبقًا حين يكون `autoMigrate=false` (كي يقيس
    /// الاختبار ما بعد الترحيل)، وفارغة حين يريد الاختبار أن يرحّل المهيّئ بنفسه.
    /// </summary>
    private static (PostgresHostDatabase Host, DocGeneratorPostgresDbContext Db, IDatabaseInitializer Initializer)
        CreateInitializer(bool autoMigrate = false)
    {
        var host = PostgresHostDatabase.Create(migrate: !autoMigrate);
        var options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
            .UseNpgsql(host.ConnectionString, npgsql => npgsql.CommandTimeout(180))
            .Options;
        var db = new DocGeneratorPostgresDbContext(options);
        var databaseOptions = Options.Create(new DatabaseOptions { AutoMigrate = autoMigrate });
        return (host, db, new DatabaseInitializer(db, new FastTestPasswordHasher(), databaseOptions));
    }

    [PostgresFact]
    public async Task InitializeAsync_Development_SeedsBranchesAndUsers()
    {
        var (host, db, initializer) = CreateInitializer();
        try
        {
            await initializer.InitializeAsync(development: true, bootstrapAdminPassword: null);

            Assert.True(await db.Branches.AnyAsync());
            Assert.True(await db.Users.CountAsync() >= 4);
            Assert.NotNull(await db.Users.FirstOrDefaultAsync(u => u.Username == "lawyer1"));
            Assert.NotNull(await db.Users.FirstOrDefaultAsync(u => u.Username == "admin"));
        }
        finally
        {
            await db.DisposeAsync();
            host.Dispose();
        }
    }

    [PostgresFact]
    public async Task InitializeAsync_AutoMigrateEnabled_OnEmptyDatabase_MigratesAndProceeds()
    {
        var (host, db, initializer) = CreateInitializer(autoMigrate: true);
        try
        {
            await initializer.InitializeAsync(development: false, bootstrapAdminPassword: "Strong!Pass123");

            Assert.Equal(1, await db.Users.CountAsync());
            Assert.Contains(
                db.Database.GetAppliedMigrations(),
                migration => migration.EndsWith("InitialPostgresBaseline", StringComparison.Ordinal));
        }
        finally
        {
            await db.DisposeAsync();
            host.Dispose();
        }
    }

    [PostgresFact]
    public async Task InitializeAsync_AutoMigrateDisabled_WithUpToDateSchema_Proceeds()
    {
        var (host, db, initializer) = CreateInitializer(autoMigrate: false);
        try
        {
            await initializer.InitializeAsync(development: false, bootstrapAdminPassword: "Strong!Pass123");

            Assert.Equal(1, await db.Users.CountAsync());
        }
        finally
        {
            await db.DisposeAsync();
            host.Dispose();
        }
    }

    [PostgresFact]
    public async Task InitializeAsync_Production_WithAdminPassword_CreatesOnlyAdmin()
    {
        var (host, db, initializer) = CreateInitializer();
        try
        {
            await initializer.InitializeAsync(development: false, bootstrapAdminPassword: "Strong!Pass123");

            Assert.Equal(1, await db.Users.CountAsync());
            var admin = await db.Users.SingleAsync();
            Assert.Equal("admin", admin.Username);
        }
        finally
        {
            await db.DisposeAsync();
            host.Dispose();
        }
    }

    [PostgresFact]
    public async Task InitializeAsync_Production_WithoutAdminPassword_Throws()
    {
        var (host, db, initializer) = CreateInitializer();
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => initializer.InitializeAsync(development: false, bootstrapAdminPassword: null));
        }
        finally
        {
            await db.DisposeAsync();
            host.Dispose();
        }
    }

    /// <summary>
    /// حارس R9 دفاعٌ عن قواعد **قديمة** سبقت قيد القاعدة: نُسقط القيد في قاعدة الاختبار
    /// (بعد الهجرة) لنصنع الحالة التاريخية، ثم يتصدى الحارس لها بالإقلاع. في المخطط
    /// الحديث يمنع القيد كتابة الصف أصلًا — ولهذا يلزم إسقاطه صراحةً هنا.
    /// </summary>
    [PostgresFact]
    public async Task InitializeAsync_Production_RejectsNumberedDocumentWithoutCircuit_R9()
    {
        var (host, db, initializer) = CreateInitializer(autoMigrate: false);
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Documents\" DROP CONSTRAINT \"CK_Documents_FileNumberRequiresCircuit\"");

            var branch = new Branch { Name = "دمشق", Code = "DAM" };
            db.Branches.Add(branch);
            await db.SaveChangesAsync();
            var user = new User
            {
                Username = "lawyer_r9",
                FullName = "محامي",
                Role = UserRole.Lawyer,
                BranchId = branch.Id,
                PasswordHash = new FastTestPasswordHasher().Hash("123456"),
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.Documents.Add(new Document
            {
                CreatedById = user.Id,
                BranchId = branch.Id,
                FileNumber = "1",
                FileYear = "2026",
                FileType = "صلح",
                IsDraft = false,
                NeedsRegistration = false,
            });
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => initializer.InitializeAsync(development: false, bootstrapAdminPassword: "Strong!Pass123"));
            Assert.Contains("بلا دائرة", ex.Message);
        }
        finally
        {
            await db.DisposeAsync();
            host.Dispose();
        }
    }

    [PostgresFact]
    public async Task InitializeAsync_Production_WhenUsersExist_SkipsSeeding()
    {
        var (host, db, initializer) = CreateInitializer();
        try
        {
            db.Users.Add(new User
            {
                Username = "existing",
                FullName = "المشرف العام",
                // مشرف بلا فرع بالتصميم (قيد القاعدة يمنع المحامي/الرئيس بلا فرع) —
                // نية الاختبار «مستخدم موجود» لا تتأثر بالدور.
                Role = UserRole.Admin,
                PasswordHash = new FastTestPasswordHasher().Hash("123456"),
            });
            await db.SaveChangesAsync();

            await initializer.InitializeAsync(development: false, bootstrapAdminPassword: null);

            Assert.Equal(1, await db.Users.CountAsync());
            Assert.Equal("existing", (await db.Users.SingleAsync()).Username);
        }
        finally
        {
            await db.DisposeAsync();
            host.Dispose();
        }
    }
}
