using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using DocGenerator.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات المراجعة النقدية لخطة `docs/timezone-fix-plan.md` (§5 وحقول H1/H3/M2):
/// المحوّل المركزي <c>UtcDateTimeConverter</c> — ذهاب/عودة الـ <c>Kind</c> على
/// <c>DateTime</c> و <c>DateTime?</c>، تسلسل السلك بـ <c>Z</c>، إثبات «بلا هجرة»
/// (<c>HasPendingModelChanges</c>) للسياقين، ومسار <c>DbLoginRateLimiter</c> الخام.
/// </summary>
public class UtcDateTimeConverterTests
{
    private static readonly DateTime Origin = new(2026, 9, 26, 12, 34, 56, DateTimeKind.Utc);

    [Fact]
    public async Task UtcTimestamp_RoundTrips_WithKindUtc()
    {
        using var db = TestDb.Create();
        db.AuditLogs.Add(new AuditLog { UserName = "u", Timestamp = Origin });
        await db.SaveChangesAsync();

        var reread = await db.AuditLogs.AsNoTracking().SingleAsync();

        Assert.Equal(DateTimeKind.Utc, reread.Timestamp.Kind);
        Assert.Equal(Origin, reread.Timestamp);
    }

    [Fact]
    public async Task LocalTimestamp_IsStoredAsUtc()
    {
        using var db = TestDb.Create();
        // قيمة Local بفارق +3 (دمشق): يجب أن تُخزَّن وتُقرأ UTC محوَّلة.
        var local = new DateTime(2026, 9, 26, 15, 34, 56, DateTimeKind.Local);
        db.Branches.Add(new Branch { Name = "دمشق", Code = "DAM", CreatedAt = local });
        await db.SaveChangesAsync();

        var reread = await db.Branches.AsNoTracking().SingleAsync();

        Assert.Equal(DateTimeKind.Utc, reread.CreatedAt.Kind);
        Assert.Equal(local.ToUniversalTime(), reread.CreatedAt);
    }

    [Fact]
    public async Task UnspecifiedTimestamp_IsInterpretedAsUtc()
    {
        using var db = TestDb.Create();
        // القيمة المخزّنة تاريخيًا في SQLite (Kind=Unspecified) تُسمّى Utc دون تحويل Ticks.
        var unspecified = new DateTime(2026, 9, 26, 12, 34, 56, DateTimeKind.Unspecified);
        db.AuditLogs.Add(new AuditLog { UserName = "u", Timestamp = unspecified });
        await db.SaveChangesAsync();

        var reread = await db.AuditLogs.AsNoTracking().SingleAsync();

        Assert.Equal(DateTimeKind.Utc, reread.Timestamp.Kind);
        Assert.Equal(Origin, reread.Timestamp);
    }

    [Fact]
    public async Task NullableDateTime_RoundTrips_WithKindUtc()
    {
        using var db = TestDb.Create();
        var branch = db.Branches.Add(new Branch { Name = "دمشق", Code = "DAM" }).Entity;
        await db.SaveChangesAsync();
        db.Users.Add(new User
        {
            Username = "nullable_ts",
            FullName = "مستخدم الطابع الفارغ",
            Role = UserRole.Lawyer,
            BranchId = branch.Id,
            PasswordHash = "x",
            LastLogin = Origin,
        });
        await db.SaveChangesAsync();

        var reread = await db.Users.AsNoTracking().SingleAsync(u => u.Username == "nullable_ts");

        Assert.NotNull(reread.LastLogin);
        Assert.Equal(DateTimeKind.Utc, reread.LastLogin!.Value.Kind);
        Assert.Equal(Origin, reread.LastLogin);
    }

    [Fact]
    public async Task NullableDateTime_NullValue_StaysNull()
    {
        using var db = TestDb.Create();
        var branch = db.Branches.Add(new Branch { Name = "دمشق", Code = "DAM" }).Entity;
        await db.SaveChangesAsync();
        db.Users.Add(new User
        {
            Username = "null_ts",
            FullName = "بدون دخول",
            Role = UserRole.Lawyer,
            BranchId = branch.Id,
            PasswordHash = "x",
            LastLogin = null,
        });
        await db.SaveChangesAsync();

        var reread = await db.Users.AsNoTracking().SingleAsync(u => u.Username == "null_ts");

        Assert.Null(reread.LastLogin);
    }

    [Fact]
    public async Task ReadTimestamp_SerializesWithExplicitUtcOffset()
    {
        using var db = TestDb.Create();
        db.AuditLogs.Add(new AuditLog { UserName = "u", Timestamp = Origin });
        await db.SaveChangesAsync();

        var reread = await db.AuditLogs.AsNoTracking().SingleAsync();
        var json = JsonSerializer.Serialize(reread.Timestamp);

        Assert.Equal("\"2026-09-26T12:34:56Z\"", json);
    }

    [Fact]
    public void SqliteModel_HasNoPendingMigrations()
    {
        using var db = TestDb.Create();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void PostgresModel_HasNoPendingMigrations()
    {
        var options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=docgen_dummy;Username=dummy;Password=dummy")
            .Options;
        using var db = new DocGeneratorPostgresDbContext(options);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task DbLoginRateLimiter_RawSqlWorks_WithConverterRegistered()
    {
        using var db = TestDb.Create();
        var limiter = new DbLoginRateLimiter(
            db,
            Options.Create(new RateLimitOptions { MaxLoginAttempts = 2, WindowMinutes = 5 }));

        Assert.True(await limiter.IsAllowedAsync("key|u", default));
        Assert.True(await limiter.TryRecordFailureAsync("key|u", default));
        Assert.True(await limiter.TryRecordFailureAsync("key|u", default));

        // الحد 2: الثالثة تُرفض وحدود المقارنة (معاملات raw SQL) سليمة الـ Kind.
        Assert.False(await limiter.IsAllowedAsync("key|u", default));
        Assert.False(await limiter.TryRecordFailureAsync("key|u", default));

        var rows = await db.LoginAttempts.AsNoTracking().Where(a => a.Key == "key|u").ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(DateTimeKind.Utc, r.AttemptedAtUtc.Kind));
    }

    [Fact]
    public async Task Postgres_RoundTrip_PreservesUtcKind()
    {
        // بوابة حية (M1): يلزم خادم Postgres حقيقي؛ تُشغَّل فقط عند تعريف:
        //   DOCGEN_TEST_POSTGRES="Host=...;Port=5432;Database=...;Username=...;Password=..."
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

            // Unspecified تقويمية + لحظة Utc: كلتاهما تُحفظان وتُقرآن بـ Kind=Utc دون تغيير Ticks.
            var utc = new DateTime(2026, 9, 26, 12, 34, 56, DateTimeKind.Utc);
            db.Branches.Add(new Branch { Name = "بوابة الزمن", Code = "PG", CreatedAt = utc });
            await db.SaveChangesAsync();

            var reread = await db.Branches.AsNoTracking().SingleAsync();
            Assert.Equal(DateTimeKind.Utc, reread.CreatedAt.Kind);
            Assert.Equal(utc, reread.CreatedAt);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}