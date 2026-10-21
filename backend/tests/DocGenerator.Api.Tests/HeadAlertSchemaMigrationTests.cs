using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

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

            // كتابة بالشكل القديم: بلا Kind وبلا LastContentChangedAt وبلا DedupeKey.
            var alert = new HeadAlert
            {
                BranchId = branch.Id,
                CreatedById = creator.Id,
                TargetType = HeadAlertTargetType.Branch,
                Message = "تنبيه قديم بلا نوع",
                CreatedAt = DateTime.UtcNow,
            };
            db.HeadAlerts.Add(alert);
            await db.SaveChangesAsync();

            var stored = await db.HeadAlerts.SingleAsync();
            Assert.Null(stored.Kind);
            Assert.Null(stored.LastContentChangedAt);
            Assert.Null(stored.DedupeKey);
            Assert.False(stored.IsArchived);
            Assert.Null(stored.ArchivedAt);

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

            var stored = await db.HeadAlerts.AsNoTracking().SingleAsync();
            Assert.Equal(longMessage, stored.Message);
            Assert.Null(stored.Kind);
            Assert.Null(stored.LastContentChangedAt);
            Assert.Null(stored.DedupeKey);
            Assert.False(stored.IsArchived);
            Assert.Null(stored.ArchivedAt);
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
}
