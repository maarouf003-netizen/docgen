using System.Globalization;
using Npgsql;

namespace DocGenerator.Benchmarks;

/// <summary>
/// خيارات تشغيل منصة القياس (`P3-01`). كل القيم لها افتراضات صريحة، والمقياس والاتصال
/// إلزاميان: لا معنى لقياس بلا `PostgreSQL` حقيقي ولا بلا حجم بيانات محدد.
/// </summary>
public sealed record BenchmarkOptions(
    int Scale,
    int Iterations,
    int Warmup,
    int Seed,
    string Task,
    string ConnectionString,
    string OutputDirectory,
    bool KeepDatabase,
    bool ShowHelp)
{
    public const int DefaultIterations = 20;
    public const int DefaultWarmup = 5;
    public const int DefaultSeed = 20261121;

    public static BenchmarkOptions Parse(string[] args)
    {
        if (args.Any(static a => a is "--help" or "-h" or "/?"))
            return Defaults() with { ShowHelp = true };

        int? scale = null;
        var iterations = DefaultIterations;
        var warmup = DefaultWarmup;
        var seed = DefaultSeed;
        var task = "P3-01";
        string? connection = null;
        string? output = null;
        var keepDatabase = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string Next() => i + 1 < args.Length
                ? args[++i]
                : throw new ArgumentException($"الخيار {arg} يحتاج قيمة.");

            switch (arg)
            {
                case "--scale":
                    scale = int.Parse(Next(), CultureInfo.InvariantCulture);
                    break;
                case "--iterations":
                    iterations = int.Parse(Next(), CultureInfo.InvariantCulture);
                    break;
                case "--warmup":
                    warmup = int.Parse(Next(), CultureInfo.InvariantCulture);
                    break;
                case "--seed":
                    seed = int.Parse(Next(), CultureInfo.InvariantCulture);
                    break;
                case "--task":
                    task = Next().Trim();
                    break;
                case "--connection":
                    connection = Next();
                    break;
                case "--output":
                    output = Next();
                    break;
                case "--keep-database":
                    keepDatabase = true;
                    break;
                default:
                    throw new ArgumentException($"خيار غير معروف: {arg}");
            }
        }

        if (scale is null)
            throw new ArgumentException("--scale إلزامي (مثال: --scale 10000).");
        if (scale is not (10000 or 50000))
            throw new ArgumentException("--scale يجب أن يكون 10000 أو 50000 وفق نطاق P3-01.");
        if (iterations < 1)
            throw new ArgumentException("--iterations يجب أن يكون 1 على الأقل.");
        if (warmup < 0)
            throw new ArgumentException("--warmup لا يكون سالبًا.");
        if (task is not ("P3-01" or "P3-02"))
            throw new ArgumentException("--task يجب أن يكون P3-01 أو P3-02.");

        connection ??= Environment.GetEnvironmentVariable("DOCGEN_BENCH_POSTGRES")
            ?? Environment.GetEnvironmentVariable("DOCGEN_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new ArgumentException(
                "سلسلة اتصال PostgreSQL إلزامية: مرّر --connection أو عيّن DOCGEN_BENCH_POSTGRES/DOCGEN_TEST_POSTGRES.");
        }

        ValidateConnection(connection);

        var root = RepositoryRoot.Find();
        var outputDirectory = output is null
            ? Path.Combine(root, "docs", "audit", "measurements")
            : Path.GetFullPath(output, root);

        return new BenchmarkOptions(
            scale.Value, iterations, warmup, seed, task, connection.Trim(), outputDirectory, keepDatabase, false);
    }

    public static BenchmarkOptions Defaults() => new(
        Scale: 10000,
        Iterations: DefaultIterations,
        Warmup: DefaultWarmup,
        Seed: DefaultSeed,
        Task: "P3-01",
        ConnectionString: string.Empty,
        OutputDirectory: string.Empty,
        KeepDatabase: false,
        ShowHelp: true);

    private static void ValidateConnection(string connection)
    {
        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(connection);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"سلسلة اتصال PostgreSQL غير صالحة: {ex.Message}", ex);
        }

        if (string.IsNullOrWhiteSpace(builder.Host))
            throw new ArgumentException("سلسلة الاتصال بلا Host.");
    }
}

/// <summary>يحدد جذر المستودع بالصعود من مجلد التشغيل حتى يجد وثيقة الخريطة.</summary>
public static class RepositoryRoot
{
    public static string Find()
    {
        var candidates = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
        };

        foreach (var start in candidates)
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "docs", "PERFECTION_ROADMAP.md")))
                    return directory.FullName;
                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException(
            "تعذّر تحديد جذر المستودع (docs/PERFECTION_ROADMAP.md). شغّل الأمر من داخل المستودع.");
    }
}
