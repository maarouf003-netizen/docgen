using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Api.Tests.TestServices;
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
        return (path, db, new DatabaseInitializer(db, new FastTestPasswordHasher()));
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
                FullName = "المشرف العام",
                Role = UserRole.Lawyer,
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

    [Fact]
    public async Task RequiredDocumentOwnerMigration_BackfillsOrphanedDocuments()
    {
        var (path, db, initializer) = CreateInitializer();
        try
        {
            // قاعدة تُهاجَر حتى المهاجرة السابقة (قبل فرض المحامي الإلزامي).
            // EF Core 8: الترحيل إلى هدف محدد عبر IMigrator (Migrate/MigrateAsync لا يقبلان targetMigration).
            await db.GetService<IMigrator>().MigrateAsync("20260804075839_AddDocumentSoftDelete");

            // إدراج المستخدم بـ SQL خام بالأعمدة الموجودة في القاعدة القديمة فقط،
            // لأن نموذج EF الحالي يحمل أعمدة بوابة المندوب غير الموجودة في هذه السكيمة.
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Users\" (\"Username\", \"FullName\", \"Role\", \"PasswordHash\", \"FailedLoginCount\", \"IsActive\", \"TokenVersion\", \"CreatedAt\", \"UpdatedAt\") VALUES ({0}, {1}, {2}, {3}, 0, 1, 0, {4}, {4})",
                "admin", "المشرف العام", "admin", new FastTestPasswordHasher().Hash("123456"), DateTime.UtcNow);
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

            var migrated = await db.Documents.Include(d => d.CreatedBy).SingleAsync();
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

            // إدراج المستخدم بـ SQL خام بالأعمدة الموجودة في القاعدة القديمة فقط،
            // لأن نموذج EF الحالي يحمل أعمدة بوابة المندوب غير الموجودة في هذه السكيمة.
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Users\" (\"Username\", \"FullName\", \"Role\", \"PasswordHash\", \"FailedLoginCount\", \"IsActive\", \"TokenVersion\", \"CreatedAt\", \"UpdatedAt\") VALUES ({0}, {1}, {2}, {3}, 0, 1, 0, {4}, {4})",
                "admin", "المشرف العام", "admin", new FastTestPasswordHasher().Hash("123456"), DateTime.UtcNow);

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

            // إدراج المستخدم بـ SQL خام بالأعمدة الموجودة في القاعدة القديمة فقط،
            // لأن نموذج EF الحالي يحمل أعمدة بوابة المندوب غير الموجودة في هذه السكيمة.
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Users\" (\"Username\", \"FullName\", \"Role\", \"PasswordHash\", \"FailedLoginCount\", \"IsActive\", \"TokenVersion\", \"CreatedAt\", \"UpdatedAt\") VALUES ({0}, {1}, {2}, {3}, 0, 1, 0, {4}, {4})",
                "admin", "المشرف العام", "admin", new FastTestPasswordHasher().Hash("123456"), DateTime.UtcNow);
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

            // إدراج المستخدم بـ SQL خام بالأعمدة الموجودة في القاعدة القديمة فقط،
            // لأن نموذج EF الحالي يحمل أعمدة بوابة المندوب غير الموجودة في هذه السكيمة.
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Users\" (\"Username\", \"FullName\", \"Role\", \"PasswordHash\", \"FailedLoginCount\", \"IsActive\", \"TokenVersion\", \"CreatedAt\", \"UpdatedAt\") VALUES ({0}, {1}, {2}, {3}, 0, 1, 0, {4}, {4})",
                "admin", "المشرف العام", "admin", new FastTestPasswordHasher().Hash("123456"), DateTime.UtcNow);
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

            var migrated = await db.Assets.Include(a => a.Owners).SingleAsync();
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

            // إدراج المستخدم بـ SQL خام بالأعمدة الموجودة في القاعدة القديمة فقط،
            // لأن نموذج EF الحالي يحمل أعمدة بوابة المندوب غير الموجودة في هذه السكيمة.
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Users\" (\"Username\", \"FullName\", \"Role\", \"PasswordHash\", \"FailedLoginCount\", \"IsActive\", \"TokenVersion\", \"CreatedAt\", \"UpdatedAt\") VALUES ({0}, {1}, {2}, {3}, 0, 1, 0, {4}, {4})",
                "admin", "المشرف العام", "admin", new FastTestPasswordHasher().Hash("123456"), DateTime.UtcNow);

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

            // إدراج المستخدم بـ SQL خام بالأعمدة الموجودة في القاعدة القديمة فقط،
            // لأن نموذج EF الحالي يحمل أعمدة بوابة المندوب غير الموجودة في هذه السكيمة.
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Users\" (\"Username\", \"FullName\", \"Role\", \"PasswordHash\", \"FailedLoginCount\", \"IsActive\", \"TokenVersion\", \"CreatedAt\", \"UpdatedAt\") VALUES ({0}, {1}, {2}, {3}, 0, 1, 0, {4}, {4})",
                "admin", "المشرف العام", "admin", new FastTestPasswordHasher().Hash("123456"), DateTime.UtcNow);

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

    [Fact]
    public async Task AddDelegationsMigration_CreatesTables_AndPersistsDelegationGraph()
    {
        var (path, db, initializer) = CreateInitializer();
        try
        {
            await initializer.InitializeAsync(development: true, bootstrapAdminPassword: null);

            // الجداول الجديدة موجودة بعد الترحيل حتى آخر مهاجرة.
            Assert.True(await TableExistsAsync(db, "DocumentDelegations"));
            Assert.True(await TableExistsAsync(db, "DelegationAssets"));

            var branch = await db.Branches.FirstAsync();
            var sourceLawyer = await db.Users.FirstAsync(u => u.Username == "lawyer1");
            var targetLawyer = await db.Users.FirstAsync(u => u.Username == "head1");

            var source = new Document
            {
                CreatedById = sourceLawyer.Id,
                BranchId = branch.Id,
                BorrowerName = "المنيب",
                GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
                DocumentType = "متداول - المنيب",
            };
            db.Documents.Add(source);
            await db.SaveChangesAsync();

            var asset = new Asset
            {
                DocumentId = source.Id,
                AssetKind = AssetKindCatalog.RealEstate,
                PropertyNumber = "77",
                PropertyDistrict = "المزة",
            };
            db.Assets.Add(asset);
            await db.SaveChangesAsync();

            var delegation = new DocumentDelegation
            {
                SourceDocumentId = source.Id,
                DelegatedCourt = "دائرة تنفيذ دمشق",
                IsExternal = true,
                ExternalBranchId = branch.Id,
                DelegationDate = new DateTime(2026, 8, 1),
                DelegationText = "الإنابة على عقار رقم 77",
                DepositBookNumber = "كتاب-1",
                DepositBookDate = new DateTime(2026, 8, 2),
                Status = DelegationStatusCatalog.Assigned,
                AssignedLawyerId = targetLawyer.Id,
                CreatedById = sourceLawyer.Id,
                Assets =
                {
                    new DelegationAsset { AssetKind = AssetKindCatalog.RealEstate, AssetLabel = "عقار رقم 77 — المزة", SalePrice = 500_000m },
                },
            };
            db.DocumentDelegations.Add(delegation);
            await db.SaveChangesAsync();

            // الملف المناب يرتبط بإنابته عبر SourceDelegationId الفريد (1:1).
            var target = new Document
            {
                CreatedById = targetLawyer.Id,
                BranchId = branch.Id,
                SourceDelegationId = delegation.Id,
                GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
                DocumentType = "منفذ إنابة - المناب",
            };
            db.Documents.Add(target);
            await db.SaveChangesAsync();

            var reloaded = await db.Documents
                .Include(d => d.SourceDelegation)
                    .ThenInclude(dl => dl!.Assets)
                .Include(d => d.Delegations)
                    .ThenInclude(dl => dl.Assets)
                .FirstAsync(d => d.Id == source.Id);

            Assert.Single(reloaded.Delegations);
            Assert.Equal("دائرة تنفيذ دمشق", reloaded.Delegations.Single().DelegatedCourt);
            Assert.True(reloaded.Delegations.Single().IsExternal);
            var delegatedAsset = reloaded.Delegations.Single().Assets.Single();
            Assert.Equal("عقار رقم 77 — المزة", delegatedAsset.AssetLabel);
            Assert.Equal(AssetKindCatalog.RealEstate, delegatedAsset.AssetKind);
            Assert.Equal(500_000m, delegatedAsset.SalePrice);

            var targetReloaded = await db.Documents
                .Include(d => d.SourceDelegation)
                    .ThenInclude(dl => dl!.SourceDocument)
                .FirstAsync(d => d.Id == target.Id);
            Assert.Equal(source.Id, targetReloaded.SourceDelegation!.SourceDocumentId);
            Assert.Equal(DelegationStatusCatalog.Assigned, targetReloaded.SourceDelegation.Status);

            // لا يمكن ربط ملفين منابين بإنابة واحدة (فهرس فريد على SourceDelegationId).
            // نُفرغ التتبع ليمر الإدراج المكرر إلى القاعدة مباشرة ويرتد من الفهرس الفريد.
            db.ChangeTracker.Clear();
            var duplicate = new Document
            {
                CreatedById = targetLawyer.Id,
                BranchId = branch.Id,
                SourceDelegationId = delegation.Id,
                GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
                DocumentType = "منفذ إنابة - المناب المكرر",
            };
            db.Documents.Add(duplicate);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    // قائمة بيضاء بالجداول المسموح فحصها في الاختبار؛ تُقيَّد بها أسماء الجداول قبل
    // ضمّها نصيًا في استعلام pragma_table_info (الذي لا يقبل معامَلَين مرتبطين لاسم الجدول)
    // لضمان عدم وجود أي حقن SQL عبر المتغير table.
    private static readonly HashSet<string> AllowedTables =
        new(StringComparer.Ordinal)
        {
            "DocumentDelegations",
            "DelegationAssets",
        };

    // دالة مساعدة للاختبار تتحقق من وجود عمود Id في جدول SQLite عبر pragma_table_info.
    // pragma_table_info لا يقبل معامَلَين مرتبطين لاسم الجدول في SQLite، ولذلك يُضمَّن
    // الاسم نصيًا في الاستعلام، مع تقييده مسبقًا بقائمة بيضاء للجداول المعروفة
    // (منعًا لأي حقن SQL عبر بناء السلسلة). ولذلك يُسكَت تحذيرا EF1002/EF1003 هنا بشكل موضعي
    // ومبرَّر بعد ضمان أمن الاسم من القائمة البيضاء.
#pragma warning disable EF1002, EF1003 // مبرَّر: اسم الجدول مُقيَّد بقائمة بيضاء ولا يُبنى من مدخل مستخدم
    private static async Task<bool> TableExistsAsync(DocGeneratorDbContext db, string table)
    {
        if (!AllowedTables.Contains(table))
            throw new ArgumentException($"اسم جدول غير مسموح في فحص الاختبار: {table}", nameof(table));

        var found = await db.Database.SqlQueryRaw<string>(
            $"SELECT name AS Value FROM pragma_table_info('{table}') WHERE name = 'Id'").AnyAsync();
        return found;
    }
#pragma warning restore EF1002, EF1003
}
