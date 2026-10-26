using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using DocGenerator.Infrastructure.Persistence.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace DocGenerator.Api.Tests;

/// <summary>
/// حراسة هجرات إعادة هيكلة التنبيهات — مرحلة التوسّع (PR-1 / M1) وجدول Outbox
/// (الخطة §6.3 و§14.1).
/// </summary>
public class HeadAlertSchemaMigrationTests
{
    /// <summary>اسم هجرة التوسّع (SQLite) — مرجع اختباري ملزم.</summary>
    private const string ExpandMigration = "20261020202259_HeadAlertTaxonomy_Expand";

    /// <summary>اسم هجرة Outbox (SQLite) — مرجع اختباري ملزم.</summary>
    private const string OutboxMigration = "20261020202554_HeadAlertOutbox_Create";

    /// <summary>اسم هجرة M2 (SQLite) — مرجع اختباري ملزم.</summary>
    private const string BackfillMigration = "20261021232824_HeadAlertTaxonomy_Backfill";

    /// <summary>اسم هجرة M3 (SQLite) — مرجع اختباري ملزم.</summary>
    private const string ContractMigration = "20261026151929_HeadAlertTaxonomy_Contract";

    private static (string Path, DocGeneratorDbContext Db) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"docgen_headalert_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<DocGeneratorDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return (path, new DocGeneratorDbContext(options));
    }

    private static void Cleanup(string path)
    {
        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            try { File.Delete(path + suffix); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Migration_Expand_OldCodeStillWrites()
    {
        var (path, db) = CreateDb();
        try
        {
            // يُهاجَر حتى مرحلة التوسّع فقط (بلا backfill ولا فهرس فريد).
            await db.GetService<IMigrator>().MigrateAsync(ExpandMigration);

            var branch = new Branch { Name = "دمشق", Code = "DAM" };
            db.Branches.Add(branch);
            await db.SaveChangesAsync();

            var creator = new User
            {
                Username = "lawyer1",
                FullName = "محامي",
                Role = UserRole.Lawyer,
                BranchId = branch.Id,
                PasswordHash = "x",
            };
            db.Users.Add(creator);
            await db.SaveChangesAsync();

            // كتابة بالشكل القديم عبر SQL خام: الكيان الحالي بعد M3 لا يمثّل NULL،
            // لكن قاعدة M1 يجب أن تبقى متوافقة مع الكتابة القديمة.
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"HeadAlerts\" (\"BranchId\", \"CreatedById\", \"TargetType\", \"Message\", \"CreatedAt\") VALUES ({0}, {1}, {2}, {3}, '2026-10-20 00:00:00')",
                branch.Id, creator.Id, (int)HeadAlertTargetType.Branch, "تنبيه قديم بلا نوع");
            var legacyId = await db.Database.SqlQueryRaw<int>(
                "SELECT MAX(\"Id\") AS Value FROM \"HeadAlerts\"").SingleAsync();

            var storedKind = await db.Database.SqlQueryRaw<string?>(
                "SELECT \"Kind\" AS Value FROM \"HeadAlerts\" WHERE \"Id\" = {0}", legacyId).SingleAsync();
            var storedLastChanged = await db.Database.SqlQueryRaw<DateTime?>(
                "SELECT \"LastContentChangedAt\" AS Value FROM \"HeadAlerts\" WHERE \"Id\" = {0}", legacyId).SingleAsync();
            var storedDedupeKey = await db.Database.SqlQueryRaw<string?>(
                "SELECT \"DedupeKey\" AS Value FROM \"HeadAlerts\" WHERE \"Id\" = {0}", legacyId).SingleAsync();
            var storedIsArchived = await db.Database.SqlQueryRaw<int>(
                "SELECT \"IsArchived\" AS Value FROM \"HeadAlerts\" WHERE \"Id\" = {0}", legacyId).SingleAsync();
            var storedArchivedAt = await db.Database.SqlQueryRaw<DateTime?>(
                "SELECT \"ArchivedAt\" AS Value FROM \"HeadAlerts\" WHERE \"Id\" = {0}", legacyId).SingleAsync();

            Assert.Null(storedKind);
            Assert.Null(storedLastChanged);
            Assert.Null(storedDedupeKey);
            Assert.Equal(0, storedIsArchived);
            Assert.Null(storedArchivedAt);

            // لا قيد Kind (القيمة الفارغة مقبولة بعد M1).
            var withoutKind = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = creator.Id,
                TargetType = HeadAlertTargetType.Branch,
                Message = "تنبيه بلا نوع",
                CreatedAt = DateTime.UtcNow,
            };
            db.HeadAlerts.Add(withoutKind);
            await db.SaveChangesAsync();

            // لا فهرس فريد على DedupeKey بعد M1: مفتاحان متطابقان يُقبلان.
            var first = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = creator.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = creator.Id,
                Message = "أول",
                DedupeKey = "same:key",
                CreatedAt = DateTime.UtcNow,
            };
            var second = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = creator.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = creator.Id,
                Message = "ثانٍ",
                DedupeKey = "same:key",
                CreatedAt = DateTime.UtcNow,
            };
            db.HeadAlerts.AddRange(first, second);
            await db.SaveChangesAsync();
            Assert.Equal(2, await db.HeadAlerts.CountAsync(a => a.DedupeKey == "same:key"));

            // لا قيد فريد على (HeadAlertId, UserId) بعد M1.
            var alertId = (await db.HeadAlerts.SingleAsync(a => a.Message == "أول")).Id;
            db.HeadAlertRecipients.AddRange(
                new HeadAlertRecipient { HeadAlertId = alertId, UserId = creator.Id },
                new HeadAlertRecipient { HeadAlertId = alertId, UserId = creator.Id });
            await db.SaveChangesAsync();
            Assert.Equal(2, await db.HeadAlertRecipients.CountAsync(r => r.HeadAlertId == alertId));
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    [Fact]
    public async Task Migration_Expand_PreservesExistingRowsAndWidensMessage()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261014180913_AddInclusionNotes");

            // الإدراج بـ SQL خام: نموذج EF الحالي يحمل أعمدة مرحلة التوسّع (ArchivedAt
            // وغيرها) وهي غير موجودة في القاعدة قبل هذه المرحلة.
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Branches\" (\"Name\", \"Code\", \"IsActive\", \"CreatedAt\") VALUES ('دمشق', 'DAM', 1, '2026-10-20 00:00:00')");
            var branchId = await db.Database.SqlQueryRaw<int>("SELECT \"Id\" AS Value FROM \"Branches\" ORDER BY \"Id\" DESC LIMIT 1").FirstAsync();
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Users\" (\"Username\", \"FullName\", \"Role\", \"PasswordHash\", \"FailedLoginCount\", \"IsActive\", \"TokenVersion\", \"CreatedAt\", \"UpdatedAt\", \"BranchId\") VALUES ('lawyer1', 'محامي', 'lawyer', 'x', 0, 1, 0, '2026-10-20 00:00:00', '2026-10-20 00:00:00', {0})",
                branchId);
            var userId = await db.Database.SqlQueryRaw<int>("SELECT \"Id\" AS Value FROM \"Users\" ORDER BY \"Id\" DESC LIMIT 1").FirstAsync();

            const string longMessage = "نص طويل جدًا يتجاوز الحد القديم بوضوح — يُحفظ كاملًا بلا قصّ";
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"HeadAlerts\" (\"BranchId\", \"CreatedById\", \"TargetType\", \"Message\", \"CreatedAt\") VALUES ({0}, {1}, 3, {2}, '2026-10-20 00:00:00')",
                branchId, userId, longMessage);

            // تطبيق مرحلة التوسّع: تُضاف الأعمدة الجديدة ولا يُمس النص.
            await db.GetService<IMigrator>().MigrateAsync(ExpandMigration);

            var storedMessage = await db.Database.SqlQueryRaw<string>(
                "SELECT \"Message\" AS Value FROM \"HeadAlerts\" LIMIT 1").SingleAsync();
            var storedKind = await db.Database.SqlQueryRaw<string?>(
                "SELECT \"Kind\" AS Value FROM \"HeadAlerts\" LIMIT 1").SingleAsync();
            var storedLastChanged = await db.Database.SqlQueryRaw<DateTime?>(
                "SELECT \"LastContentChangedAt\" AS Value FROM \"HeadAlerts\" LIMIT 1").SingleAsync();
            var storedDedupeKey = await db.Database.SqlQueryRaw<string?>(
                "SELECT \"DedupeKey\" AS Value FROM \"HeadAlerts\" LIMIT 1").SingleAsync();
            var storedIsArchived = await db.Database.SqlQueryRaw<int>(
                "SELECT \"IsArchived\" AS Value FROM \"HeadAlerts\" LIMIT 1").SingleAsync();
            var storedArchivedAt = await db.Database.SqlQueryRaw<DateTime?>(
                "SELECT \"ArchivedAt\" AS Value FROM \"HeadAlerts\" LIMIT 1").SingleAsync();

            Assert.Equal(longMessage, storedMessage);
            Assert.Null(storedKind);
            Assert.Null(storedLastChanged);
            Assert.Null(storedDedupeKey);
            Assert.Equal(0, storedIsArchived);
            Assert.Null(storedArchivedAt);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    [Fact]
    public async Task Outbox_Schema_TableIndexesAndConstraintsExist()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(OutboxMigration);

            // الجدول موجود.
            var tables = await db.Database
                .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'")
                .ToListAsync();
            Assert.Contains("HeadAlertOutboxMessages", tables);

            // الفهارس الأربعة.
            var indexes = await db.Database
                .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'index' AND tbl_name = 'HeadAlertOutboxMessages'")
                .ToListAsync();
            Assert.Contains("IX_HeadAlertOutboxMessages_Status_NextAttemptAt_Id", indexes);
            Assert.Contains("IX_HeadAlertOutboxMessages_Status_LockedUntil", indexes);
            Assert.Contains("IX_HeadAlertOutboxMessages_Status_TargetUserId", indexes);
            Assert.Contains("IX_HeadAlertOutboxMessages_Status_ProcessedAt", indexes);

            // القيود موجودة بأسمائها.
            var schema = await db.Database
                .SqlQueryRaw<string>("SELECT sql AS Value FROM sqlite_master WHERE type = 'table' AND name = 'HeadAlertOutboxMessages'")
                .ToListAsync();
            var tableSql = Assert.Single(schema);
            Assert.Contains("CK_HeadAlertOutbox_Operation", tableSql);
            Assert.Contains("CK_HeadAlertOutbox_Status", tableSql);
            Assert.Contains("CK_HeadAlertOutbox_Kind_Operation", tableSql);
            Assert.Contains("CK_HeadAlertOutbox_PayloadVersion", tableSql);
            Assert.Contains("CK_HeadAlertOutbox_Attempts", tableSql);

            // لا مفاتيح أجنبية على الجدول.
            var foreignKeys = await db.Database
                .SqlQueryRaw<string>("SELECT \"table\" AS Value FROM pragma_foreign_key_list('HeadAlertOutboxMessages')")
                .ToListAsync();
            Assert.Empty(foreignKeys);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    [Fact]
    public async Task Outbox_Constraints_RejectInvalidValues()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(OutboxMigration);

            // القيم غير المسموحة لا تُنتَج من الكود (المحوّل يكتب تعدادًا صالحًا دائمًا)،
            // فتُحقن بـ SQL خام لإثبات أن القيد على مستوى القاعدة يرفضها.
            const string insertSql = """
                INSERT INTO "HeadAlertOutboxMessages"
                    ("Id", "Operation", "Status", "Kind", "Payload", "PayloadVersion",
                     "OccurredAt", "CreatedAt", "NextAttemptAt", "Attempts")
                VALUES ({0}, {1}, {2}, {3}, 'payload', 1, '2026-10-20 00:00:00', '2026-10-20 00:00:00', '2026-10-20 00:00:00', 0)
                """;

            // حالة غير مسموحة.
            await Assert.ThrowsAsync<SqliteException>(async () =>
                await db.Database.ExecuteSqlRawAsync(insertSql, 9001, "upsert", "bogus_status", "kind"));

            // عملية غير مسموحة.
            await Assert.ThrowsAsync<SqliteException>(async () =>
                await db.Database.ExecuteSqlRawAsync(insertSql, 9002, "bogus_op", "pending", "kind"));

            // نوع فارغ مع عملية upsert — ممنوع.
            await Assert.ThrowsAsync<SqliteException>(async () =>
                await db.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO "HeadAlertOutboxMessages"
                        ("Id", "Operation", "Status", "Kind", "Payload", "PayloadVersion",
                         "OccurredAt", "CreatedAt", "NextAttemptAt", "Attempts")
                    VALUES (9003, 'upsert', 'pending', NULL, 'payload', 1, '2026-10-20 00:00:00', '2026-10-20 00:00:00', '2026-10-20 00:00:00', 0)
                    """));

            // نوع غير فارغ مع عملية circuit_renamed — ممنوع.
            await Assert.ThrowsAsync<SqliteException>(async () =>
                await db.Database.ExecuteSqlRawAsync(insertSql, 9004, "circuit_renamed", "pending", "kind", "{}"));

            // إصدار حمولة غير موجب — ممنوع.
            await Assert.ThrowsAsync<SqliteException>(async () =>
                await db.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO "HeadAlertOutboxMessages"
                        ("Id", "Operation", "Status", "Kind", "Payload", "PayloadVersion",
                         "OccurredAt", "CreatedAt", "NextAttemptAt", "Attempts")
                    VALUES (9005, 'upsert', 'pending', 'kind', 'payload', 0, '2026-10-20 00:00:00', '2026-10-20 00:00:00', '2026-10-20 00:00:00', 0)
                    """));

            // عدد محاولات سالب — ممنوع.
            await Assert.ThrowsAsync<SqliteException>(async () =>
                await db.Database.ExecuteSqlRawAsync(
                    """
                    INSERT INTO "HeadAlertOutboxMessages"
                        ("Id", "Operation", "Status", "Kind", "Payload", "PayloadVersion",
                         "OccurredAt", "CreatedAt", "NextAttemptAt", "Attempts")
                    VALUES (9006, 'upsert', 'pending', 'kind', 'payload', 1, '2026-10-20 00:00:00', '2026-10-20 00:00:00', '2026-10-20 00:00:00', -1)
                    """));

            // القيم الصالحة تُقبل.
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "HeadAlertOutboxMessages"
                    ("Id", "Operation", "Status", "Kind", "Payload", "PayloadVersion",
                     "OccurredAt", "CreatedAt", "NextAttemptAt", "Attempts")
                VALUES (9007, 'circuit_renamed', 'pending', NULL, 'payload', 1, '2026-10-20 00:00:00', '2026-10-20 00:00:00', '2026-10-20 00:00:00', 0)
                """);
            Assert.Equal(1, await db.HeadAlertOutboxMessages.CountAsync());
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    [Fact]
    public async Task Outbox_ValidMessageRoundTrips_WithLowercaseStatus()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(OutboxMigration);

            var message = new HeadAlertOutboxMessage
            {
                Operation = HeadAlertOutboxOperation.Upsert,
                Status = HeadAlertOutboxStatus.Pending,
                Kind = HeadAlertKindCatalog.DelegationPendingApproval,
                Payload = "{\"operation\":\"upsert\"}",
                PayloadVersion = 1,
                OccurredAt = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc),
                NextAttemptAt = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc),
            };
            db.HeadAlertOutboxMessages.Add(message);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var stored = await db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
            Assert.Equal(HeadAlertOutboxStatus.Pending, stored.Status);
            Assert.Equal(HeadAlertOutboxOperation.Upsert, stored.Operation);
            Assert.Equal(HeadAlertKindCatalog.DelegationPendingApproval, stored.Kind);

            // القيم مخزَّنة بنص صغير (lowercase) لا كعدد رقمي.
            var raw = await db.Database
                .SqlQueryRaw<string>("SELECT \"Status\" AS Value FROM \"HeadAlertOutboxMessages\"")
                .ToListAsync();
            Assert.Equal("pending", Assert.Single(raw));

            var rawOperation = await db.Database
                .SqlQueryRaw<string>("SELECT \"Operation\" AS Value FROM \"HeadAlertOutboxMessages\"")
                .ToListAsync();
            Assert.Equal("upsert", Assert.Single(rawOperation));
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    [Fact]
    public async Task Outbox_Down_RefusesToDropTableWithUnresolvedMessages()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(OutboxMigration);

            db.HeadAlertOutboxMessages.Add(new HeadAlertOutboxMessage
            {
                Operation = HeadAlertOutboxOperation.Upsert,
                Status = HeadAlertOutboxStatus.Pending,
                Kind = HeadAlertKindCatalog.DelegationPendingApproval,
                Payload = "{}",
                PayloadVersion = 1,
                OccurredAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                NextAttemptAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();

            // التراجع إلى ما قبل هجرة Outbox يجب أن يُرفض لوجود رسالة غير محسومة.
            await Assert.ThrowsAnyAsync<Exception>(async () =>
                await db.GetService<IMigrator>().MigrateAsync("20261020202259_HeadAlertTaxonomy_Expand"));
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    /// <summary>
    /// M2 لا تلمس صفوف PR-2 غير الفارغة، وتعبّئ القديمة فقط، وتؤرشف تنبيهات
    /// مراجعة الجهات المتقاعدة.
    /// </summary>
    [Fact]
    public async Task Migration_Backfill_LegacyOnlyDoesNotOverwriteNewRows()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(OutboxMigration);
            var (branch, user) = await SeedBranchAndUserAsync(db);

            var legacyCreatedAt = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
            var newLastChangedAt = new DateTime(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc);
            var legacyId = await InsertLegacyAlertAsync(
                db, branch.Id, user.Id, HeadAlertTargetType.Branch,
                "تنبيه قديم بلا تصنيف", legacyCreatedAt);
            var retiredReviewId = await InsertLegacyAlertAsync(
                db, branch.Id, user.Id, HeadAlertTargetType.Branch,
                "المحامي فلان أدخل جهة عامة جديدة «جهة» (دمشق / أول) — يرجى مراجعتها",
                legacyCreatedAt.AddMinutes(5));
            var newRow = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = user.Id,
                Kind = HeadAlertKindCatalog.DelegationMirrorUpdated,
                DedupeKey = "preserve:key",
                Message = "تنبيه جديد لا يُمس",
                CreatedAt = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc),
                LastContentChangedAt = newLastChangedAt,
            };
            db.HeadAlerts.Add(newRow);
            await db.SaveChangesAsync();

            await db.GetService<IMigrator>().MigrateAsync(BackfillMigration);
            db.ChangeTracker.Clear();

            var storedLegacy = await db.HeadAlerts.AsNoTracking().SingleAsync(a => a.Id == legacyId);
            Assert.Equal(HeadAlertKindCatalog.Legacy, storedLegacy.Kind);
            Assert.Equal(legacyCreatedAt, storedLegacy.LastContentChangedAt);
            Assert.Null(storedLegacy.DedupeKey);

            var storedNew = await db.HeadAlerts.AsNoTracking().SingleAsync(a => a.Id == newRow.Id);
            Assert.Equal(HeadAlertKindCatalog.DelegationMirrorUpdated, storedNew.Kind);
            Assert.Equal("preserve:key", storedNew.DedupeKey);
            Assert.Equal("تنبيه جديد لا يُمس", storedNew.Message);
            Assert.Equal(newLastChangedAt, storedNew.LastContentChangedAt);
            Assert.False(storedNew.IsArchived);

            var storedRetired = await db.HeadAlerts.AsNoTracking().SingleAsync(a => a.Id == retiredReviewId);
            Assert.True(storedRetired.IsArchived);
            Assert.NotNull(storedRetired.ArchivedAt);
            Assert.Null(storedRetired.DedupeKey);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    /// <summary>دمج تكرارات (HeadAlertId, UserId) قبل إنشاء القيد الفريد.</summary>
    [Fact]
    public async Task Migration_DuplicateRecipientRows_MergedBeforeUniqueConstraint()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(OutboxMigration);
            var (branch, user) = await SeedBranchAndUserAsync(db);

            var alert = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = user.Id,
                Message = "تنبيه لتكرار المستلم",
                CreatedAt = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc),
            };
            db.HeadAlerts.Add(alert);
            await db.SaveChangesAsync();

            var earliestRead = new DateTime(2026, 10, 20, 11, 0, 0, DateTimeKind.Utc);
            db.HeadAlertRecipients.AddRange(
                new HeadAlertRecipient { HeadAlertId = alert.Id, UserId = user.Id, IsRead = false },
                new HeadAlertRecipient { HeadAlertId = alert.Id, UserId = user.Id, IsRead = true, ReadAt = earliestRead.AddHours(1) },
                new HeadAlertRecipient { HeadAlertId = alert.Id, UserId = user.Id, IsRead = true, ReadAt = earliestRead });
            await db.SaveChangesAsync();

            await db.GetService<IMigrator>().MigrateAsync(BackfillMigration);
            db.ChangeTracker.Clear();

            var recipients = await db.HeadAlertRecipients.AsNoTracking()
                .Where(r => r.HeadAlertId == alert.Id)
                .ToListAsync();
            var recipient = Assert.Single(recipients);
            Assert.True(recipient.IsRead);
            Assert.Equal(earliestRead, recipient.ReadAt);

            var indexes = await db.Database
                .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'index' AND tbl_name = 'HeadAlertRecipients'")
                .ToListAsync();
            Assert.Contains("UX_HeadAlertRecipients_HeadAlertId_UserId", indexes);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    /// <summary>حل تصادم DedupeKey: حامل واحد نشط، والخاسر مؤرشف بلا مفتاح.</summary>
    [Fact]
    public async Task Migration_DuplicateDedupeKeyCollision_ResolvedBeforeUniqueIndex()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(OutboxMigration);
            var (branch, user) = await SeedBranchAndUserAsync(db);

            const string key = "delegation:1:recipient:1:delegation_mirror_updated";
            var older = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = user.Id,
                Kind = HeadAlertKindCatalog.DelegationMirrorUpdated,
                DedupeKey = key,
                Message = "النسخة الأقدم",
                CreatedAt = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc),
                LastContentChangedAt = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc),
            };
            var newer = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = user.Id,
                Kind = HeadAlertKindCatalog.DelegationMirrorUpdated,
                DedupeKey = key,
                Message = "النسخة الأحدث",
                CreatedAt = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc),
                LastContentChangedAt = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc),
            };
            db.HeadAlerts.AddRange(older, newer);
            await db.SaveChangesAsync();

            db.HeadAlertRecipients.AddRange(
                new HeadAlertRecipient { HeadAlertId = older.Id, UserId = user.Id, IsRead = true, ReadAt = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc) },
                new HeadAlertRecipient { HeadAlertId = newer.Id, UserId = user.Id, IsRead = false });
            await db.SaveChangesAsync();

            await db.GetService<IMigrator>().MigrateAsync(BackfillMigration);
            db.ChangeTracker.Clear();

            var holder = await db.HeadAlerts.AsNoTracking().SingleAsync(a => a.DedupeKey == key);
            Assert.Equal(newer.Id, holder.Id);
            Assert.False(holder.IsArchived);

            var loser = await db.HeadAlerts.AsNoTracking().SingleAsync(a => a.Id == older.Id);
            Assert.True(loser.IsArchived);
            Assert.Equal(HeadAlertKindCatalog.Legacy, loser.Kind);
            Assert.Null(loser.DedupeKey);
            Assert.NotNull(loser.ArchivedAt);

            var recipient = Assert.Single(await db.HeadAlertRecipients.AsNoTracking()
                .Where(r => r.HeadAlertId == holder.Id)
                .ToListAsync());
            Assert.True(recipient.IsRead);
            Assert.Equal(new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc), recipient.ReadAt);

            var indexes = await db.Database
                .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'index' AND tbl_name = 'HeadAlerts'")
                .ToListAsync();
            Assert.Contains("UX_HeadAlerts_DedupeKey_Active", indexes);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    /// <summary>الفهرس الجزئي الفريد يرفض مفتاحًا نشطًا مكررًا، ويقبله بعد أرشفة الصف الحالي.</summary>
    [Fact]
    public async Task Migration_PartialUniqueIndex_BlocksDuplicateActiveKey_AllowsAfterArchive()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(BackfillMigration);
            var (branch, user) = await SeedBranchAndUserAsync(db);

            var activeKey = "delegation:7:recipient:9:delegation_pending_approval";
            var first = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = user.Id,
                Kind = HeadAlertKindCatalog.DelegationPendingApproval,
                DedupeKey = activeKey,
                Message = "الأول",
                CreatedAt = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc),
                LastContentChangedAt = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc),
            };
            db.HeadAlerts.Add(first);
            await db.SaveChangesAsync();

            var duplicate = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = user.Id,
                Kind = HeadAlertKindCatalog.DelegationPendingApproval,
                DedupeKey = activeKey,
                Message = "المكرر النشط",
                CreatedAt = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc),
                LastContentChangedAt = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc),
            };
            db.HeadAlerts.Add(duplicate);

            var ex = await Assert.ThrowsAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
            Assert.IsType<SqliteException>(ex.InnerException);

            db.ChangeTracker.Clear();
            var storedFirst = await db.HeadAlerts.SingleAsync(a => a.Id == first.Id);
            storedFirst.IsArchived = true;
            storedFirst.ArchivedAt = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);
            storedFirst.DedupeKey = null;
            await db.SaveChangesAsync();

            var duplicate2 = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = user.Id,
                Kind = HeadAlertKindCatalog.DelegationPendingApproval,
                DedupeKey = activeKey,
                Message = "المكرر النشط بعد الأرشفة",
                CreatedAt = new DateTime(2026, 10, 20, 11, 0, 0, DateTimeKind.Utc),
                LastContentChangedAt = new DateTime(2026, 10, 20, 11, 0, 0, DateTimeKind.Utc),
            };
            db.HeadAlerts.Add(duplicate2);
            await db.SaveChangesAsync();

            var storedDuplicate = await db.HeadAlerts.AsNoTracking().SingleAsync(a => a.DedupeKey == activeKey && !a.IsArchived);
            Assert.Equal(duplicate2.Message, storedDuplicate.Message);

            var indexes = await db.Database
                .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'index' AND tbl_name = 'HeadAlerts'")
                .ToListAsync();
            Assert.Contains("UX_HeadAlerts_DedupeKey_Active", indexes);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    /// <summary>تصنيف المعلقات القديمة وربط ExecutionCircuitId من المستند.</summary>
    [Fact]
    public async Task Migration_Backfill_SetsLastContentChangedAndCircuit()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(OutboxMigration);
            var (branch, user) = await SeedBranchAndUserAsync(db);

            var circuit = new ExecutionCircuit
            {
                BranchId = branch.Id,
                Name = "دائرة التنفيذ الأولى",
                NameNorm = "دائرة التنفيذ الاولى",
                CreatedById = user.Id,
            };
            db.ExecutionCircuits.Add(circuit);
            await db.SaveChangesAsync();

            var document = new Document
            {
                CreatedById = user.Id,
                BranchId = branch.Id,
                ExecutionCircuitId = circuit.Id,
            };
            db.Documents.Add(document);
            await db.SaveChangesAsync();

            var createdAt = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
            var alertId = await InsertLegacyAlertAsync(
                db, branch.Id, user.Id, HeadAlertTargetType.Lawyer,
                $"{ExecutionCircuitPendingPrefix}{circuit.Name} — يرجى تحديث معلوماتها",
                createdAt,
                documentId: document.Id,
                targetLawyerId: user.Id);
            db.HeadAlertRecipients.Add(new HeadAlertRecipient { HeadAlertId = alertId, UserId = user.Id });
            await db.SaveChangesAsync();

            await db.GetService<IMigrator>().MigrateAsync(BackfillMigration);
            db.ChangeTracker.Clear();

            var stored = await db.HeadAlerts.AsNoTracking().SingleAsync(a => a.Id == alertId);
            Assert.Equal(HeadAlertKindCatalog.CircuitPendingDocuments, stored.Kind);
            Assert.Equal(circuit.Id, stored.ExecutionCircuitId);
            Assert.Equal(createdAt, stored.LastContentChangedAt);
            Assert.Equal(
                $"circuit-pending:{branch.Id}:{circuit.Id}:recipient:{user.Id}:circuit_pending_documents",
                stored.DedupeKey);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    /// <summary>
    /// تصنيف تدفقات الإنابة والاستئناف وكتاب المطالعة القديمة وتوليد مفاتيحها
    /// وفق §6.5، مع بقاء الصفوف غير المطابقة نصيًا legacy.
    /// </summary>
    [Fact]
    public async Task Migration_LegacyDelegationAppealReview_ClassifiedWithKeys()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(OutboxMigration);
            var (branch, user) = await SeedBranchAndUserAsync(db);

            var document = new Document
            {
                CreatedById = user.Id,
                BranchId = branch.Id,
            };
            db.Documents.Add(document);
            await db.SaveChangesAsync();

            var delegation = new DocumentDelegation
            {
                SourceDocumentId = document.Id,
                SourceDocument = document,
                CreatedById = user.Id,
                CreatedBy = user,
            };
            db.DocumentDelegations.Add(delegation);
            await db.SaveChangesAsync();

            var appeal = new DocumentAppeal
            {
                DocumentId = document.Id,
                Document = document,
                CreatedById = user.Id,
                CreatedBy = user,
            };
            db.DocumentAppeals.Add(appeal);
            await db.SaveChangesAsync();

            var review = new ReviewLetter
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                LetterNumber = "R-1",
                CreatedBy = user,
            };
            db.ReviewLetters.Add(review);
            await db.SaveChangesAsync();

            var createdAt = new DateTime(2026, 10, 20, 13, 0, 0, DateTimeKind.Utc);
            var delegationAlertId = await InsertLegacyAlertAsync(
                db, branch.Id, user.Id, HeadAlertTargetType.Head,
                "بانتظار اعتماد الإنابة — سطّر المحامي فلان إنابة",
                createdAt,
                delegationId: delegation.Id);
            var appealAlertId = await InsertLegacyAlertAsync(
                db, branch.Id, user.Id, HeadAlertTargetType.Head,
                "يرجى التفضل بإسناد الاستئناف (ملف 1 — محال من فلان)",
                createdAt.AddMinutes(1),
                appealId: appeal.Id);
            var reviewAlertId = await InsertLegacyAlertAsync(
                db, branch.Id, user.Id, HeadAlertTargetType.Lawyer,
                "رئيس القسم ردّ على كتاب مطالعتك رقم R-1: تم",
                createdAt.AddMinutes(2),
                targetLawyerId: user.Id,
                reviewLetterId: review.Id);

            db.HeadAlertRecipients.AddRange(
                new HeadAlertRecipient { HeadAlertId = delegationAlertId, UserId = user.Id },
                new HeadAlertRecipient { HeadAlertId = appealAlertId, UserId = user.Id });
            await db.SaveChangesAsync();

            await db.GetService<IMigrator>().MigrateAsync(BackfillMigration);
            db.ChangeTracker.Clear();

            var storedDelegation = await db.HeadAlerts.AsNoTracking().SingleAsync(a => a.Id == delegationAlertId);
            Assert.Equal(HeadAlertKindCatalog.DelegationPendingApproval, storedDelegation.Kind);
            Assert.Equal(
                $"delegation:{delegation.Id}:recipient:{user.Id}:delegation_pending_approval",
                storedDelegation.DedupeKey);
            Assert.Equal(createdAt, storedDelegation.LastContentChangedAt);

            var storedAppeal = await db.HeadAlerts.AsNoTracking().SingleAsync(a => a.Id == appealAlertId);
            Assert.Equal(HeadAlertKindCatalog.AppealReferralToHead, storedAppeal.Kind);
            Assert.Equal(
                $"appeal:{appeal.Id}:recipient:{user.Id}:appeal_referral_to_head",
                storedAppeal.DedupeKey);

            var storedReview = await db.HeadAlerts.AsNoTracking().SingleAsync(a => a.Id == reviewAlertId);
            Assert.Equal(HeadAlertKindCatalog.ReviewLetterReply, storedReview.Kind);
            Assert.Equal(
                $"review:{review.Id}:recipient:{user.Id}:review_letter_reply",
                storedReview.DedupeKey);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    /// <summary>
    /// §14.1: صفوف منشئ الملف النظامية القديمة تُصنَّف بمفتاح المستند/المنشئ،
    /// بينما التنبيه اليدوي وصاحب النص نفسه على نوع هدف خاطئ يبقيان legacy.
    /// </summary>
    [Fact]
    public async Task Migration_LegacyDocumentCreator_ClassifiedOrExcludedManual()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(OutboxMigration);
            var (branch, user) = await SeedBranchAndUserAsync(db);

            var document = new Document
            {
                CreatedById = user.Id,
                BranchId = branch.Id,
            };
            db.Documents.Add(document);
            await db.SaveChangesAsync();

            var delegation = new DocumentDelegation
            {
                SourceDocumentId = document.Id,
                SourceDocument = document,
                CreatedById = user.Id,
                CreatedBy = user,
            };
            db.DocumentDelegations.Add(delegation);
            await db.SaveChangesAsync();

            var createdAt = new DateTime(2026, 10, 20, 13, 3, 0, DateTimeKind.Utc);
            var manualAlert = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Document,
                DocumentId = document.Id,
                Message = "تنبيه من رئيس القسم: أحال إليك رئيس القسم ملف إنابة لقيده أصولًا",
                CreatedAt = createdAt,
            };
            var creatorAssigned = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Document,
                DocumentId = document.Id,
                Message = "أحال إليك رئيس القسم ملف إنابة لقيده أصولًا في محكمة دمشق (ملف 1)",
                CreatedAt = createdAt.AddMinutes(1),
            };
            var creatorRejected = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Document,
                DocumentId = document.Id,
                DelegationId = delegation.Id,
                Message = "رُفضت الإنابة (رقم 1) للدائرة الخطأ — سبب — صحّح الدائرة من بطاقة تشعبات الملف",
                CreatedAt = createdAt.AddMinutes(2),
            };
            var creatorCompleted = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Document,
                DocumentId = document.Id,
                Message = "أُعيدت الإنابة المسطرة إلى دائرة محكمة دمشق في ملف 1 منفذة — البدل غطى كامل المديونية",
                CreatedAt = createdAt.AddMinutes(3),
            };
            var sameTextWrongTarget = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = user.Id,
                DocumentId = document.Id,
                Message = "أحال إليك رئيس القسم ملف إنابة لقيده أصولًا في محكمة دمشق (ملف 1)",
                CreatedAt = createdAt.AddMinutes(4),
            };
            db.HeadAlerts.AddRange(
                manualAlert,
                creatorAssigned,
                creatorRejected,
                creatorCompleted,
                sameTextWrongTarget);
            await db.SaveChangesAsync();

            await db.GetService<IMigrator>().MigrateAsync(BackfillMigration);
            db.ChangeTracker.Clear();

            var storedManual = await db.HeadAlerts.AsNoTracking().SingleAsync(a => a.Id == manualAlert.Id);
            Assert.Equal(HeadAlertKindCatalog.Legacy, storedManual.Kind);
            Assert.Null(storedManual.DedupeKey);
            Assert.False(storedManual.IsArchived);

            foreach (var (alert, kind) in new[]
                     {
                         (creatorAssigned, HeadAlertKindCatalog.DelegationAssignedToLawyer),
                         (creatorRejected, HeadAlertKindCatalog.DelegationRejectedWrongCourt),
                         (creatorCompleted, HeadAlertKindCatalog.DelegationCompletedReturned),
                     })
            {
                var stored = await db.HeadAlerts.AsNoTracking().SingleAsync(a => a.Id == alert.Id);
                Assert.Equal(kind, stored.Kind);
                Assert.Equal($"document:{document.Id}:recipient:{user.Id}:{kind}", stored.DedupeKey);
            }

            // النص نفسه على TargetType=Lawyer لا يُصنَّف كتنبيه منشئ ملف.
            var storedWrongTarget = await db.HeadAlerts.AsNoTracking()
                .SingleAsync(a => a.Id == sameTextWrongTarget.Id);
            Assert.Equal(HeadAlertKindCatalog.Legacy, storedWrongTarget.Kind);
            Assert.Null(storedWrongTarget.DedupeKey);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    /// <summary>تراجع M2 يعيد مخطط ما قبلها بلا عكس بيانات الـbackfill.</summary>
    [Fact]
    public async Task Migration_Backfill_Down_RestoresPreBackfillIndexes()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(OutboxMigration);
            var (branch, user) = await SeedBranchAndUserAsync(db);
            db.HeadAlerts.Add(new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Branch,
                Message = "صف يبقى بعد التراجع",
            });
            await db.SaveChangesAsync();

            await db.GetService<IMigrator>().MigrateAsync(BackfillMigration);
            await db.GetService<IMigrator>().MigrateAsync(OutboxMigration);
            db.ChangeTracker.Clear();

            var indexes = await db.Database
                .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'index' AND tbl_name = 'HeadAlerts'")
                .ToListAsync();
            Assert.Contains("IX_HeadAlerts_TargetLawyerId", indexes);
            Assert.DoesNotContain("UX_HeadAlerts_DedupeKey_Active", indexes);

            var columns = await db.Database
                .SqlQueryRaw<string>("SELECT name AS Value FROM pragma_table_info('HeadAlerts')")
                .ToListAsync();
            Assert.Contains("Kind", columns);
            Assert.Contains("DedupeKey", columns);
            Assert.Contains("ExecutionCircuitId", columns);

            var foreignKeys = await db.Database
                .SqlQueryRaw<string>("SELECT \"table\" AS Value FROM pragma_foreign_key_list('HeadAlerts')")
                .ToListAsync();
            Assert.DoesNotContain("ExecutionCircuits", foreignKeys);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    /// <summary>
    /// §6.5/§14.1: صفوف التيار الواحد القديمة تُرقّى بمفتاح واحد، والازدواج داخل
    /// التيار يُحسم بحامل نشط واحد ودمج قراءة المستلمين وأرشفة الباقي.
    /// </summary>
    [Fact]
    public async Task Migration_LegacyBackfill_SingleActiveRowPerStream()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(OutboxMigration);
            var (branch, user) = await SeedBranchAndUserAsync(db);

            var document = new Document
            {
                CreatedById = user.Id,
                BranchId = branch.Id,
            };
            db.Documents.Add(document);
            await db.SaveChangesAsync();

            var delegation = new DocumentDelegation
            {
                SourceDocumentId = document.Id,
                CreatedById = user.Id,
            };
            var appeal = new DocumentAppeal
            {
                DocumentId = document.Id,
                CreatedById = user.Id,
            };
            db.DocumentDelegations.Add(delegation);
            db.DocumentAppeals.Add(appeal);
            await db.SaveChangesAsync();

            var olderCreatedAt = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
            var newerCreatedAt = olderCreatedAt.AddHours(2);
            var olderDelegation = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Head,
                DelegationId = delegation.Id,
                Message = "بانتظار اعتماد الإنابة — سطّر المحامي فلان إنابة",
                CreatedAt = olderCreatedAt,
            };
            var newerDelegation = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Head,
                DelegationId = delegation.Id,
                Message = "بانتظار اعتماد الإنابة — سطّر المحامي فلان إنابة",
                CreatedAt = newerCreatedAt,
            };
            var appealAlert = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Head,
                AppealId = appeal.Id,
                Message = "يرجى التفضل بإسناد الاستئناف (ملف 1 — محال من فلان)",
                CreatedAt = olderCreatedAt.AddMinutes(30),
            };
            db.HeadAlerts.AddRange(olderDelegation, newerDelegation, appealAlert);
            await db.SaveChangesAsync();

            db.HeadAlertRecipients.AddRange(
                new HeadAlertRecipient
                {
                    HeadAlertId = olderDelegation.Id,
                    UserId = user.Id,
                    IsRead = true,
                    ReadAt = olderCreatedAt.AddHours(1),
                },
                new HeadAlertRecipient { HeadAlertId = newerDelegation.Id, UserId = user.Id },
                new HeadAlertRecipient { HeadAlertId = appealAlert.Id, UserId = user.Id });
            await db.SaveChangesAsync();

            await db.GetService<IMigrator>().MigrateAsync(BackfillMigration);
            db.ChangeTracker.Clear();

            // لا صفان نشطان بالمفتاح نفسه — ما يفرضه الفهرس الفريد الجزئي.
            var duplicateActiveKeys = await db.Database.SqlQueryRaw<int>(
                """
                SELECT COUNT(*) AS Value
                FROM (
                    SELECT "DedupeKey"
                    FROM "HeadAlerts"
                    WHERE NOT "IsArchived" AND "DedupeKey" IS NOT NULL
                    GROUP BY "DedupeKey"
                    HAVING COUNT(*) > 1)
                """).SingleAsync();
            Assert.Equal(0, duplicateActiveKeys);

            var delegationKey =
                $"delegation:{delegation.Id}:recipient:{user.Id}:delegation_pending_approval";
            var holder = await db.HeadAlerts.AsNoTracking()
                .SingleAsync(a => a.DedupeKey == delegationKey);
            Assert.Equal(newerDelegation.Id, holder.Id);
            Assert.Equal(HeadAlertKindCatalog.DelegationPendingApproval, holder.Kind);
            Assert.False(holder.IsArchived);

            // قراءة النُسخ المدموجة تُحفظ: الأقدم كان مقروءًا، والحامل أحدث نسخة.
            var mergedRecipient = Assert.Single(await db.HeadAlertRecipients.AsNoTracking()
                .Where(r => r.HeadAlertId == holder.Id)
                .ToListAsync());
            Assert.True(mergedRecipient.IsRead);
            Assert.Equal(olderCreatedAt.AddHours(1), mergedRecipient.ReadAt);

            var loser = await db.HeadAlerts.AsNoTracking()
                .SingleAsync(a => a.Id == olderDelegation.Id);
            Assert.True(loser.IsArchived);
            Assert.Equal(HeadAlertKindCatalog.Legacy, loser.Kind);
            Assert.Null(loser.DedupeKey);
            Assert.NotNull(loser.ArchivedAt);

            // تيار ثانٍ مستقل: صف نشط واحد بمفتاح الاستئناف.
            var appealKey = $"appeal:{appeal.Id}:recipient:{user.Id}:appeal_referral_to_head";
            var appealHolder = await db.HeadAlerts.AsNoTracking()
                .SingleAsync(a => a.DedupeKey == appealKey);
            Assert.Equal(appealAlert.Id, appealHolder.Id);
            Assert.Equal(HeadAlertKindCatalog.AppealReferralToHead, appealHolder.Kind);

            Assert.Equal(
                2,
                await db.HeadAlerts.CountAsync(a => !a.IsArchived && a.DedupeKey != null));
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    /// <summary>
    /// §6.5/§14.1: بادئة المعلقات القديمة لا تبقى نشطة بلا تصنيف؛ إما تُربط بدائرة
    /// وتُصنَّف <c>circuit_pending_documents</c>، أو تبقى/تُؤرشف ويُغطيها تقرير §16.
    /// </summary>
    [Fact]
    public async Task Migration_LegacyPendingPrefix_AllClassifiedOrReported()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(OutboxMigration);
            var (branch, user) = await SeedBranchAndUserAsync(db);

            var circuitByDocument = new ExecutionCircuit
            {
                BranchId = branch.Id,
                Name = "دائرة التنفيذ الأولى",
                NameNorm = "دائرة التنفيذ الاولى",
                CreatedById = user.Id,
            };
            var circuitByName = new ExecutionCircuit
            {
                BranchId = branch.Id,
                Name = "دائرة التنفيذ الثانية",
                NameNorm = "دائرة التنفيذ الثانية",
                CreatedById = user.Id,
            };
            db.ExecutionCircuits.AddRange(circuitByDocument, circuitByName);
            await db.SaveChangesAsync();

            var document = new Document
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                ExecutionCircuitId = circuitByDocument.Id,
            };
            db.Documents.Add(document);
            await db.SaveChangesAsync();

            var createdAt = new DateTime(2026, 10, 20, 14, 0, 0, DateTimeKind.Utc);
            var byDocument = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = user.Id,
                DocumentId = document.Id,
                Message = $"{ExecutionCircuitPendingPrefix}{circuitByDocument.Name} — يرجى تحديث معلوماتها",
                CreatedAt = createdAt,
            };
            var byName = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = user.Id,
                Message = $"{ExecutionCircuitPendingPrefix}{circuitByName.Name} — يرجى تحديث معلوماتها",
                CreatedAt = createdAt.AddMinutes(1),
            };
            // شاذ: بادئة معلقات قديمة بلا مستند ولا دائرة مطابقة للاسم.
            var orphan = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = user.Id,
                Message = $"{ExecutionCircuitPendingPrefix}دائرة غير مسجلة — يرجى تحديث معلوماتها",
                CreatedAt = createdAt.AddMinutes(2),
            };
            var archived = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = user.Id,
                Message = $"{ExecutionCircuitPendingPrefix}{circuitByName.Name} — يرجى تحديث معلوماتها",
                CreatedAt = createdAt.AddMinutes(3),
                IsArchived = true,
                ArchivedAt = createdAt.AddMinutes(4),
            };
            db.HeadAlerts.AddRange(byDocument, byName, orphan, archived);
            await db.SaveChangesAsync();

            await db.GetService<IMigrator>().MigrateAsync(BackfillMigration);
            db.ChangeTracker.Clear();

            var storedByDocument = await db.HeadAlerts.AsNoTracking()
                .SingleAsync(a => a.Id == byDocument.Id);
            Assert.Equal(HeadAlertKindCatalog.CircuitPendingDocuments, storedByDocument.Kind);
            Assert.Equal(circuitByDocument.Id, storedByDocument.ExecutionCircuitId);
            Assert.Equal(
                $"circuit-pending:{branch.Id}:{circuitByDocument.Id}:recipient:{user.Id}:circuit_pending_documents",
                storedByDocument.DedupeKey);

            var storedByName = await db.HeadAlerts.AsNoTracking()
                .SingleAsync(a => a.Id == byName.Id);
            Assert.Equal(HeadAlertKindCatalog.CircuitPendingDocuments, storedByName.Kind);
            Assert.Equal(circuitByName.Id, storedByName.ExecutionCircuitId);
            Assert.Equal(
                $"circuit-pending:{branch.Id}:{circuitByName.Id}:recipient:{user.Id}:circuit_pending_documents",
                storedByName.DedupeKey);

            var storedOrphan = await db.HeadAlerts.AsNoTracking()
                .SingleAsync(a => a.Id == orphan.Id);
            Assert.Equal(HeadAlertKindCatalog.Legacy, storedOrphan.Kind);
            Assert.Null(storedOrphan.ExecutionCircuitId);
            Assert.Null(storedOrphan.DedupeKey);

            var storedArchived = await db.HeadAlerts.AsNoTracking()
                .SingleAsync(a => a.Id == archived.Id);
            Assert.True(storedArchived.IsArchived);
            Assert.Equal(HeadAlertKindCatalog.Legacy, storedArchived.Kind);
            Assert.Null(storedArchived.ExecutionCircuitId);
            Assert.Null(storedArchived.DedupeKey);

            // تقرير §16 للمعلقات المتبقية بلا مطابقة: الشاذ وحده.
            var reportedIds = await db.Database.SqlQueryRaw<int>(
                """
                SELECT "Id" AS Value
                FROM "HeadAlerts"
                WHERE "Kind" = 'legacy' AND "IsArchived" = 0 AND "TargetType" = 2
                  AND "DelegationId" IS NULL AND "AppealId" IS NULL
                  AND "ReviewLetterId" IS NULL AND "PublicEntityId" IS NULL
                  AND "ExecutionCircuitId" IS NULL
                  AND "Message" LIKE 'أحال لك رئيس القسم ملفات من دائرة %'
                ORDER BY "Id"
                """).ToListAsync();
            Assert.Equal(new[] { orphan.Id }, reportedIds);

            // لا صف بادئة معلقات نشط خارج التصنيف أو التقرير المفسَّر.
            var unexplained = await db.Database.SqlQueryRaw<int>(
                """
                SELECT COUNT(*) AS Value
                FROM "HeadAlerts"
                WHERE "IsArchived" = 0 AND "TargetType" = 2
                  AND "Message" LIKE 'أحال لك رئيس القسم ملفات من دائرة %'
                  AND "Kind" <> 'circuit_pending_documents'
                  AND NOT ("Kind" = 'legacy'
                           AND "DelegationId" IS NULL AND "AppealId" IS NULL
                           AND "ReviewLetterId" IS NULL AND "PublicEntityId" IS NULL
                           AND "ExecutionCircuitId" IS NULL)
                """).SingleAsync();
            Assert.Equal(0, unexplained);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    /// <summary>
    /// §14.1: إعادة بناء SQLite في M2 تحافظ على الصفوف والمستلمين والفهارس
    /// والمفاتيح الأجنبية (مع استبدال فهرس <c>TargetLawyerId</c> المقصود).
    /// </summary>
    [Fact]
    public async Task Migration_Sqlite_RebuildPreservesRowsIndexesAndForeignKeys()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(OutboxMigration);
            var (branch, user) = await SeedBranchAndUserAsync(db);

            var circuit = new ExecutionCircuit
            {
                BranchId = branch.Id,
                Name = "دائرة التنفيذ الأولى",
                NameNorm = "دائرة التنفيذ الاولى",
                CreatedById = user.Id,
            };
            db.ExecutionCircuits.Add(circuit);
            await db.SaveChangesAsync();

            var document = new Document
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
            };
            db.Documents.Add(document);
            await db.SaveChangesAsync();

            var delegation = new DocumentDelegation
            {
                SourceDocumentId = document.Id,
                CreatedById = user.Id,
            };
            db.DocumentDelegations.Add(delegation);
            await db.SaveChangesAsync();

            var createdAt = new DateTime(2026, 10, 20, 15, 0, 0, DateTimeKind.Utc);
            var circuitAlert = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = user.Id,
                ExecutionCircuitId = circuit.Id,
                Kind = HeadAlertKindCatalog.CircuitPendingDocuments,
                DedupeKey =
                    $"circuit-pending:{branch.Id}:{circuit.Id}:recipient:{user.Id}:circuit_pending_documents",
                Message = "أحال لك رئيس القسم ملفات من دائرة التنفيذ الأولى — يرجى تحديث معلوماتها",
                CreatedAt = createdAt,
                LastContentChangedAt = createdAt,
            };
            var mirrorAlert = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = user.Id,
                DocumentId = document.Id,
                DelegationId = delegation.Id,
                Kind = HeadAlertKindCatalog.DelegationMirrorUpdated,
                DedupeKey =
                    $"delegation:{delegation.Id}:recipient:{user.Id}:delegation_mirror_updated",
                Message = "تم تحديث نسخة الملف المناب",
                CreatedAt = createdAt.AddMinutes(1),
                LastContentChangedAt = createdAt.AddMinutes(1),
            };
            var manualAlert = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Branch,
                Kind = HeadAlertKindCatalog.ManualToBranch,
                Message = "تعميم يدوي يبقى كما هو",
                CreatedAt = createdAt.AddMinutes(2),
            };
            db.HeadAlerts.AddRange(circuitAlert, mirrorAlert, manualAlert);
            await db.SaveChangesAsync();

            db.HeadAlertRecipients.AddRange(
                new HeadAlertRecipient
                {
                    HeadAlertId = circuitAlert.Id,
                    UserId = user.Id,
                    IsRead = true,
                    ReadAt = createdAt.AddHours(1),
                },
                new HeadAlertRecipient { HeadAlertId = mirrorAlert.Id, UserId = user.Id });
            await db.SaveChangesAsync();

            var alertsBefore = await db.HeadAlerts.AsNoTracking()
                .OrderBy(a => a.Id)
                .Select(a => new
                {
                    a.Id,
                    a.BranchId,
                    a.CreatedById,
                    a.TargetType,
                    a.Kind,
                    a.DedupeKey,
                    a.ExecutionCircuitId,
                    a.DocumentId,
                    a.TargetLawyerId,
                    a.Message,
                    a.CreatedAt,
                    a.LastContentChangedAt,
                    a.IsArchived,
                    a.ArchivedAt,
                })
                .ToListAsync();
            var recipientsBefore = await db.HeadAlertRecipients.AsNoTracking()
                .OrderBy(r => r.Id)
                .Select(r => new { r.Id, r.HeadAlertId, r.UserId, r.IsRead, r.ReadAt })
                .ToListAsync();
            var indexesBefore = await GetSqliteIndexNamesAsync(db, "HeadAlerts");
            var foreignKeysBefore = await GetSqliteForeignKeyTablesAsync(db, "HeadAlerts");

            await db.GetService<IMigrator>().MigrateAsync(BackfillMigration);
            db.ChangeTracker.Clear();

            var alertsAfter = await db.HeadAlerts.AsNoTracking()
                .OrderBy(a => a.Id)
                .Select(a => new
                {
                    a.Id,
                    a.BranchId,
                    a.CreatedById,
                    a.TargetType,
                    a.Kind,
                    a.DedupeKey,
                    a.ExecutionCircuitId,
                    a.DocumentId,
                    a.TargetLawyerId,
                    a.Message,
                    a.CreatedAt,
                    a.LastContentChangedAt,
                    a.IsArchived,
                    a.ArchivedAt,
                })
                .ToListAsync();
            Assert.Equal(alertsBefore, alertsAfter);

            var recipientsAfter = await db.HeadAlertRecipients.AsNoTracking()
                .OrderBy(r => r.Id)
                .Select(r => new { r.Id, r.HeadAlertId, r.UserId, r.IsRead, r.ReadAt })
                .ToListAsync();
            Assert.Equal(recipientsBefore, recipientsAfter);

            var integrity = await db.Database
                .SqlQueryRaw<string>("SELECT integrity_check AS Value FROM pragma_integrity_check")
                .SingleAsync();
            Assert.Equal("ok", integrity);

            var foreignKeyViolations = await db.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_foreign_key_check")
                .SingleAsync();
            Assert.Equal(0, foreignKeyViolations);

            var indexesAfter = await GetSqliteIndexNamesAsync(db, "HeadAlerts");
            foreach (var index in indexesBefore.Where(i => i != "IX_HeadAlerts_TargetLawyerId"))
                Assert.Contains(index, indexesAfter);
            Assert.DoesNotContain("IX_HeadAlerts_TargetLawyerId", indexesAfter);
            Assert.Contains("IX_HeadAlerts_TargetLawyerId_Kind_IsArchived", indexesAfter);
            Assert.Contains("UX_HeadAlerts_DedupeKey_Active", indexesAfter);

            var foreignKeysAfter = await GetSqliteForeignKeyTablesAsync(db, "HeadAlerts");
            foreach (var table in foreignKeysBefore)
                Assert.Contains(table, foreignKeysAfter);
            Assert.Contains("ExecutionCircuits", foreignKeysAfter);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    /// <summary>
    /// §14.1: بعد إعادة البناء <c>integrity_check</c> = ok و<c>foreign_key_check</c>
    /// بلا صفوف، وبصمة الأعمدة الثابتة مطابقة؛ الصفوف المصنفة سابقًا لا تتغير
    /// قيمها الدلالية (صفوف legacy تتغير عمدًا وفق backfill).
    /// </summary>
    [Fact]
    public async Task Migration_Sqlite_IntegrityAndFingerprint_PostRebuild()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(OutboxMigration);
            var (branch, user) = await SeedBranchAndUserAsync(db);

            var document = new Document
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
            };
            db.Documents.Add(document);
            await db.SaveChangesAsync();

            var delegation = new DocumentDelegation
            {
                SourceDocumentId = document.Id,
                CreatedById = user.Id,
            };
            db.DocumentDelegations.Add(delegation);
            await db.SaveChangesAsync();

            var createdAt = new DateTime(2026, 10, 20, 16, 0, 0, DateTimeKind.Utc);
            var lastChangedAt = createdAt.AddMinutes(5);
            var classified = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = user.Id,
                DelegationId = delegation.Id,
                Kind = HeadAlertKindCatalog.DelegationMirrorUpdated,
                DedupeKey =
                    $"delegation:{delegation.Id}:recipient:{user.Id}:delegation_mirror_updated",
                Message = "تم تحديث نسخة الملف المناب",
                CreatedAt = createdAt,
                LastContentChangedAt = lastChangedAt,
            };
            var legacyCreatedAt = createdAt.AddMinutes(10);
            var archivedCreatedAt = createdAt.AddMinutes(20);
            var legacyId = await InsertLegacyAlertAsync(
                db, branch.Id, user.Id, HeadAlertTargetType.Branch,
                "تنبيه قديم بلا تصنيف", legacyCreatedAt);
            var archivedLegacyId = await InsertLegacyAlertAsync(
                db, branch.Id, user.Id, HeadAlertTargetType.Branch,
                "تنبيه قديم مؤرشف", archivedCreatedAt,
                isArchived: true, archivedAt: createdAt.AddMinutes(21));
            db.HeadAlerts.Add(classified);
            await db.SaveChangesAsync();

            var classifiedIds = new[] { classified.Id };
            var structuralBefore = await StructuralFingerprintAsync(db);
            var semanticBefore = await SemanticFingerprintAsync(db, classifiedIds);

            await db.GetService<IMigrator>().MigrateAsync(BackfillMigration);
            db.ChangeTracker.Clear();

            Assert.Equal(structuralBefore, await StructuralFingerprintAsync(db));
            Assert.Equal(semanticBefore, await SemanticFingerprintAsync(db, classifiedIds));

            var storedLegacy = await db.HeadAlerts.AsNoTracking()
                .SingleAsync(a => a.Id == legacyId);
            Assert.Equal(HeadAlertKindCatalog.Legacy, storedLegacy.Kind);
            Assert.Equal(legacyCreatedAt, storedLegacy.LastContentChangedAt);
            Assert.False(storedLegacy.IsArchived);

            var storedArchived = await db.HeadAlerts.AsNoTracking()
                .SingleAsync(a => a.Id == archivedLegacyId);
            Assert.True(storedArchived.IsArchived);
            Assert.Equal(HeadAlertKindCatalog.Legacy, storedArchived.Kind);
            Assert.Equal(archivedCreatedAt, storedArchived.LastContentChangedAt);

            var integrity = await db.Database
                .SqlQueryRaw<string>("SELECT integrity_check AS Value FROM pragma_integrity_check")
                .SingleAsync();
            Assert.Equal("ok", integrity);

            var foreignKeyViolations = await db.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_foreign_key_check")
                .SingleAsync();
            Assert.Equal(0, foreignKeyViolations);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    /// <summary>
    /// إعادة تنفيذ أوامر البيانات في M2 (لا أوامر المخطط؛ EF يسجّل الهجرة مرة واحدة)
    /// لا تغيّر أي صف أو مستلم — شرط أمان إعادة التشغيل اليدوية الموثقة.
    /// </summary>
    [Fact]
    public async Task Migration_Backfill_RerunIsIdempotent()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(OutboxMigration);
            var (branch, user) = await SeedBranchAndUserAsync(db);

            var document = new Document
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
            };
            db.Documents.Add(document);
            await db.SaveChangesAsync();

            var delegation = new DocumentDelegation
            {
                SourceDocumentId = document.Id,
                CreatedById = user.Id,
            };
            db.DocumentDelegations.Add(delegation);
            await db.SaveChangesAsync();

            var createdAt = new DateTime(2026, 10, 20, 17, 0, 0, DateTimeKind.Utc);
            var legacyClassified = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Head,
                DelegationId = delegation.Id,
                Message = "بانتظار اعتماد الإنابة — سطّر المحامي فلان إنابة",
                CreatedAt = createdAt,
            };
            var legacyPendingOrphan = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = user.Id,
                Message = "أحال لك رئيس القسم ملفات من دائرة غير مسجلة — يرجى تحديث معلوماتها",
                CreatedAt = createdAt.AddMinutes(5),
            };
            var newRow = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                TargetLawyerId = user.Id,
                DelegationId = delegation.Id,
                Kind = HeadAlertKindCatalog.DelegationMirrorUpdated,
                DedupeKey =
                    $"delegation:{delegation.Id}:recipient:{user.Id}:delegation_mirror_updated",
                Message = "تم تحديث نسخة الملف المناب",
                CreatedAt = createdAt.AddMinutes(10),
                LastContentChangedAt = createdAt.AddMinutes(11),
            };
            db.HeadAlerts.AddRange(legacyClassified, legacyPendingOrphan, newRow);
            await db.SaveChangesAsync();

            db.HeadAlertRecipients.Add(new HeadAlertRecipient
            {
                HeadAlertId = legacyClassified.Id,
                UserId = user.Id,
            });
            await db.SaveChangesAsync();

            await db.GetService<IMigrator>().MigrateAsync(BackfillMigration);
            db.ChangeTracker.Clear();

            var before = await FullStateFingerprintAsync(db);

            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
            new RerunnableBackfillMigration().ApplyDataOperations(builder);
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                foreach (var operation in builder.Operations.OfType<SqlOperation>())
                    await db.Database.ExecuteSqlRawAsync(operation.Sql);
                await transaction.CommitAsync();
            }

            db.ChangeTracker.Clear();
            Assert.Equal(before, await FullStateFingerprintAsync(db));
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    /// <summary>
    /// M3 ترفض التشديد إذا بقيت قيم <c>NULL</c> أو نوع فارغ/أبيض — قبل إعادة بناء
    /// الجدول وعبر حارس صريح قابل للتشخيص (الخطة §6.3/§16.2).
    /// </summary>
    [Theory]
    [InlineData("null_kind", "CK_HeadAlertContractGuard_NullKind")]
    [InlineData("null_last_changed", "CK_HeadAlertContractGuard_NullLastChanged")]
    [InlineData("empty_kind", "CK_HeadAlertContractGuard_EmptyKind")]
    [InlineData("whitespace_kind", "CK_HeadAlertContractGuard_EmptyKind")]
    public async Task Migration_Contract_RejectsNullAndEmptyKind(
        string violation, string expectedConstraint)
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(BackfillMigration);
            var (branch, user) = await SeedBranchAndUserAsync(db);
            var createdAt = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
            var alert = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Branch,
                Kind = HeadAlertKindCatalog.Legacy,
                Message = "صف اختبار حارس M3",
                CreatedAt = createdAt,
                LastContentChangedAt = createdAt,
            };
            db.HeadAlerts.Add(alert);
            await db.SaveChangesAsync();
            var alertId = alert.Id;
            db.ChangeTracker.Clear();

            var sql = violation switch
            {
                "null_kind" => "UPDATE \"HeadAlerts\" SET \"Kind\" = NULL",
                "null_last_changed" => "UPDATE \"HeadAlerts\" SET \"LastContentChangedAt\" = NULL",
                "empty_kind" => "UPDATE \"HeadAlerts\" SET \"Kind\" = ''",
                "whitespace_kind" => "UPDATE \"HeadAlerts\" SET \"Kind\" = '   '",
                _ => throw new ArgumentOutOfRangeException(nameof(violation)),
            };
            await db.Database.ExecuteSqlRawAsync(sql);

            var exception = await Assert.ThrowsAnyAsync<Exception>(
                () => db.GetService<IMigrator>().MigrateAsync(ContractMigration));

            // رسالة التشخيص تحمل اسم القيد لا خطأ NULL عامًا — فيُعرف أي فحص فشل.
            Assert.Contains(expectedConstraint, exception.ToString(), StringComparison.Ordinal);

            var applied = await db.Database
                .SqlQueryRaw<string>("SELECT \"MigrationId\" AS Value FROM \"__EFMigrationsHistory\"")
                .ToListAsync();
            Assert.DoesNotContain(ContractMigration, applied);

            // الفشل يترك المخطط والبيانات كما كانا: لا NOT NULL ولا قيد، ولا جدول
            // إعادة بناء مؤقت، والقيم المخالفة نفسها محفوظة (لا تحويل صامت).
            var kindNotNull = await db.Database.SqlQueryRaw<int>(
                "SELECT \"notnull\" AS Value FROM pragma_table_info('HeadAlerts') WHERE name = 'Kind'").SingleAsync();
            var lastChangedNotNull = await db.Database.SqlQueryRaw<int>(
                "SELECT \"notnull\" AS Value FROM pragma_table_info('HeadAlerts') WHERE name = 'LastContentChangedAt'").SingleAsync();
            Assert.Equal(0, kindNotNull);
            Assert.Equal(0, lastChangedNotNull);

            var tableSql = await db.Database
                .SqlQueryRaw<string>("SELECT sql AS Value FROM sqlite_master WHERE type = 'table' AND name = 'HeadAlerts'")
                .SingleAsync();
            Assert.DoesNotContain("CK_HeadAlerts_Kind_NotEmpty", tableSql, StringComparison.Ordinal);

            var tempTables = await db.Database
                .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table' AND name = 'ef_temp_HeadAlerts'")
                .ToListAsync();
            Assert.Empty(tempTables);

            var storedKind = await db.Database.SqlQueryRaw<string?>(
                "SELECT \"Kind\" AS Value FROM \"HeadAlerts\" WHERE \"Id\" = {0}", alertId).SingleAsync();
            var storedLastChanged = await db.Database.SqlQueryRaw<DateTime?>(
                "SELECT \"LastContentChangedAt\" AS Value FROM \"HeadAlerts\" WHERE \"Id\" = {0}", alertId).SingleAsync();
            var expectedKind = violation switch
            {
                "null_kind" => null,
                "empty_kind" => string.Empty,
                "whitespace_kind" => "   ",
                _ => HeadAlertKindCatalog.Legacy,
            };
            var expectedLastChanged = violation == "null_last_changed" ? null : (DateTime?)createdAt;
            Assert.Equal(expectedKind, storedKind);
            Assert.Equal(expectedLastChanged, storedLastChanged);

            var storedMessage = await db.Database.SqlQueryRaw<string>(
                "SELECT \"Message\" AS Value FROM \"HeadAlerts\" WHERE \"Id\" = {0}", alertId).SingleAsync();
            Assert.Equal("صف اختبار حارس M3", storedMessage);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    /// <summary>
    /// بعد M3: العمودان NOT NULL بلا Default دائم، والقيد يرفض النوع الفارغ/الأبيض،
    /// مع بقاء الفهارس (ومنها الفريد الجزئي) والمفاتيح الأجنبية سليمة.
    /// </summary>
    [Fact]
    public async Task Migration_Contract_EnforcesNonNullAndPreservesSchema()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(BackfillMigration);
            var (branch, user) = await SeedBranchAndUserAsync(db);
            var createdAt = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
            db.HeadAlerts.Add(new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Branch,
                Kind = HeadAlertKindCatalog.Legacy,
                Message = "صف مشروع قبل M3",
                CreatedAt = createdAt,
                LastContentChangedAt = createdAt,
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var indexesBefore = await GetSqliteIndexNamesAsync(db, "HeadAlerts");
            var foreignKeyTablesBefore = await GetSqliteForeignKeyTablesAsync(db, "HeadAlerts");

            await db.GetService<IMigrator>().MigrateAsync(ContractMigration);

            var kindNotNull = await db.Database.SqlQueryRaw<int>(
                "SELECT \"notnull\" AS Value FROM pragma_table_info('HeadAlerts') WHERE name = 'Kind'").SingleAsync();
            var lastChangedNotNull = await db.Database.SqlQueryRaw<int>(
                "SELECT \"notnull\" AS Value FROM pragma_table_info('HeadAlerts') WHERE name = 'LastContentChangedAt'").SingleAsync();
            var kindDefault = await db.Database.SqlQueryRaw<string?>(
                "SELECT dflt_value AS Value FROM pragma_table_info('HeadAlerts') WHERE name = 'Kind'").SingleAsync();
            var lastChangedDefault = await db.Database.SqlQueryRaw<string?>(
                "SELECT dflt_value AS Value FROM pragma_table_info('HeadAlerts') WHERE name = 'LastContentChangedAt'").SingleAsync();

            Assert.Equal(1, kindNotNull);
            Assert.Equal(1, lastChangedNotNull);
            Assert.Null(kindDefault);
            Assert.Null(lastChangedDefault);

            var indexes = await GetSqliteIndexNamesAsync(db, "HeadAlerts");
            Assert.Contains("UX_HeadAlerts_DedupeKey_Active", indexes);
            Assert.Contains("IX_HeadAlerts_Kind_Active_LastChanged", indexes);
            Assert.Contains("IX_HeadAlerts_ExecutionCircuitId_Kind_IsArchived", indexes);
            // لا يكفي وجود ثلاثة أسماء: إعادة البناء يجب ألا تُسقط أي فهرس من M2.
            Assert.Equal(indexesBefore, indexes);

            // الفهرسة الجزئية ليست مجرد اسم: الفلتر نفسه يجب أن يبقى بعد إعادة البناء.
            var dedupeIndexSql = await db.Database.SqlQueryRaw<string>(
                "SELECT sql AS Value FROM sqlite_master WHERE type = 'index' AND name = 'UX_HeadAlerts_DedupeKey_Active'").SingleAsync();
            Assert.Contains("\"DedupeKey\" IS NOT NULL", dedupeIndexSql, StringComparison.Ordinal);
            Assert.Contains("\"IsArchived\" = 0", dedupeIndexSql, StringComparison.Ordinal);

            var foreignKeyTables = await GetSqliteForeignKeyTablesAsync(db, "HeadAlerts");
            Assert.Contains("Branches", foreignKeyTables);
            Assert.Contains("ExecutionCircuits", foreignKeyTables);
            Assert.Equal(foreignKeyTablesBefore, foreignKeyTables);

            var integrity = await db.Database
                .SqlQueryRaw<string>("SELECT integrity_check AS Value FROM pragma_integrity_check()")
                .SingleAsync();
            Assert.Equal("ok", integrity);
            var foreignKeyViolations = await db.Database
                .SqlQueryRaw<string>("SELECT \"table\" AS Value FROM pragma_foreign_key_check()")
                .ToListAsync();
            Assert.Empty(foreignKeyViolations);

            // القيد يرفض الفراغ والأبيض، وقاعدة NOT NULL ترفض NULL.
            foreach (var invalidKind in new[] { "", "   " })
            {
                await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO \"HeadAlerts\" (\"BranchId\", \"CreatedById\", \"TargetType\", \"Kind\", \"Message\", \"CreatedAt\", \"LastContentChangedAt\") VALUES ({0}, {1}, 3, {2}, 'x', '2026-10-20 00:00:00', '2026-10-20 00:00:00')",
                    branch.Id, user.Id, invalidKind));
            }

            await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync(
                "UPDATE \"HeadAlerts\" SET \"Kind\" = NULL WHERE \"Id\" = {0}", 1));
            await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync(
                "UPDATE \"HeadAlerts\" SET \"LastContentChangedAt\" = NULL WHERE \"Id\" = {0}", 1));

            // مسار الكيان أيضًا: القيمة الافتراضية string.Empty لا تمرّ من القيد.
            db.HeadAlerts.Add(new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Branch,
                Kind = string.Empty,
                Message = "صف بنوع فارغ",
                CreatedAt = createdAt.AddMinutes(2),
                LastContentChangedAt = createdAt.AddMinutes(2),
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();

            // الكتابة الصحيحة تمر، والصف القديم محفوظ بنصه ونوعه.
            db.HeadAlerts.Add(new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Branch,
                Kind = HeadAlertKindCatalog.Legacy,
                Message = "صف بعد M3",
                CreatedAt = createdAt.AddMinutes(1),
                LastContentChangedAt = createdAt.AddMinutes(1),
            });
            await db.SaveChangesAsync();
            Assert.Equal(2, await db.HeadAlerts.CountAsync());
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    /// <summary>
    /// الـDown يعيد العمودين nullable ويسقط القيد فقط، مع الحفاظ على البيانات
    /// (لا تحويل قيم إلى NULL ولا حذف صفوف — الخطة §16.2).
    /// </summary>
    [Fact]
    public async Task Migration_Contract_Down_RestoresNullableColumns()
    {
        var (path, db) = CreateDb();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(BackfillMigration);
            var (branch, user) = await SeedBranchAndUserAsync(db);
            var createdAt = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
            db.HeadAlerts.Add(new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                TargetType = HeadAlertTargetType.Branch,
                Kind = HeadAlertKindCatalog.Legacy,
                Message = "صف يبقى بعد Down",
                CreatedAt = createdAt,
                LastContentChangedAt = createdAt,
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            await db.GetService<IMigrator>().MigrateAsync(ContractMigration);
            await db.GetService<IMigrator>().MigrateAsync(BackfillMigration);
            db.ChangeTracker.Clear();

            var kindNotNull = await db.Database.SqlQueryRaw<int>(
                "SELECT \"notnull\" AS Value FROM pragma_table_info('HeadAlerts') WHERE name = 'Kind'").SingleAsync();
            var lastChangedNotNull = await db.Database.SqlQueryRaw<int>(
                "SELECT \"notnull\" AS Value FROM pragma_table_info('HeadAlerts') WHERE name = 'LastContentChangedAt'").SingleAsync();
            Assert.Equal(0, kindNotNull);
            Assert.Equal(0, lastChangedNotNull);

            var tableSql = await db.Database
                .SqlQueryRaw<string>("SELECT sql AS Value FROM sqlite_master WHERE type = 'table' AND name = 'HeadAlerts'")
                .SingleAsync();
            Assert.DoesNotContain("CK_HeadAlerts_Kind_NotEmpty", tableSql, StringComparison.Ordinal);

            var stored = await db.HeadAlerts.AsNoTracking().SingleAsync();
            Assert.Equal(HeadAlertKindCatalog.Legacy, stored.Kind);
            Assert.Equal(createdAt, stored.LastContentChangedAt);
            Assert.Equal("صف يبقى بعد Down", stored.Message);
        }
        finally
        {
            await db.DisposeAsync();
            Cleanup(path);
        }
    }

    /// <summary>تنفيذ أوامر بيانات M2 وحدها دون أوامر المخطط — لاختبار إعادة التشغيل.</summary>
    private sealed class RerunnableBackfillMigration : HeadAlertTaxonomy_Backfill
    {
        public void ApplyDataOperations(MigrationBuilder builder) => Up(builder);
    }

    private static async Task<List<string>> GetSqliteIndexNamesAsync(
        DocGeneratorDbContext db, string table)
        => await db.Database
            .SqlQueryRaw<string>(
                """
                SELECT name AS Value
                FROM sqlite_master
                WHERE type = 'index' AND tbl_name = {0}
                  AND name NOT LIKE 'sqlite_autoindex_%'
                ORDER BY name
                """,
                table)
            .ToListAsync();

    private static async Task<List<string>> GetSqliteForeignKeyTablesAsync(
        DocGeneratorDbContext db, string table)
        => await db.Database
            .SqlQueryRaw<string>(
                "SELECT \"table\" AS Value FROM pragma_foreign_key_list({0}) ORDER BY \"table\"",
                table)
            .ToListAsync();

    private static async Task<string> StructuralFingerprintAsync(DocGeneratorDbContext db)
    {
        var rows = await db.HeadAlerts.AsNoTracking()
            .OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.BranchId, a.CreatedAt })
            .ToListAsync();

        var lines = rows.Select(row => string.Create(
            CultureInfo.InvariantCulture,
            $"alert|{row.Id}|{row.BranchId}|{row.CreatedAt:O}"));
        return Sha256(string.Join('\n', lines));
    }

    private static async Task<string> SemanticFingerprintAsync(
        DocGeneratorDbContext db, int[] alertIds)
    {
        var rows = await db.HeadAlerts.AsNoTracking()
            .Where(a => alertIds.Contains(a.Id))
            .OrderBy(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.BranchId,
                a.Kind,
                a.DedupeKey,
                a.CreatedAt,
                a.LastContentChangedAt,
            })
            .ToListAsync();

        var lines = rows.Select(row => string.Create(
            CultureInfo.InvariantCulture,
            $"alert|{row.Id}|{row.BranchId}|{row.Kind}|{row.DedupeKey ?? "<null>"}|{row.CreatedAt:O}|{Format(row.LastContentChangedAt)}"));
        return Sha256(string.Join('\n', lines));
    }

    private static async Task<string> FullStateFingerprintAsync(DocGeneratorDbContext db)
    {
        var alerts = await db.HeadAlerts.AsNoTracking()
            .OrderBy(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.BranchId,
                a.CreatedById,
                a.TargetType,
                a.Kind,
                a.DedupeKey,
                a.ExecutionCircuitId,
                a.DocumentId,
                a.TargetLawyerId,
                a.DelegationId,
                a.AppealId,
                a.ReviewLetterId,
                a.PublicEntityId,
                a.Message,
                a.CreatedAt,
                a.LastContentChangedAt,
                a.IsArchived,
                a.ArchivedAt,
            })
            .ToListAsync();
        var recipients = await db.HeadAlertRecipients.AsNoTracking()
            .OrderBy(r => r.Id)
            .Select(r => new { r.Id, r.HeadAlertId, r.UserId, r.IsRead, r.ReadAt })
            .ToListAsync();

        static string Number(int? value)
            => value?.ToString(CultureInfo.InvariantCulture) ?? "<null>";

        var lines = new List<string>(alerts.Count + recipients.Count);
        foreach (var alert in alerts)
        {
            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"alert|{alert.Id}|{alert.BranchId}|{alert.CreatedById}|{(int)alert.TargetType}|{alert.Kind}|{alert.DedupeKey ?? "<null>"}|{Number(alert.ExecutionCircuitId)}|{Number(alert.DocumentId)}|{Number(alert.TargetLawyerId)}|{Number(alert.DelegationId)}|{Number(alert.AppealId)}|{Number(alert.ReviewLetterId)}|{Number(alert.PublicEntityId)}|{alert.Message}|{alert.CreatedAt:O}|{Format(alert.LastContentChangedAt)}|{alert.IsArchived}|{Format(alert.ArchivedAt)}"));
        }

        foreach (var recipient in recipients)
        {
            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"recipient|{recipient.Id}|{recipient.HeadAlertId}|{recipient.UserId}|{recipient.IsRead}|{Format(recipient.ReadAt)}"));
        }

        return string.Join('\n', lines);
    }

    private static string Format(DateTime? value)
        => value?.ToString("O", CultureInfo.InvariantCulture) ?? "<null>";

    private static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private const string ExecutionCircuitPendingPrefix = "أحال لك رئيس القسم ملفات من دائرة ";

    /// <summary>
    /// يُدرج صفًا بالشكل القديم عبر SQL خام (بلا <c>Kind</c>/<c>LastContentChangedAt</c>)
    /// لاختبار مرحلة الـbackfill على قاعدة M1، لأن الكيان الحالي بعد M3 لا يمثّل NULL.
    /// </summary>
    private static async Task<int> InsertLegacyAlertAsync(
        DocGeneratorDbContext db,
        int branchId,
        int createdById,
        HeadAlertTargetType targetType,
        string message,
        DateTime createdAt,
        int? documentId = null,
        int? targetLawyerId = null,
        int? delegationId = null,
        int? appealId = null,
        int? reviewLetterId = null,
        int? publicEntityId = null,
        int? executionCircuitId = null,
        bool isArchived = false,
        DateTime? archivedAt = null)
    {
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO "HeadAlerts"
                    ("BranchId", "CreatedById", "TargetType", "Message", "CreatedAt",
                     "DocumentId", "TargetLawyerId", "DelegationId", "AppealId",
                     "ReviewLetterId", "PublicEntityId", "ExecutionCircuitId",
                     "IsArchived", "ArchivedAt")
                VALUES ($branchId, $createdById, $targetType, $message, $createdAt,
                        $documentId, $targetLawyerId, $delegationId, $appealId,
                        $reviewLetterId, $publicEntityId, $executionCircuitId,
                        $isArchived, $archivedAt);
                """;

            command.Parameters.AddWithValue("$branchId", branchId);
            command.Parameters.AddWithValue("$createdById", createdById);
            command.Parameters.AddWithValue("$targetType", (int)targetType);
            command.Parameters.AddWithValue("$message", message);
            command.Parameters.AddWithValue("$createdAt", createdAt.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$documentId", (object?)documentId ?? DBNull.Value);
            command.Parameters.AddWithValue("$targetLawyerId", (object?)targetLawyerId ?? DBNull.Value);
            command.Parameters.AddWithValue("$delegationId", (object?)delegationId ?? DBNull.Value);
            command.Parameters.AddWithValue("$appealId", (object?)appealId ?? DBNull.Value);
            command.Parameters.AddWithValue("$reviewLetterId", (object?)reviewLetterId ?? DBNull.Value);
            command.Parameters.AddWithValue("$publicEntityId", (object?)publicEntityId ?? DBNull.Value);
            command.Parameters.AddWithValue("$executionCircuitId", (object?)executionCircuitId ?? DBNull.Value);
            command.Parameters.AddWithValue("$isArchived", isArchived ? 1 : 0);
            command.Parameters.AddWithValue("$archivedAt", archivedAt is null
                ? DBNull.Value
                : archivedAt.Value.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync();

            await using var idCommand = connection.CreateCommand();
            idCommand.CommandText = "SELECT last_insert_rowid();";
            var id = await idCommand.ExecuteScalarAsync();
            return Convert.ToInt32(id, CultureInfo.InvariantCulture);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<(Branch Branch, User User)> SeedBranchAndUserAsync(DocGeneratorDbContext db)
    {
        var branch = new Branch { Name = "دمشق", Code = "DAM-" + Guid.NewGuid().ToString("N")[..8] };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var user = new User
        {
            Username = "lawyer-" + Guid.NewGuid().ToString("N")[..8],
            FullName = "المحامي فلان",
            Role = UserRole.Lawyer,
            BranchId = branch.Id,
            PasswordHash = "x",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (branch, user);
    }
}
