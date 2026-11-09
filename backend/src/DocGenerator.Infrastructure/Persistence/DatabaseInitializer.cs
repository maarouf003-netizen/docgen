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
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public DatabaseInitializer(
        DocGeneratorDbContext db,
        IPasswordHasher hasher,
        IOptions<DatabaseOptions> options,
        IHostEnvironment? environment = null,
        TimeProvider? clock = null,
        TimeZoneInfo? timeZone = null)
    {
        _db = db;
        _hasher = hasher;
        _options = options.Value;
        // الفحوص قاطعة في الإنتاج فقط: قاعدة الاختبار/التطوير مؤقتة، وفشلُها
        // يُسقط الجولة كاملة بلا فائدة. الافتراضي = قاطع (سلامة البيانات أولًا).
        _strictSchemaChecks = environment is null || !environment.IsDevelopment();
        _clock = clock ?? TimeProvider.System;
        _timeZone = timeZone ?? TimeZoneInfo.Utc;
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

        await EnsureNumberRegistryConsistencyAsync(ct);

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

    /// <summary>
    /// <summary>
    /// R9: مراجعة اتساق سجل أرقام الأساس عند الإقلاع — كل ملف مرقّم (غير محذوف)
    /// له دائرة وسنة صالحة وصف قيد غير ملغى في دائرته/سنته، ولا صفوف بلا دائرة.
    /// في الإنتاج قاطعة (ترفض الإقلاع) وفي التطوير تحذير فقط.
    /// </summary>
    private async Task EnsureNumberRegistryConsistencyAsync(CancellationToken ct)
    {
        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        var numbered = _db.Documents
            .AsNoTracking()
            .Where(d => !d.IsDeleted
                && d.FileNumber != null && d.FileNumber.Trim() != "");

        var missingCircuit = await numbered
            .Where(d => d.ExecutionCircuitId == null)
            .Select(d => d.Id)
            .Take(11)
            .ToListAsync(ct);

        var missingRow = await numbered
            .Where(d => d.ExecutionCircuitId != null
                && d.FileYear != null && d.FileYear.Trim() != "")
            .Where(d => !_db.BaseNumbers.Any(b =>
                b.DocumentId == d.Id
                && b.VoidedAt == null
                && b.Source == NumberSources.Registration
                && b.ExecutionCircuitId == d.ExecutionCircuitId
                && b.Year.ToString() == d.FileYear!.Trim()))
            .Select(d => d.Id)
            .Take(11)
            .ToListAsync(ct);

        var invalidYear = await GetInvalidRegistryYearIdsAsync(currentYear, ct);

        var orphans = await _db.BaseNumbers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(b => b.ExecutionCircuitId == null
                && (b.CircuitName == null || b.CircuitName.Trim() == ""))
            .Select(b => b.Id)
            .Take(11)
            .ToListAsync(ct);

        if (missingCircuit.Count == 0 && invalidYear.Count == 0
            && missingRow.Count == 0 && orphans.Count == 0)
            return;

        var message = "مراجعة اتساق سجل أرقام الأساس فشلت: "
            + $"ملفات مرقّمة بلا دائرة = {missingCircuit.Count}"
            + (missingCircuit.Count > 10 ? "+" : "")
            + $"، ملفات بسنة غير صالحة = {invalidYear.Count}"
            + (invalidYear.Count > 10 ? "+" : "")
            + $"، ملفات بلا صف قيد فعّال في دائرتها/سنتها = {missingRow.Count}"
            + (missingRow.Count > 10 ? "+" : "")
            + $"، صفوف بلا دائرة = {orphans.Count}"
            + (orphans.Count > 10 ? "+" : "")
            + " — راجع تدقيق خطة v2 §8.1/§10 (R9)."
            + (missingCircuit.Count > 0 ? $" أمثلة بلا دائرة: {string.Join(',', missingCircuit)}." : "")
            + (invalidYear.Count > 0 ? $" أمثلة بسنة غير صالحة: {string.Join(',', invalidYear.Take(10))}." : "");
        if (_strictSchemaChecks)
            throw new InvalidOperationException(message);
        Console.Error.WriteLine("[schema-check] WARNING: " + message);
    }

    /// <summary>
    /// سنوات غير صالحة على PostgreSQL باستعلام تجميعي واحد (لا تحميل 500k صف).
    /// على SQLite (اختبارات فقط) تُقرأ الأعمدة الصغيرة وتُفحص في C#.
    /// </summary>
    private async Task<List<int>> GetInvalidRegistryYearIdsAsync(int currentYear, CancellationToken ct)
    {
        if (_db.Database.IsNpgsql())
        {
            const string sql = """
                SELECT "Id" AS "Value"
                FROM "Documents"
                WHERE "IsDeleted" = false
                  AND "FileNumber" IS NOT NULL
                  AND btrim("FileNumber") <> ''
                  AND (
                    "FileYear" IS NULL
                    OR btrim("FileYear") = ''
                    OR CASE
                         WHEN regexp_replace(
                                translate(btrim("FileYear"),
                                  '٠١٢٣٤٥٦٧٨٩۰۱۲۳۴۵۶۷۸۹',
                                  '01234567890123456789'),
                                '\s', '', 'g') ~ '^[0-9]+$'
                         THEN regexp_replace(
                                translate(btrim("FileYear"),
                                  '٠١٢٣٤٥٦٧٨٩۰۱۲۳۴۵۶۷۸۹',
                                  '01234567890123456789'),
                                '\s', '', 'g')::int NOT BETWEEN 1900 AND {0}
                         ELSE true
                       END)
                ORDER BY "Id"
                LIMIT 11
                """;
            return await _db.Database.SqlQueryRaw<int>(sql, currentYear).ToListAsync(ct);
        }

        var rows = await _db.Documents
            .AsNoTracking()
            .Where(d => !d.IsDeleted
                && d.FileNumber != null && d.FileNumber.Trim() != "")
            .Select(d => new { d.Id, d.FileYear })
            .ToListAsync(ct);
        return rows
            .Where(d => ParseRegistryYear(d.FileYear, currentYear) is null)
            .Select(d => d.Id)
            .Take(11)
            .ToList();
    }

    private static int? ParseRegistryYear(string? rawYear, int currentYear)
    {
        var normalized = DigitNormalizer.NormalizeDigitsOrNull(rawYear);
        if (normalized is null
            || !int.TryParse(normalized, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var year))
            return null;
        return year >= 1900 && year <= currentYear ? year : null;
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
