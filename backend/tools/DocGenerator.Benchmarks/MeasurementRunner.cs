using System.Diagnostics;
using System.Text.Json;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Benchmarks;

/// <summary>
/// تشغيل استعلام ساخن بعد إحماء، بأخذ عيّنات من إنشاء سياق جديد لكل تكرار (كما في طلب
/// حقيقي)، ثم حساب `p50`/`p95` بطريقة الاستيفاء الخطي على العيّنات المرتبة.
/// </summary>
public static class MeasurementRunner
{
    public static async Task<QueryMeasurement> RunAsync(
        HotQuery query,
        Func<DocGeneratorPostgresDbContext> contextFactory,
        BenchmarkOptions options,
        CancellationToken ct)
    {
        QueryOutcome? expected = null;

        for (var i = 0; i < options.Warmup; i++)
        {
            await using var db = contextFactory();
            var outcome = await query.Execute(db, ct);
            expected ??= outcome;
            AssertSameOutcome(query.Id, expected, outcome, "الإحماء");
        }

        if (expected is null)
        {
            // --warmup 0 مسموح: نأخذ مرجع النتيجة مرة واحدة دون احتسابها في العيّنة.
            await using var baselineContext = contextFactory();
            expected = await query.Execute(baselineContext, ct);
        }

        var samples = new List<double>(options.Iterations);
        for (var i = 0; i < options.Iterations; i++)
        {
            await using var db = contextFactory();
            var started = Stopwatch.GetTimestamp();
            var outcome = await query.Execute(db, ct);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            AssertSameOutcome(query.Id, expected, outcome, "القياس الزمني");
            samples.Add(elapsed);
        }

        samples.Sort();
        var mean = samples.Average();
        var stdDev = Math.Sqrt(samples.Sum(x => (x - mean) * (x - mean)) / samples.Count);

        return new QueryMeasurement(
            Id: query.Id,
            Description: query.Description,
            Rows: expected?.Rows ?? 0,
            Fingerprint: expected?.Fingerprint ?? string.Empty,
            Iterations: options.Iterations,
            Warmup: options.Warmup,
            P50Ms: Round(Percentile(samples, 50)),
            P95Ms: Round(Percentile(samples, 95)),
            MinMs: Round(samples[0]),
            MaxMs: Round(samples[^1]),
            MeanMs: Round(mean),
            StdDevMs: Round(stdDev),
            SamplesMs: samples.Select(Round).ToList(),
            Plans: []);
    }

    /// <summary>يستبدل عقود الخطط بعد جمعها (السجل قابل للتعديل عمدًا ليبقى التقرير كائنًا واحدًا).</summary>
    public static QueryMeasurement WithPlans(
        QueryMeasurement measurement, IReadOnlyList<PlanCapture> plans) =>
        measurement with { Plans = plans };

    /// <summary>
    /// `p95` بعيّنة صغيرة لا تُترك للحدس: استيفاء خطي بين الرتب المجاورة (R-7/Excel).
    /// العيّنة المفردة تعيد قيمتها كما هي.
    /// </summary>
    public static double Percentile(IReadOnlyList<double> sortedSamples, double percentile)
    {
        if (sortedSamples.Count == 0)
            throw new ArgumentException("لا توجد عيّنات لحساب المئين.", nameof(sortedSamples));
        if (sortedSamples.Count == 1)
            return sortedSamples[0];

        var rank = (percentile / 100d) * (sortedSamples.Count - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        if (lower == upper)
            return sortedSamples[lower];

        var weight = rank - lower;
        return (sortedSamples[lower] * (1 - weight)) + (sortedSamples[upper] * weight);
    }

    private static void AssertSameOutcome(
        string queryId, QueryOutcome expected, QueryOutcome actual, string phase)
    {
        if (expected.Rows != actual.Rows || !string.Equals(expected.Fingerprint, actual.Fingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"نتيجة الاستعلام {queryId} تغيّرت في مرحلة {phase}: "
                + $"({expected.Rows}, {expected.Fingerprint}) ← ({actual.Rows}, {actual.Fingerprint}). "
                + "القياس يتطلب نتيجة صحيحة وثابتة.");
        }
    }

    private static double Round(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}

public sealed record QueryMeasurement(
    string Id,
    string Description,
    int Rows,
    string Fingerprint,
    int Iterations,
    int Warmup,
    double P50Ms,
    double P95Ms,
    double MinMs,
    double MaxMs,
    double MeanMs,
    double StdDevMs,
    IReadOnlyList<double> SamplesMs,
    IReadOnlyList<PlanCapture> Plans);

public sealed record PlanCapture(
    string Sql,
    IReadOnlyList<PlanParameter> Parameters,
    JsonElement? Plan,
    string? Error);

public sealed record PlanParameter(string Name, string NpgsqlDbType, bool IsNull, string? Value);

public sealed record PgStatStatement(
    string QueryId,
    long Calls,
    long Rows,
    double TotalExecTimeMs,
    double MeanExecTimeMs,
    string Query);

public sealed record PgStatStatementsInfo(
    bool Available,
    bool Installed,
    bool Preloaded,
    string SharedPreloadLibraries,
    string? Note,
    IReadOnlyList<PgStatStatement> TopStatements);

public sealed record BenchmarkReport(
    int SchemaVersion,
    string Task,
    string GeneratedAtUtc,
    EnvironmentInfo Environment,
    PgStatStatementsInfo PgStatStatements,
    IReadOnlyList<QueryMeasurement> Queries);

public sealed record EnvironmentInfo(
    string OperatingSystem,
    string Framework,
    string MachineName,
    int ProcessorCount,
    long TotalAvailableMemoryBytes,
    string BuildConfiguration,
    int Scale,
    int Seed,
    int Iterations,
    int Warmup,
    string DatabaseName,
    long DocumentCount,
    double SeedSeconds,
    string DataFingerprint,
    IReadOnlyDictionary<string, long> TableCounts,
    IReadOnlyDictionary<string, string> PostgresSettings);
