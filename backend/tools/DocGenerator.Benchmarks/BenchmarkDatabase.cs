using Npgsql;

namespace DocGenerator.Benchmarks;

/// <summary>
/// قاعدة قياس معزولة على خادم `PostgreSQL` حقيقي. تُنشأ باسم فريد، تُهاجَر بهجرات
/// `DocGeneratorPostgresDbContext` نفسها (لا `EnsureCreated`)، وتُسقط في النهاية إلا
/// إذا طُلب الاحتفاظ بها. هذا يمنع أي اعتماد على `SQLite` أو على قالب اختباري.
/// </summary>
public sealed class BenchmarkDatabase
{
    private readonly string _adminConnectionString;

    private BenchmarkDatabase(string adminConnectionString, string databaseName, string connectionString)
    {
        _adminConnectionString = adminConnectionString;
        DatabaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string DatabaseName { get; }

    public string ConnectionString { get; }

    public static async Task<BenchmarkDatabase> CreateAsync(
        string baseConnectionString, string databaseName, CancellationToken ct)
    {
        var admin = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = "postgres",
            Pooling = false,
        };

        await using (var connection = new NpgsqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync(ct);

            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await command.ExecuteNonQueryAsync(ct);
        }

        var target = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Database = databaseName,
            // القياس أحادي الخيط: تجميع اتصالات صغير يكفي ولا يستهلك `max_connections`.
            Pooling = true,
            MaxPoolSize = 8,
            MinPoolSize = 0,
        };

        return new BenchmarkDatabase(admin.ConnectionString, databaseName, target.ConnectionString);
    }

    public async Task DropAsync(CancellationToken ct = default)
    {
        NpgsqlConnection.ClearAllPools();

        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{DatabaseName}\" WITH (FORCE)";
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>قراءة إعدادات الخادم المؤثرة في القياس (نسخة، ذاكرة، إحصاءات).</summary>
    public async Task<Dictionary<string, string>> ReadServerSettingsAsync(CancellationToken ct)
    {
        var keys = new[]
        {
            "server_version",
            "shared_preload_libraries",
            "max_connections",
            "shared_buffers",
            "work_mem",
            "maintenance_work_mem",
            "effective_cache_size",
            "random_page_cost",
            "seq_page_cost",
            "jit",
            "track_io_timing",
        };

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(ct);

        foreach (var key in keys)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT current_setting(@key, true)";
            command.Parameters.AddWithValue("key", key);
            var value = await command.ExecuteScalarAsync(ct);
            result[key] = value is null or DBNull ? string.Empty : Convert.ToString(value) ?? string.Empty;
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT version()";
            result["version"] = Convert.ToString(await command.ExecuteScalarAsync(ct)) ?? string.Empty;
        }

        return result;
    }

    public async Task<long> CountDocumentsAsync(CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"Documents\"";
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
    }

    /// <summary>
    /// بصمة حتمية للبيانات: تجزئة معرّفات المستندات وحقولها الدلالية مرتبة بالمعرّف،
    /// مع عدّ الجداول المرتبطة. تثبت أن بذرتين بنفس البذرة أنتجتا البيانات نفسها.
    /// </summary>
    public async Task<DataSignature> ReadDataSignatureAsync(CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(ct);

        string fingerprint;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT md5(string_agg(
                    "Id"::text || '|' ||
                    extract(epoch FROM "CreatedAt")::bigint::text || '|' ||
                    coalesce("BorrowerName", '') || '|' ||
                    coalesce("BorrowerFamily", '') || '|' ||
                    coalesce("ExecStatus", '') || '|' ||
                    coalesce("ExecutedStatus", ''),
                    ',' ORDER BY "Id"))
                FROM "Documents"
                """;
            fingerprint = Convert.ToString(await command.ExecuteScalarAsync(ct)) ?? string.Empty;
        }

        var tables = new[]
        {
            "Documents",
            "ApplicantPublicEntities",
            "ExecutedPublicEntities",
            "ExecutionApplicants",
            "Guarantors",
            "Assets",
            "ExecutionActions",
            "DocumentBaseNumbers",
            "HeadAlerts",
            "HeadAlertRecipients",
        };

        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
            counts[table] = Convert.ToInt64(await command.ExecuteScalarAsync(ct));
        }

        return new DataSignature(fingerprint, counts);
    }

    /// <summary>
    /// يُثبّت الإحصاءات بعد البذر الكثيف قبل أي قياس: بدونه يخطط `PostgreSQL` بأرقام
    /// تقديرية قديمة فيختار أول استعلام خطة أسوأ بمئات المرات، ثم يغيّرها `autovacuum`
    /// في منتصف التشغيل — وهي أرقام غير قابلة للتكرار ولا تمثّل الحالة المستقرة.
    /// </summary>
    public async Task PrepareForMeasurementAsync(CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "VACUUM (ANALYZE)";
        command.CommandTimeout = 600;
        await command.ExecuteNonQueryAsync(ct);
    }
}
