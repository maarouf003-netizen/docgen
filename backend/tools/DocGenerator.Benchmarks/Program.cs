using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using DocGenerator.Benchmarks;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

Console.OutputEncoding = System.Text.Encoding.UTF8;

if (args.Any(static a => a == "--self-test"))
{
    return SelfTest.Run();
}

var options = BenchmarkOptions.Parse(args);
if (options.ShowHelp)
{
    PrintHelp();
    return 0;
}

if (BuildConfiguration() == "Debug")
    Console.WriteLine("[P3-01] تحذير: هذا بناء Debug — لا يُعتمد زمنه كحكم إنتاجي. استخدم `-c Release`.");

var databaseName = string.Create(
    CultureInfo.InvariantCulture,
    $"docgen_bench_p301_{options.Scale}_{options.Seed}_{DateTime.UtcNow:yyyyMMddHHmmss}");

Console.WriteLine($"[P3-01] قاعدة القياس: {databaseName}");
var database = await BenchmarkDatabase.CreateAsync(options.ConnectionString, databaseName, CancellationToken.None);

try
{
    var seedWatch = Stopwatch.StartNew();
    await MigrateAsync(database.ConnectionString);
    Console.WriteLine($"[P3-01] الهجرات: {seedWatch.Elapsed.TotalSeconds:0.0}s");

    var initialProbe = await PgStatStatementsProbe.ProbeAsync(database.ConnectionString, CancellationToken.None);
    Console.WriteLine(
        $"[P3-01] pg_stat_statements: متوفرة={initialProbe.Available} مثبّتة={initialProbe.Installed} محمّلة={initialProbe.Preloaded}");

    if (initialProbe.Installed && initialProbe.Preloaded)
    {
        await PgStatStatementsProbe.ResetAsync(database.ConnectionString, CancellationToken.None);
    }

    SeedManifest manifest;
    await using (var seedContext = CreateContext(database.ConnectionString))
    {
        Console.WriteLine($"[P3-01] البذر الحتمي ({options.Scale:N0} مستندًا، بذرة {options.Seed})…");
        var progress = new Progress<string>(message => Console.WriteLine("  " + message));
        manifest = await DeterministicSeeder.SeedAsync(
            seedContext, options.Scale, options.Seed, progress, CancellationToken.None);
    }

    var seedSeconds = seedWatch.Elapsed.TotalSeconds;
    var documentCount = await database.CountDocumentsAsync(CancellationToken.None);
    if (documentCount != options.Scale)
    {
        throw new InvalidOperationException(
            $"عدد المستندات بعد البذر {documentCount} لا يطابق المقياس المطلوب {options.Scale}.");
    }

    Console.WriteLine($"[P3-01] البذر اكتمل: {seedSeconds:0.0}s، {documentCount:N0} مستندًا. تثبيت الإحصاءات…");
    await database.PrepareForMeasurementAsync(CancellationToken.None);
    var signature = await database.ReadDataSignatureAsync(CancellationToken.None);
    Console.WriteLine($"[P3-01] بصمة البيانات: {signature.Fingerprint}");

    var queries = HotQueryCatalog.Build(manifest);
    var measurements = new List<QueryMeasurement>(queries.Count);
    var dumpSql = Environment.GetEnvironmentVariable("DOCGEN_BENCH_DUMP_SQL") == "1";

    foreach (var query in queries)
    {
        Console.WriteLine($"[P3-01] قياس {query.Id}…");
        var measurement = await MeasurementRunner.RunAsync(
            query,
            () => CreateContext(database.ConnectionString, dump: dumpSql),
            options,
            CancellationToken.None);
        measurements.Add(measurement);
        Console.WriteLine(
            $"  {query.Id}: p50={measurement.P50Ms:0.###}ms p95={measurement.P95Ms:0.###}ms rows={measurement.Rows}");
    }

    var statInfo = await PgStatStatementsProbe.ProbeAsync(database.ConnectionString, CancellationToken.None);

    // الخطط بعد لقطة الإحصاءات حتى لا تلوّث أرقام pg_stat_statements.
    var withPlans = new List<QueryMeasurement>(measurements.Count);
    foreach (var query in queries)
    {
        var measurement = measurements.Single(m => m.Id == query.Id);
        var capture = new CommandCaptureInterceptor();
        await using (var captureContext = CreateContext(database.ConnectionString, capture))
        {
            await query.Execute(captureContext, CancellationToken.None);
        }

        var plans = await PlanCollector.CaptureAsync(
            database.ConnectionString, capture.Commands, CancellationToken.None);
        withPlans.Add(MeasurementRunner.WithPlans(measurement, plans));
        Console.WriteLine($"  {query.Id}: {plans.Count} خطة (أخطاء: {plans.Count(p => p.Error is not null)})");
    }

    var serverSettings = await database.ReadServerSettingsAsync(CancellationToken.None);
    var report = new BenchmarkReport(
        SchemaVersion: 1,
        Task: options.Task,
        GeneratedAtUtc: DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
        Environment: new EnvironmentInfo(
            OperatingSystem: RuntimeInformation.OSDescription,
            Framework: RuntimeInformation.FrameworkDescription,
            MachineName: Environment.MachineName,
            ProcessorCount: Environment.ProcessorCount,
            TotalAvailableMemoryBytes: GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            BuildConfiguration: BuildConfiguration(),
            Scale: options.Scale,
            Seed: options.Seed,
            Iterations: options.Iterations,
            Warmup: options.Warmup,
            DatabaseName: database.DatabaseName,
            DocumentCount: documentCount,
            SeedSeconds: Math.Round(seedSeconds, 1, MidpointRounding.AwayFromZero),
            DataFingerprint: signature.Fingerprint,
            TableCounts: signature.TableCounts,
            PostgresSettings: serverSettings),
        PgStatStatements: statInfo,
        Queries: withPlans);

    var (jsonPath, markdownPath) = await ReportWriter.WriteAsync(
        report, options.OutputDirectory, CancellationToken.None);

    Console.WriteLine();
    Console.WriteLine("[P3-01] النتيجة:");
    foreach (var measurement in withPlans)
    {
        Console.WriteLine(
            $"  {measurement.Id,-26} p50={measurement.P50Ms,8:0.###}ms  p95={measurement.P95Ms,8:0.###}ms  rows={measurement.Rows}");
    }

    if (statInfo.Note is not null)
        Console.WriteLine($"[P3-01] pg_stat_statements: {statInfo.Note}");

    Console.WriteLine($"[P3-01] JSON: {jsonPath}");
    Console.WriteLine($"[P3-01] Markdown: {markdownPath}");

    return 0;
}
finally
{
    if (options.KeepDatabase)
    {
        Console.WriteLine($"[P3-01] أُبقيت القاعدة: {database.DatabaseName}");
    }
    else
    {
        await database.DropAsync(CancellationToken.None);
        Console.WriteLine("[P3-01] أُسقطت قاعدة القياس.");
    }
}

static DocGeneratorPostgresDbContext CreateContext(
    string connectionString, DbCommandInterceptor? interceptor = null, bool dump = false)
{
    var builder = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
        .UseNpgsql(connectionString, npg =>
            npg.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));

    if (interceptor is not null)
        builder.AddInterceptors(interceptor);
    else if (dump)
        builder.AddInterceptors(new CommandCaptureInterceptor(dump: true));

    return new DocGeneratorPostgresDbContext(builder.Options);
}

static async Task MigrateAsync(string connectionString)
{
    await using var context = CreateContext(connectionString);
    await context.Database.MigrateAsync();
}

static string BuildConfiguration()
{
#if DEBUG
    return "Debug";
#else
    return "Release";
#endif
}

static void PrintHelp()
{
    Console.WriteLine("""
        DocGenerator.Benchmarks — منصة قياس P3-01 على PostgreSQL/Release.

        الاستخدام:
          dotnet run -c Release --project backend/tools/DocGenerator.Benchmarks -- --scale 10000

        الخيارات:
          --scale <10000|50000>   حجم البيانات الحتمي (إلزامي).
          --iterations <n>        عدد تكرارات القياس بعد الإحماء (افتراضي 20).
          --warmup <n>            عدد تكرارات الإحماء (افتراضي 5).
          --seed <n>              بذرة البذر الحتمي (افتراضي 20261121).
          --task <P3-01|P3-02>    وسم المهمة في أسماء التقارير (افتراضي P3-01).
          --connection <string>   سلسلة اتصال PostgreSQL (أو DOCGEN_BENCH_POSTGRES / DOCGEN_TEST_POSTGRES).
          --output <dir>          مجلد النتائج (افتراضي docs/audit/measurements).
          --keep-database         لا تُسقط قاعدة القياس بعد الانتهاء.
          --help                  هذه المساعدة.

        ملاحظات:
          - يجب تشغيله ببناء Release: زمن Debug لا يُستخدم كحكم إنتاجي.
          - كل تشغيل ينشئ قاعدة جديدة ويهاجرها ثم يسقطها (إلا مع --keep-database).
          - pg_stat_statements تُفحص فعليًا؛ وإن غابت تُسجَّل الحالة في التقرير بلا اختلاق أرقام.
        """);
}
