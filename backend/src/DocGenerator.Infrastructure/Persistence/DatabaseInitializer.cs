using System.Data;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// تنفيذ تهيئة قاعدة البيانات عبر كيان EF الفعلي: يطبّق المهاجرات (حسب
/// <see cref="DatabaseOptions.AutoMigrate"/>), ثم يسند البذر إلى <see cref="DbSeeder"/>
/// حسب البيئة (تطوير/إنتاج).
/// </summary>
public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly DocGeneratorDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly DatabaseOptions _options;
    private readonly bool _strictSchemaChecks;

    public DatabaseInitializer(
        DocGeneratorDbContext db,
        IPasswordHasher hasher,
        IOptions<DatabaseOptions> options,
        IHostEnvironment? environment = null)
    {
        _db = db;
        _hasher = hasher;
        _options = options.Value;
        // الفحوص قاطعة في الإنتاج فقط: قاعدة الاختبار/التطوير مؤقتة، وفشلُها
        // يُسقط الجولة كاملة بلا فائدة. الافتراضي = قاطع (سلامة البيانات أولًا).
        _strictSchemaChecks = environment is null || !environment.IsDevelopment();
    }

    public async Task InitializeAsync(
        bool development,
        string? bootstrapAdminPassword,
        CancellationToken ct = default,
        string? devSeedPassword = null)
    {
        // H-02: لا قفل استشاري مخصص هنا. `MigrateAsync` في `EF Core 9+` يؤمّن
        // تنفيذ الهجرات بقفل على مستوى قاعدة البيانات، والقفل المخصص حوله
        // تكرار يضيف سطح فشل دون حماية إضافية. النسخة الاحتياطية قبل النشر
        // تبقى إلزامية لأن الهجرة نفسها قد تسقط.
        if (_options.AutoMigrate)
            await MigrateAsync(ct);
        else
            await EnsureSchemaIsCurrentAsync(ct);

        if (development)
            await DbSeeder.SeedAsync(_db, _hasher, ct, devSeedPassword);
        else
            await DbSeeder.BootstrapAsync(_db, _hasher, bootstrapAdminPassword, ct);
    }

    /// <summary>
    /// يطبّق الهجرات. في SQLite تُعطَّل المفاتيح الأجنبية قبل الترحيل
    /// (<c>PRAGMA foreign_keys</c> لا يعمل داخل معاملة <c>Up</c> ويُتجاهل صامتًا)،
    /// ثم تُعاد وتُفحص سلامة المفاتيح والصورة بعد الانتهاء (الخطة §6.3).
    /// </summary>
    private async Task MigrateAsync(CancellationToken ct)
    {
        if (!_db.Database.IsSqlite())
        {
            await _db.Database.MigrateAsync(ct);
            return;
        }

        var connection = _db.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen)
            await connection.OpenAsync(ct);

        try
        {
            await ExecutePragmaAsync(connection, "PRAGMA foreign_keys=OFF;", ct);
            await _db.Database.MigrateAsync(ct);
            await ExecutePragmaAsync(connection, "PRAGMA foreign_keys=ON;", ct);

            var violations = await ReadPragmaRowsAsync(connection, "PRAGMA foreign_key_check;", ct);
            var integrity = await ReadPragmaRowsAsync(connection, "PRAGMA integrity_check;", ct);
            var integrityOk = integrity.Count == 1 && string.Equals(integrity[0], "ok", StringComparison.OrdinalIgnoreCase);

            // تشخيص ظاهر في سجلّ CI: أي مخالفة تُطبع بدل أن تختفي خلف فشل مبهم.
            Console.Out.WriteLine(
                $"[schema-check] autoMigrate=true foreign_key_violations={violations.Count} integrity={string.Join('|', integrity)}");

            // في الإنتاج: الفحص قاطع (سلامة البيانات ليست قابلة للتفاوض). في التطوير
            // والاختبار: تحذير فقط — قاعدة الاختبار مؤقتة، وفشلها يُسقط الجولة كاملة
            // بلا فائدة تشخيصية.
            if (!integrityOk)
            {
                var message = "فحص سلامة قاعدة البيانات (integrity_check) فشل بعد الترحيل.";
                if (_strictSchemaChecks)
                    throw new InvalidOperationException(message);
                Console.Error.WriteLine("[schema-check] WARNING: " + message);
            }

            if (violations.Count > 0)
            {
                var message = $"فحص سلامة المفاتيح الأجنبية فشل بعد الترحيل ({violations.Count} مخالفة).";
                if (_strictSchemaChecks)
                    throw new InvalidOperationException(message);
                Console.Error.WriteLine("[schema-check] WARNING: " + message);
            }
        }
        finally
        {
            // تُعاد المفاتيح الأجنبية إلى حالتها الأولى عند الفشل حتى لا تُترك معطلة:
            // إن كان الاتصال مفتوحًا مسبقًا فنعيد تفعيلها عليه صراحةً (إغلاق الاتصال
            // يكفي في الحالة الأخرى لأن الإعداد على مستوى الاتصال).
            if (wasOpen)
            {
                try { await ExecutePragmaAsync(connection, "PRAGMA foreign_keys=ON;", CancellationToken.None); }
                catch { /* best effort: لا يُفشِل الإقلاع بسبب إعادة التفعيل */ }
            }
            else
            {
                await connection.CloseAsync();
            }
        }
    }

    /// <summary>
    /// في الإنتاج (الترحيل التلقائي معطَّل) لا تُطبَّق الهجرات تلقائيًا؛ يُتحقق أن
    /// المخطط مطابق لنسخة التطبيق، وإلا يُرفض الإقلاع برسالة صريحة تحمل أوامر التطبيق —
    /// فلا يبدأ التطبيق على مخطط ناقص ( «no such column» / «no such table» ).
    /// </summary>
    private async Task EnsureSchemaIsCurrentAsync(CancellationToken ct)
    {
        var pending = (await _db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count == 0)
            return;

        throw new InvalidOperationException(
            "الترحيل التلقائي معطَّل (Database:AutoMigrate=false) والمخطط غير مطابق لنسخة التطبيق. "
            + "طبّق الهجرات صراحةً قبل تشغيل النسخة، ثم أعد التشغيل. الهجرات المعلّقة: "
            + string.Join(", ", pending)
            + ". أوامر التطبيق: dotnet ef database update --context DocGeneratorDbContext "
            + "dotnet ef database update --context DocGeneratorPostgresDbContext");
    }

    private static async Task ExecutePragmaAsync(
        System.Data.Common.DbConnection connection,
        string commandText,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<string>> ReadPragmaRowsAsync(
        System.Data.Common.DbConnection connection,
        string commandText,
        CancellationToken ct)
    {
        var results = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(reader.IsDBNull(0) ? string.Empty : reader.GetValue(0)?.ToString() ?? string.Empty);
        return results;
    }
}
