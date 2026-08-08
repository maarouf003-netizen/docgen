using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DocGenerator.Api.Tests;

public class DatabaseInitializerTests
{
    private static (string Path, DocGeneratorDbContext Db, IDatabaseInitializer Initializer) CreateInitializer()
    {
        var path = Path.Combine(Path.GetTempPath(), $"docgen_init_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<DocGeneratorDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        var db = new DocGeneratorDbContext(options);
        return (path, db, new DatabaseInitializer(db, new PasswordHasher()));
    }

    private static void Cleanup(string path)
    {
        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            try { File.Delete(path + suffix); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task InitializeAsync_Development_AppliesMigrationsAndSeedsBranchesAndUsers()
    {
        var (path, db, initializer) = CreateInitializer();
        try
        {
            await initializer.InitializeAsync(development: true, bootstrapAdminPassword: null);

            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
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
    public async Task InitializeAsync_Production_WhenUsersExist_SkipsSeeding()
    {
        var (path, db, initializer) = CreateInitializer();
        try
        {
            await db.Database.MigrateAsync();
            db.Users.Add(new User
            {
                Username = "existing",
                FullName = "مستخدم موجود",
                Role = UserRole.Lawyer,
                PasswordHash = new PasswordHasher().Hash("123456"),
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

    [Fact]
    public async Task RequiredDocumentOwnerMigration_BackfillsOrphanedDocuments()
    {
        var (path, db, initializer) = CreateInitializer();
        try
        {
            // قاعدة تُهاجَر حتى المهاجرة السابقة (قبل فرض المحامي الإلزامي).
            // EF Core 8: الترحيل إلى هدف محدد عبر IMigrator (Migrate/MigrateAsync لا يقبلان targetMigration).
            await db.GetService<IMigrator>().MigrateAsync("20260804075839_AddDocumentSoftDelete");

            db.Users.Add(new User
            {
                Username = "admin",
                FullName = "المشرف العام",
                Role = UserRole.Admin,
                PasswordHash = new PasswordHasher().Hash("123456"),
            });
            await db.SaveChangesAsync();
            // إدراج المستند بـ SQL خام بالأعمدة الموجودة في القاعدة القديمة فقط،
            // لأن نموذج EF الحالي يحمل أعمدة وضع «منفذ عليه» غير الموجودة في هذا السكيمة.
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Documents\" (\"CreatedAt\", \"UpdatedAt\", \"CreatedById\", \"IsDraft\", \"DocumentType\", \"AmountNumeric\", \"Amount2Numeric\", \"InclusionAmountNumeric\", \"ViewCount\", \"PrintCount\") VALUES ({0}, {0}, {1}, {2}, {3}, 0, 0, 0, 0, 0)",
                DateTime.UtcNow, 1, true, "بيان");
            var docId = await db.Documents.Select(d => d.Id).MaxAsync();

            // محاكاة ملف يتيم (بلا محامٍ مختص) موجود في القاعدة القديمة.
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"Documents\" SET \"CreatedById\" = NULL WHERE \"Id\" = {0}", docId);

            // تطبيق مهاجرة AddRequiredDocumentOwner: يجب أن تُسند اليتيم لأدنى مستخدم ثم تفرض NOT NULL.
            await db.Database.MigrateAsync();

            var migrated = await db.Documents.SingleAsync();
            Assert.Equal(1, migrated.CreatedById);
            Assert.False(migrated.CreatedBy is null);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    [Fact]
    public async Task AddRealEstateOwnersMigration_MovesExistingOwnersIntoList()
    {
        var (path, db, initializer) = CreateInitializer();
        try
        {
            // قاعدة تُهاجَر حتى المهاجرة السابقة (قبل قائمة الملاك) ثم يُحقن عقار
            // بقيمة المالك المفرد كما كانت القاعدة القديمة.
            await db.GetService<IMigrator>().MigrateAsync("20260807221412_AddHeirs");

            db.Users.Add(new User
            {
                Username = "admin",
                FullName = "المشرف العام",
                Role = UserRole.Admin,
                PasswordHash = new PasswordHasher().Hash("123456"),
            });
            await db.SaveChangesAsync();
            // إدراج المستند بـ SQL خام بالأعمدة الموجودة في القاعدة القديمة فقط،
            // لأن نموذج EF الحالي يحمل أعمدة وضع «منفذ عليه» غير الموجودة في هذا السكيمة.
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Documents\" (\"CreatedAt\", \"UpdatedAt\", \"CreatedById\", \"IsDraft\", \"DocumentType\", \"AmountNumeric\", \"Amount2Numeric\", \"InclusionAmountNumeric\", \"ViewCount\", \"PrintCount\") VALUES ({0}, {0}, {1}, {2}, {3}, 0, 0, 0, 0, 0)",
                DateTime.UtcNow, 1, true, "بيان");
            var docId = await db.Documents.Select(d => d.Id).MaxAsync();
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"RealEstates\" (\"DocumentId\", \"Owner\", \"Property\", \"PropertyNumber\", \"PropertyDistrict\", \"LandRegistry\", \"ShareType\") VALUES ({0}, {1}, {2}, NULL, NULL, NULL, NULL)",
                docId, "أحمد محمد خالد", "منزل");

            // تطبيق مهاجرة AddRealEstateOwners: يجب ترحيل قيمة Owner إلى جدول الملاك.
            await db.Database.MigrateAsync();

            var migrated = await db.RealEstates.Include(r => r.Owners).SingleAsync();
            var owner = Assert.Single(migrated.Owners);
            Assert.Equal("أحمد محمد خالد", owner.Name);
            Assert.Equal(0, owner.Order);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }
}
