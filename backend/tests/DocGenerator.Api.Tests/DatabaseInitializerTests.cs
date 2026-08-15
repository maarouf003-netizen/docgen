using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL;

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
    public async Task AddDocumentOccurrencesMigration_BackfillsExistingStruckOffAndRenewalData()
    {
        var (path, db, initializer) = CreateInitializer();
        try
        {
            // قاعدة تُهاجَر حتى المهاجرة السابقة (قبل جدول الوقوعات) ثم تُحقن ملفات
            // وضع «منفذ عليه» بتاريخ شطب و/أو بيان تجديد كما كانت القاعدة القديمة.
            await db.GetService<IMigrator>().MigrateAsync("20260813174418_AddDocumentRenewal");

            db.Users.Add(new User
            {
                Username = "admin",
                FullName = "المشرف العام",
                Role = UserRole.Admin,
                PasswordHash = new PasswordHasher().Hash("123456"),
            });
            await db.SaveChangesAsync();

            // ملف مشطوب فقط.
            var struckId = InsertLegacyExecutedDocumentAsync(
                db, FileNumber: "700", StruckOffDate: new DateTime(2026, 8, 1), RenewalFileNumber: null);

            // ملف مجدَّد فقط.
            var renewedId = InsertLegacyExecutedDocumentAsync(
                db, FileNumber: "800", StruckOffDate: null,
                RenewalFileNumber: "2026/55", RenewalFileType: "قضية تنفيذ",
                RenewalFileReceiptNumber: "45", RenewalFileReceiptDate: new DateTime(2026, 9, 2),
                RenewalDate: new DateTime(2026, 9, 5));

            // ملف مشطوب ثم مجدَّد: يُرحَّل وقعة شطب ووقعة تجديد معًا.
            var bothId = InsertLegacyExecutedDocumentAsync(
                db, FileNumber: "900", StruckOffDate: new DateTime(2026, 8, 1),
                RenewalFileNumber: "2026/99");

            // تطبيق مهاجرة AddDocumentOccurrences: يجب أن تُنشئ الوقوعات من البيانات القديمة.
            await db.Database.MigrateAsync();

            var struck = await db.DocumentOccurrences
                .IgnoreQueryFilters()
                .SingleAsync(o => o.DocumentId == struckId);
            Assert.Equal(OccurrenceTypeCatalog.StruckOff, struck.OccurrenceType);
            Assert.Equal(new DateTime(2026, 8, 1), struck.EventDate);
            Assert.Equal("700", struck.FileNumber);
            Assert.Equal(2026, struck.Year);

            var renewed = await db.DocumentOccurrences
                .IgnoreQueryFilters()
                .SingleAsync(o => o.DocumentId == renewedId);
            Assert.Equal(OccurrenceTypeCatalog.Renewal, renewed.OccurrenceType);
            Assert.Equal("2026/55", renewed.FileNumber);
            Assert.Equal("قضية تنفيذ", renewed.FileType);
            Assert.Equal(2026, renewed.Year);
            Assert.Equal("45", renewed.ReceiptNumber);
            Assert.Equal(new DateTime(2026, 9, 2), renewed.ReceiptDate);
            Assert.Equal(new DateTime(2026, 9, 5), renewed.EventDate);

            var both = await db.DocumentOccurrences
                .IgnoreQueryFilters()
                .Where(o => o.DocumentId == bothId)
                .OrderBy(o => o.OccurrenceType)
                .ToListAsync();
            Assert.Equal(2, both.Count);
            Assert.Contains(both, o => o.OccurrenceType == OccurrenceTypeCatalog.StruckOff);
            Assert.Contains(both, o => o.OccurrenceType == OccurrenceTypeCatalog.Renewal);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    [Fact]
    public async Task AddApplicantEntitiesMigration_BackfillsLegacyApplicantString()
    {
        var (path, db, initializer) = CreateInitializer();
        try
        {
            // قاعدة تُهاجَر حتى المهاجرة السابقة (قبل قائمة الجهات طالبة التنفيذ).
            await db.GetService<IMigrator>().MigrateAsync("20260814093700_AddPartyNature");

            db.Users.Add(new User
            {
                Username = "admin",
                FullName = "المشرف العام",
                Role = UserRole.Admin,
                PasswordHash = new PasswordHasher().Hash("123456"),
            });
            await db.SaveChangesAsync();
            // إدراج مستند قديم يحمل «طالب التنفيذ» نصيًا (Applicant) كما كان قبل الهجرة.
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Documents\" (\"CreatedAt\", \"UpdatedAt\", \"CreatedById\", \"IsDraft\", \"DocumentType\", \"Applicant\", \"AmountNumeric\", \"Amount2Numeric\", \"InclusionAmountNumeric\", \"ViewCount\", \"PrintCount\") VALUES ({0}, {0}, {1}, {2}, {3}, {4}, 0, 0, 0, 0, 0)",
                DateTime.UtcNow, 1, true, "بيان", "المصرف التجاري السوري");
            var docId = await db.Documents.Select(d => d.Id).MaxAsync();

            // تطبيق هجرة قائمة الجهات طالبة التنفيذ: يجب أن تُرحَّل النصوص إلى القائمة.
            await db.Database.MigrateAsync();

            var entity = await db.ApplicantPublicEntities.SingleAsync(e => e.DocumentId == docId);
            Assert.Equal("المصرف التجاري السوري", entity.Name);
            Assert.Null(entity.Branch);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    /// <summary>
    /// إدراج ملف وضع «منفذ عليه» قديم بـ SQL خام بالأعمدة الموجودة قبل هجرة الوقوعات.
    /// </summary>
    private static int InsertLegacyExecutedDocumentAsync(
        DocGeneratorDbContext db,
        string FileNumber,
        DateTime? StruckOffDate,
        string? RenewalFileNumber,
        string? RenewalFileType = null,
        string? RenewalFileReceiptNumber = null,
        DateTime? RenewalFileReceiptDate = null,
        DateTime? RenewalDate = null)
    {
        db.Database.ExecuteSqlRaw(
            "INSERT INTO \"Documents\" (\"CreatedAt\", \"UpdatedAt\", \"CreatedById\", \"IsDraft\", \"DocumentType\", \"GeneralEntitySide\", \"ExecutedStatus\", \"StruckOffDate\", \"FileNumber\", \"RenewalFileNumber\", \"RenewalFileType\", \"RenewalFileReceiptNumber\", \"RenewalFileReceiptDate\", \"RenewalDate\", \"AmountNumeric\", \"Amount2Numeric\", \"Amount3Numeric\", \"InclusionAmountNumeric\", \"InclusionAmount2Numeric\", \"InclusionAmount3Numeric\", \"ViewCount\", \"PrintCount\") VALUES ({0}, {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, 0, 0, 0, 0, 0, 0, 0, 0)",
            DateTime.UtcNow, 1, false, "الجهة العامة منفذ عليها", GeneralEntitySideCatalog.Executed,
            ExecutedStatusCatalog.StruckOff, StruckOffDate, FileNumber,
            RenewalFileNumber, RenewalFileType, RenewalFileReceiptNumber,
            RenewalFileReceiptDate, RenewalDate);
        return db.Documents.Select(d => d.Id).Max();
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

    private static async Task<int> InsertLegacyDocumentAsync(DocGeneratorDbContext db)
    {
        // إدراج المستند بـ SQL خام بالأعمدة الموجودة في القاعدة القديمة فقط،
        // لأن نموذج EF الحالي يحمل أعمدة وضع «منفذ عليه» غير الموجودة في هذا السكيمة.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"Documents\" (\"CreatedAt\", \"UpdatedAt\", \"CreatedById\", \"IsDraft\", \"DocumentType\", \"AmountNumeric\", \"Amount2Numeric\", \"InclusionAmountNumeric\", \"ViewCount\", \"PrintCount\") VALUES ({0}, {0}, {1}, {2}, {3}, 0, 0, 0, 0, 0)",
            DateTime.UtcNow, 1, true, "بيان");
        return await db.Documents.Select(d => d.Id).MaxAsync();
    }

    [Fact]
    public async Task AddRegistrationDateParsedMigration_BackfillsParsedDatesFromText()
    {
        var (path, db, initializer) = CreateInitializer();
        try
        {
            // قاعدة تُهاجَر حتى المهاجرة السابقة (قبل عمود DateParsed) ثم يُحقن مستند
            // بتواريخ قيد قديمة بصيغ النص الحر كما كانت القاعدة قبل البند الرابع.
            await db.GetService<IMigrator>().MigrateAsync("20260809073849_MakeUsernameUniqueForBranchlessUsers");

            db.Users.Add(new User
            {
                Username = "admin",
                FullName = "المشرف العام",
                Role = UserRole.Admin,
                PasswordHash = new PasswordHasher().Hash("123456"),
            });
            await db.SaveChangesAsync();

            var samples = new (string? Date, DateTime? Expected)[]
            {
                ("1/8/2026", new DateTime(2026, 8, 1)),
                ("01/08/2026", new DateTime(2026, 8, 1)),
                ("15-3-2026", new DateTime(2026, 3, 15)),
                ("2026-12-31", new DateTime(2026, 12, 31)),
                ("1/8/26", new DateTime(2026, 8, 1)),
                ("1/8/99", new DateTime(1999, 8, 1)),
                ("5/8/49", new DateTime(2049, 8, 5)),
                ("29/2/2024", new DateTime(2024, 2, 29)), // سنة كبيسة
                ("31/02/2026", null),                     // يوم غير موجود في الشهر
                ("غير صالح", null),
                (null, null),
            };
            // الجدول أساسه المستند (DocumentId مفتاح)، فيُدرج مستند لكل حالة.
            foreach (var (text, _) in samples)
            {
                var docId = await InsertLegacyDocumentAsync(db);
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO \"DocumentRegistrationDates\" (\"DocumentId\", \"Date\") VALUES ({0}, {1})",
                    docId, (object)text!);
            }

            // تطبيق المهاجرة: يجب ملء DateParsed من النص وفق صيغ ActionDateParser.
            await db.Database.MigrateAsync();

            var rows = await db.DocumentRegistrationDates.AsNoTracking()
                .Select(r => new { r.Date, r.DateParsed })
                .ToListAsync();

            Assert.Equal(samples.Length, rows.Count);
            foreach (var (text, expected) in samples)
            {
                var row = Assert.Single(rows, r => r.Date == text);
                Assert.Equal(expected, row.DateParsed);
            }
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    [Fact]
    public async Task AddRegistrationDateParsedMigration_Down_DropsParsedColumn()
    {
        var (path, db, initializer) = CreateInitializer();
        try
        {
            await db.Database.MigrateAsync();
            Assert.True(await HasDocumentRegistrationDateColumnAsync(db, "DateParsed"));

            // التراجع إلى ما قبل المهاجرة: يجب أن يسقط العمود (وطبقة SQLite لا تحتفظ به).
            await db.GetService<IMigrator>().MigrateAsync("20260809073849_MakeUsernameUniqueForBranchlessUsers");

            Assert.False(await HasDocumentRegistrationDateColumnAsync(db, "DateParsed"));
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    private static async Task<bool> HasDocumentRegistrationDateColumnAsync(DocGeneratorDbContext db, string column)
    {
        var columns = await db.Database
            .SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info('DocumentRegistrationDates')")
            .ToListAsync();
        return columns.Contains(column);
    }

    [Fact]
    public async Task AddRegistrationDateParsedMigration_Postgres_BackfillsWhenServerConfigured()
    {
        // اختبار تكامل يتطلب خادم Postgres حقيقيًا؛ يُشغَّل فقط عند تعريف المتغير:
        //   DOCGEN_TEST_POSTGRES="Host=...;Port=5432;Database=...;Username=...;Password=..."
        // يطابق سلوك اختبار SQLite أعلاه (الحقن المسبق + الهجرة + التحقق من DateParsed).
        var connectionString = Environment.GetEnvironmentVariable("DOCGEN_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var db = new DocGeneratorPostgresDbContext(options);
        try
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();

            db.Users.Add(new User
            {
                Username = "admin",
                FullName = "المشرف العام",
                Role = UserRole.Admin,
                PasswordHash = new PasswordHasher().Hash("123456"),
            });
            await db.SaveChangesAsync();

            var samples = new (string? Date, DateTime? Expected)[]
            {
                ("1/8/2026", new DateTime(2026, 8, 1)),
                ("01/08/2026", new DateTime(2026, 8, 1)),
                ("15-3-2026", new DateTime(2026, 3, 15)),
                ("2026-12-31", new DateTime(2026, 12, 31)),
                ("1/8/26", new DateTime(2026, 8, 1)),
                ("1/8/99", new DateTime(1999, 8, 1)),
                ("29/2/2024", new DateTime(2024, 2, 29)),
                ("31/02/2026", null),
                ("غير صالح", null),
                (null, null),
            };
            foreach (var (text, _) in samples)
            {
                var docId = await InsertLegacyDocumentAsync(db);
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO \"DocumentRegistrationDates\" (\"DocumentId\", \"Date\") VALUES ({0}, {1})",
                    docId, (object)text!);
            }

            await db.Database.MigrateAsync();

            var rows = await db.DocumentRegistrationDates.AsNoTracking()
                .Select(r => new { r.Date, r.DateParsed })
                .ToListAsync();

            Assert.Equal(samples.Length, rows.Count);
            foreach (var (text, expected) in samples)
            {
                var row = Assert.Single(rows, r => r.Date == text);
                Assert.Equal(expected, row.DateParsed);
            }
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
