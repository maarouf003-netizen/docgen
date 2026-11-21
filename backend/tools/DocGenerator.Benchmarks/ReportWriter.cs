using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DocGenerator.Benchmarks;

/// <summary>
/// يكتب نتائج القياس إلى `docs/audit/measurements`: ملف JSON كامل (خطط + عيّنات +
/// إحصاءات `pg_stat_statements`) وملحق Markdown موجز للقراءة البشرية.
/// </summary>
public static class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static async Task<(string JsonPath, string MarkdownPath)> WriteAsync(
        BenchmarkReport report, string outputDirectory, CancellationToken ct)
    {
        Directory.CreateDirectory(outputDirectory);
        var date = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var stem = $"{date}-{report.Task.ToLowerInvariant()}-{report.Environment.Scale.ToString(CultureInfo.InvariantCulture)}";
        var jsonPath = Path.Combine(outputDirectory, stem + ".json");
        var markdownPath = Path.Combine(outputDirectory, stem + ".md");

        var json = JsonSerializer.Serialize(report, JsonOptions);
        await File.WriteAllTextAsync(jsonPath, json + Environment.NewLine, ct);
        await File.WriteAllTextAsync(markdownPath, BuildMarkdown(report), ct);

        return (jsonPath, markdownPath);
    }

    private static string BuildMarkdown(BenchmarkReport report)
    {
        var env = report.Environment;
        var builder = new System.Text.StringBuilder();

        builder.AppendLine($"# `{report.Task}` — قياس `PostgreSQL/Release` عند {env.Scale:N0} مستندًا — {report.GeneratedAtUtc[..10]}");
        builder.AppendLine();
        builder.AppendLine("> مولَّد آليًا بـ`backend/tools/DocGenerator.Benchmarks` (لا تُحرَّر الأرقام يدويًا).");
        builder.AppendLine("> القياس على `PostgreSQL` حقيقي وبناء `Release` بعد إحماء؛ `p50`/`p95` بطريقة الاستيفاء الخطي.");
        builder.AppendLine();

        builder.AppendLine("## البيئة والبذر");
        builder.AppendLine();
        builder.AppendLine("| البند | القيمة |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| نظام التشغيل | `{env.OperatingSystem}` |");
        builder.AppendLine($"| إطار التشغيل | `{env.Framework}` |");
        builder.AppendLine($"| بناء | `{env.BuildConfiguration}` |");
        builder.AppendLine($"| المعالج | {env.ProcessorCount} منطقي |");
        builder.AppendLine($"| قاعدة القياس | `{env.DatabaseName}` |");
        builder.AppendLine($"| المستندات | {env.DocumentCount:N0} |");
        builder.AppendLine($"| البذرة | `{env.Seed}` |");
        builder.AppendLine($"| الإحماء / التكرارات | {env.Warmup} / {env.Iterations} |");
        builder.AppendLine($"| زمن البذر | {env.SeedSeconds.ToString("0.0", CultureInfo.InvariantCulture)}s |");
        builder.AppendLine($"| بصمة البيانات | `{env.DataFingerprint}` |");
        builder.AppendLine($"| إصدار `PostgreSQL` | `{Value(env.PostgresSettings, "version")}` |");
        builder.AppendLine($"| `shared_buffers` / `work_mem` | `{Value(env.PostgresSettings, "shared_buffers")}` / `{Value(env.PostgresSettings, "work_mem")}` |");
        builder.AppendLine($"| `random_page_cost` / `seq_page_cost` | `{Value(env.PostgresSettings, "random_page_cost")}` / `{Value(env.PostgresSettings, "seq_page_cost")}` |");
        builder.AppendLine();
        builder.AppendLine(
            "**عدّ الجداول:** "
            + string.Join(" · ", env.TableCounts.Select(pair => $"`{pair.Key}`={pair.Value:N0}")));
        builder.AppendLine();

        builder.AppendLine("## `pg_stat_statements`");
        builder.AppendLine();
        var stat = report.PgStatStatements;
        builder.AppendLine($"- متوفرة: **{(stat.Available ? "نعم" : "لا")}** · مثبّتة: **{(stat.Installed ? "نعم" : "لا")}** · محمّلة مسبقًا: **{(stat.Preloaded ? "نعم" : "لا")}**.");
        if (!string.IsNullOrWhiteSpace(stat.SharedPreloadLibraries))
            builder.AppendLine($"- `shared_preload_libraries`: `{stat.SharedPreloadLibraries}`.");
        if (!string.IsNullOrWhiteSpace(stat.Note))
            builder.AppendLine($"- ملاحظة: {stat.Note}");
        builder.AppendLine();

        if (stat.TopStatements.Count > 0)
        {
            builder.AppendLine("أعلى العبارات بـ`total_exec_time` (بعد إعادة التصفير قبل القياس):");
            builder.AppendLine();
            builder.AppendLine("| # | استدعاءات | صفوف | إجمالي ms | متوسط ms | الاستعلام (مقتطف) |");
            builder.AppendLine("|---|---|---|---|---|---|");
            var index = 1;
            foreach (var statement in stat.TopStatements.Take(12))
            {
                builder.AppendLine(
                    $"| {index++} | {statement.Calls:N0} | {statement.Rows:N0} | "
                    + $"{statement.TotalExecTimeMs.ToString("0.##", CultureInfo.InvariantCulture)} | "
                    + $"{statement.MeanExecTimeMs.ToString("0.###", CultureInfo.InvariantCulture)} | "
                    + $"`{Escape(Trim(statement.Query, 110))}` |");
            }
            builder.AppendLine();
        }

        builder.AppendLine("## الاستعلامات الساخنة");
        builder.AppendLine();
        builder.AppendLine("| الاستعلام | صفوف | `p50` ms | `p95` ms | أدنى | أقصى | عدد الخطط | ملاحظة الخطة |");
        builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|---|");
        foreach (var query in report.Queries)
        {
            builder.AppendLine(
                $"| `{query.Id}` | {query.Rows:N0} | "
                + $"{query.P50Ms.ToString("0.###", CultureInfo.InvariantCulture)} | "
                + $"{query.P95Ms.ToString("0.###", CultureInfo.InvariantCulture)} | "
                + $"{query.MinMs.ToString("0.###", CultureInfo.InvariantCulture)} | "
                + $"{query.MaxMs.ToString("0.###", CultureInfo.InvariantCulture)} | "
                + $"{query.Plans.Count} | "
                + $"{PlanNote(query)} |");
        }
        builder.AppendLine();

        builder.AppendLine("## قراءة الخطط");
        builder.AppendLine();
        foreach (var query in report.Queries)
        {
            builder.AppendLine($"### `{query.Id}`");
            builder.AppendLine();
            builder.AppendLine(query.Description);
            builder.AppendLine();
            if (query.Plans.Count == 0)
            {
                builder.AppendLine("لم تُلتقط خطة.");
                builder.AppendLine();
                continue;
            }

            foreach (var plan in query.Plans)
            {
                if (plan.Error is not null)
                {
                    builder.AppendLine($"- `EXPLAIN` فشل: `{Escape(Trim(plan.Error, 300))}`.");
                    continue;
                }

                builder.AppendLine($"- `{PlanSummary(plan.Plan)}` — `{Escape(Trim(plan.Sql, 160))}`");
            }

            builder.AppendLine();
        }

        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine("> الحدود: القياس محلي على خادم `PostgreSQL` واحد بلا حمل متزامن؛");
        builder.AppendLine("> لا يعني التطابق مع زمن الإنتاج، بل يثبت أن كل استعلام ساخن مقيس على المسار الحقيقي");
        builder.AppendLine("> وبخطة قابلة للتدقيق. `p95` هنا حكم على هذا الجهاز وهذه البيانات الحتمية فقط.");
        builder.AppendLine();

        return builder.ToString();
    }

    private static string PlanNote(QueryMeasurement query)
    {
        var usable = query.Plans
            .Where(p => p.Error is null && p.Plan is not null)
            .ToList();
        if (usable.Count == 0)
            return query.Plans.Any(p => p.Error is not null) ? "فشل EXPLAIN" : "—";

        var seqScan = usable.Any(p => ContainsSeqScanOnDocuments(p.Plan!.Value));
        var slowest = usable
            .OrderByDescending(p => RootActualTime(p.Plan!.Value))
            .First();

        var summary = PlanSummary(slowest.Plan);
        return seqScan ? $"`Seq Scan` على `Documents`؛ أعلى عقدة: {summary}" : summary;
    }

    private static double RootActualTime(JsonElement plan)
    {
        try
        {
            var root = plan.ValueKind == JsonValueKind.Array ? plan[0] : plan;
            if (root.TryGetProperty("Plan", out var node))
                root = node;
            return root.TryGetProperty("Actual Total Time", out var time) ? time.GetDouble() : 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static string PlanSummary(JsonElement? plan)
    {
        if (plan is null)
            return "—";

        try
        {
            var root = plan.Value.ValueKind == JsonValueKind.Array
                ? plan.Value[0]
                : plan.Value;
            if (root.TryGetProperty("Plan", out var node))
                root = node;

            var nodeType = root.TryGetProperty("Node Type", out var type) ? type.GetString() : null;
            var relation = root.TryGetProperty("Relation Name", out var rel) ? rel.GetString() : null;
            var rows = root.TryGetProperty("Actual Rows", out var actualRows) ? actualRows.GetDouble() : (double?)null;
            var time = root.TryGetProperty("Actual Total Time", out var totalTime) ? totalTime.GetDouble() : (double?)null;

            var summary = nodeType ?? "خطة";
            if (relation is not null)
                summary += $" على `{relation}`";
            if (rows is { } r)
                summary += $" ({r:N0} صفًا فعليًا";
            if (time is { } t)
                summary += rows is null ? $" ({t:0.###}ms)" : $"، {t:0.###}ms)";
            else if (rows is not null)
                summary += ")";
            return summary;
        }
        catch (Exception)
        {
            return "خطة غير قابلة للقراءة";
        }
    }

    private static bool ContainsSeqScanOnDocuments(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                if (ContainsSeqScanOnDocuments(child))
                    return true;
            }

            return false;
        }

        if (element.ValueKind != JsonValueKind.Object)
            return false;

        if (element.TryGetProperty("Node Type", out var nodeType)
            && nodeType.GetString() == "Seq Scan"
            && element.TryGetProperty("Relation Name", out var relation)
            && relation.GetString() == "Documents")
        {
            return true;
        }

        return element.EnumerateObject().Any(property => ContainsSeqScanOnDocuments(property.Value));
    }

    private static string Value(IReadOnlyDictionary<string, string> settings, string key) =>
        settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : "—";

    private static string Trim(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    private static string Escape(string text) => text.Replace("|", "\\|").ReplaceLineEndings(" ");
}
