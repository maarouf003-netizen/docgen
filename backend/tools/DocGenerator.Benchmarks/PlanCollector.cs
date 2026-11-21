using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using NpgsqlTypes;

namespace DocGenerator.Benchmarks;

/// <summary>
/// يعترض أوامر EF أثناء تنفيذ واحد فقط (لا أثناء القياس) لالتقاط SQL والمعاملات الفعلية،
/// ثم يُعاد تنفيذها بـ`EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` على اتصال مستقل.
/// هذا يضمن أن الخطة تعود للاستعلام الإنتاجي نفسه لا لنسخة يدوية.
/// </summary>
public sealed class CommandCaptureInterceptor : DbCommandInterceptor
{
    private readonly bool _dump;

    public CommandCaptureInterceptor(bool dump = false) => _dump = dump;

    public List<CapturedCommand> Commands { get; } = [];

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Capture(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Capture(command);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Capture(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Capture(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Capture(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void Capture(DbCommand command)
    {
        if (_dump)
        {
            Console.WriteLine("[SQL] " + command.CommandText.ReplaceLineEndings(" "));
            foreach (DbParameter parameter in command.Parameters)
                Console.WriteLine($"      {parameter.ParameterName} = {parameter.Value}");
        }

        var parameters = new List<CapturedParameter>(command.Parameters.Count);
        foreach (DbParameter parameter in command.Parameters)
        {
            var npgsql = parameter as NpgsqlParameter;
            parameters.Add(new CapturedParameter(
                Name: parameter.ParameterName,
                NpgsqlDbType: npgsql?.NpgsqlDbType,
                Value: parameter.Value is DBNull ? null : parameter.Value));
        }

        Commands.Add(new CapturedCommand(command.CommandText, parameters));
    }
}

public sealed record CapturedCommand(string Sql, IReadOnlyList<CapturedParameter> Parameters);

public sealed record CapturedParameter(string Name, NpgsqlDbType? NpgsqlDbType, object? Value);

public static class PlanCollector
{
    public static async Task<IReadOnlyList<PlanCapture>> CaptureAsync(
        string connectionString,
        IReadOnlyList<CapturedCommand> commands,
        CancellationToken ct)
    {
        var plans = new List<PlanCapture>(commands.Count);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        foreach (var command in commands)
        {
            plans.Add(await ExplainAsync(connection, command, ct));
        }

        return plans;
    }

    private static async Task<PlanCapture> ExplainAsync(
        NpgsqlConnection connection, CapturedCommand captured, CancellationToken ct)
    {
        var parameters = captured.Parameters
            .Select(p => new PlanParameter(
                p.Name,
                p.NpgsqlDbType?.ToString() ?? string.Empty,
                p.Value is null,
                p.Value is null ? null : FormatValue(p.Value)))
            .ToList();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + captured.Sql.TrimEnd(';');
            command.CommandTimeout = 180;

            foreach (var parameter in captured.Parameters)
            {
                var npgsqlParameter = parameter.NpgsqlDbType is { } type
                    ? new NpgsqlParameter(parameter.Name, type)
                    : new NpgsqlParameter(parameter.Name, parameter.Value ?? DBNull.Value);

                npgsqlParameter.Value = parameter.Value ?? DBNull.Value;
                command.Parameters.Add(npgsqlParameter);
            }

            var raw = await command.ExecuteScalarAsync(ct) as string;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new PlanCapture(captured.Sql, parameters, null, "EXPLAIN أعاد نتيجة فارغة.");
            }

            using var document = JsonDocument.Parse(raw);
            return new PlanCapture(captured.Sql, parameters, document.RootElement.Clone(), null);
        }
        catch (Exception ex)
        {
            // لا نُخفي الفشل: يُسجّل نصه في التقرير بدل اختلاق خطة.
            return new PlanCapture(captured.Sql, parameters, null, ex.Message);
        }
    }

    private static string FormatValue(object value) => value switch
    {
        string text => text,
        Array array => $"[{array.Length} عناصر]",
        System.Collections.IEnumerable enumerable when value is not string =>
            $"[{string.Join(", ", enumerable.Cast<object>().Take(10))}]",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
    };
}
