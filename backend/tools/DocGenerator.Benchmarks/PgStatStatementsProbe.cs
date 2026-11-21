using Npgsql;

namespace DocGenerator.Benchmarks;

/// <summary>
/// فحص `pg_stat_statements` بصدق: لا تُختلق أرقام إن كانت الإضافة غير متاحة أو غير
/// محمّلة في `shared_preload_libraries`. النتيجة تحمل الحالة والنص الصريح لما غاب.
/// </summary>
public static class PgStatStatementsProbe
{
    public static async Task<PgStatStatementsInfo> ProbeAsync(
        string connectionString, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var available = await ScalarBoolAsync(
            connection, "SELECT EXISTS (SELECT 1 FROM pg_available_extensions WHERE name = 'pg_stat_statements')", ct);
        var preloaded = (await ScalarStringAsync(connection, "SELECT current_setting('shared_preload_libraries', true)", ct) ?? string.Empty)
            .Contains("pg_stat_statements", StringComparison.OrdinalIgnoreCase);
        var installed = await ScalarBoolAsync(
            connection, "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_stat_statements')", ct);

        string? note = null;
        if (!available)
        {
            note = "إضافة pg_stat_statements غير متوفرة على هذا الخادم (pg_available_extensions) — لم تُجمع إحصاءاتها.";
        }
        else if (!installed)
        {
            try
            {
                await using var create = connection.CreateCommand();
                create.CommandText = "CREATE EXTENSION IF NOT EXISTS pg_stat_statements";
                await create.ExecuteNonQueryAsync(ct);
                installed = true;
                note = preloaded
                    ? "ثُبّتت الإضافة في قاعدة القياس."
                    : "ثُبّتت الإضافة، لكن shared_preload_libraries لا يحمّلها؛ قد تفشل القراءة حتى إعادة تشغيل الخادم بالإعداد الصحيح.";
            }
            catch (Exception ex)
            {
                note = "تعذّر تثبيت pg_stat_statements: " + ex.Message;
            }
        }

        var statements = new List<PgStatStatement>();
        if (installed && preloaded)
        {
            try
            {
                statements = await ReadTopStatementsAsync(connection, ct);
            }
            catch (Exception ex)
            {
                note = (note is null ? string.Empty : note + " ") + "تعذّرت قراءة pg_stat_statements: " + ex.Message;
            }
        }
        else if (installed)
        {
            note = (note is null ? string.Empty : note + " ")
                + "الإضافة مثبّتة لكنها ليست في shared_preload_libraries، لذلك لا تُجمع إحصاءات فعلية.";
        }

        return new PgStatStatementsInfo(
            Available: available,
            Installed: installed,
            Preloaded: preloaded,
            SharedPreloadLibraries: await ScalarStringAsync(
                connection, "SELECT current_setting('shared_preload_libraries', true)", ct) ?? string.Empty,
            Note: note,
            TopStatements: statements);
    }

    public static async Task ResetAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_stat_statements_reset()";
        await command.ExecuteScalarAsync(ct);
    }

    private static async Task<List<PgStatStatement>> ReadTopStatementsAsync(
        NpgsqlConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT queryid::text,
                   calls,
                   rows,
                   total_exec_time,
                   mean_exec_time,
                   query
            FROM pg_stat_statements
            WHERE query NOT ILIKE '%pg_stat_statements%'
            ORDER BY total_exec_time DESC
            LIMIT 50
            """;

        var result = new List<PgStatStatement>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new PgStatStatement(
                QueryId: reader.GetString(0),
                Calls: reader.GetInt64(1),
                Rows: reader.GetInt64(2),
                TotalExecTimeMs: Math.Round(reader.GetDouble(3), 3),
                MeanExecTimeMs: Math.Round(reader.GetDouble(4), 3),
                Query: reader.GetString(5)));
        }

        return result;
    }

    private static async Task<bool> ScalarBoolAsync(
        NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToBoolean(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<string?> ScalarStringAsync(
        NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
    }
}
