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
/// تهيئة قاعدة التطوير/الإنتاج بعد P0-02: لا هجرات SQLite، والمخطط يُبنى من النموذج
/// في الاختبارات (<c>EnsureCreated</c>) بينما الإنتاج يستخدم Baseline PostgreSQL واحدًا.
/// اختبارات الهجرات التاريخية أُزيلت مع الهجرات نفسها؛ ومسار Postgres الحقيقي مغطّى
/// في <see cref="PostgresBaselineTests"/> عند تعريف <c>DOCGEN_TEST_POSTGRES</c>.
/// </summary>
public class DatabaseInitializerTests
{
    private static (string Path, DocGeneratorDbContext Db, IDatabaseInitializer Initializer) CreateInitializer(
        bool autoMigrate = false)
    {
        var path = Path.Combine(Path.GetTempPath(), $"docgen_init_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<DocGeneratorDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        var db = new DocGeneratorDbContext(options);
        db.Database.EnsureCreated();
        var databaseOptions = Options.Create(new DatabaseOptions { AutoMigrate = autoMigrate });
        return (path, db, new DatabaseInitializer(db, new FastTestPasswordHasher(), databaseOptions));
    }

    private static void Cleanup(string path)
    {
        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            try { File.Delete(path + suffix); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task InitializeAsync_Development_SeedsBranchesAndUsers()
    {
        var (path, db, initializer) = CreateInitializer();
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
            Cleanup(path);
        }
    }

    [Fact]
    public async Task InitializeAsync_AutoMigrateDisabled_WithUpToDateSchema_Proceeds()
    {
        var (path, db, initializer) = CreateInitializer(autoMigrate: false);
        try
        {
            await initializer.InitializeAsync(development: false, bootstrapAdminPassword: "Strong!Pass123");

            Assert.Equal(1, await db.Users.CountAsync());
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    [Fact]
    public async Task InitializeAsync_Production_WithAdminPassword_CreatesOnlyAdmin()
    {
        var (path, db, initializer) = CreateInitializer();
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
            Cleanup(path);
        }
    }

    [Fact]
    public async Task InitializeAsync_Production_WithoutAdminPassword_Throws()
    {
        var (path, db, initializer) = CreateInitializer();
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => initializer.InitializeAsync(development: false, bootstrapAdminPassword: null));
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    [Fact]
    public async Task InitializeAsync_Production_RejectsNumberedDocumentWithoutCircuit_R9()
    {
        var (path, db, initializer) = CreateInitializer(autoMigrate: false);
        try
        {
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
            Cleanup(path);
        }
    }

    [Fact]
    public async Task InitializeAsync_Production_WhenUsersExist_SkipsSeeding()
    {
        var (path, db, initializer) = CreateInitializer();
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
            Cleanup(path);
        }
    }
}
