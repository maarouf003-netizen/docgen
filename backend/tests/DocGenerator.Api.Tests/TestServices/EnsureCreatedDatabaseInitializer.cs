using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests.TestServices;

/// <summary>
/// حيز اختبار فقط: يضمن مخطط SQLite عبر <c>EnsureCreated</c> قبل تهيئة البذور/R9.
/// بعد P0-02 لا توجد هجرات SQLite؛ لذلك <c>MigrateAsync</c> داخل المُهيّئ الأصلي
/// لا يفعل شيئًا، و<see cref="DocGeneratorDbContext.Database"/> يُنشأ من النموذج.
/// </summary>
public sealed class EnsureCreatedDatabaseInitializer : IDatabaseInitializer
{
    private readonly DocGeneratorDbContext _db;
    private readonly DatabaseInitializer _inner;

    public EnsureCreatedDatabaseInitializer(
        DocGeneratorDbContext db,
        IPasswordHasher hasher,
        IOptions<DatabaseOptions> options,
        IHostEnvironment environment)
    {
        _db = db;
        _inner = new DatabaseInitializer(db, hasher, options, environment);
    }

    public Task InitializeAsync(
        bool development,
        string? bootstrapAdminPassword,
        CancellationToken ct = default,
        string? devSeedPassword = null)
    {
        _db.Database.EnsureCreated();
        EnsureAppendOnlyTriggers();
        return _inner.InitializeAsync(development, bootstrapAdminPassword, ct, devSeedPassword);
    }

    /// <summary>
    /// RF-019 (SEC-008): مشغّلات SQLite التي كانت تنشئها هجرة محذوفة. تُعاد هنا
    /// لحيز الاختبار فقط حتى يبقى اختبار إلحاق-فقط على السجل ساريًا مع EnsureCreated.
    /// </summary>
    private void EnsureAppendOnlyTriggers()
    {
        const string sql = """
            CREATE TRIGGER IF NOT EXISTS TRG_AuditLogs_NoUpdate
                BEFORE UPDATE ON AuditLogs
                BEGIN SELECT RAISE(ABORT, 'سجل التدقيق إلحاق-فقط: ممنوع التعديل'); END;
            CREATE TRIGGER IF NOT EXISTS TRG_AuditLogs_NoDelete
                BEFORE DELETE ON AuditLogs
                BEGIN SELECT RAISE(ABORT, 'سجل التدقيق إلحاق-فقط: ممنوع الحذف'); END;
            CREATE TRIGGER IF NOT EXISTS TRG_DocumentFieldChanges_NoUpdate
                BEFORE UPDATE ON DocumentFieldChanges
                BEGIN SELECT RAISE(ABORT, 'سجل التدقيق إلحاق-فقط: ممنوع التعديل'); END;
            CREATE TRIGGER IF NOT EXISTS TRG_DocumentFieldChanges_NoDelete
                BEFORE DELETE ON DocumentFieldChanges
                BEGIN SELECT RAISE(ABORT, 'سجل التدقيق إلحاق-فقط: ممنوع الحذف'); END;
            """;
        _db.Database.ExecuteSqlRaw(sql);
    }
}
