using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DocGenerator.Testing;

/// <summary>
/// قاعدة <c>PostgreSQL</c> مؤقتة (‏`ADR-0009`):
/// يُبنى **قالب واحد لكل تشغيل** بـ<c>EnsureCreated</c> من
/// <see cref="DocGeneratorPostgresDbContext"/>، ثم:
/// <list type="bullet">
/// <item>الاختبار المعزول يحصل على **نسخة مستنسخة** بـ<c>CREATE DATABASE … TEMPLATE</c>؛</item>
/// <item>واختبارات صنفٍ واحد تتشارك نسخةً واحدة و<see cref="Reset"/> يفرّغها قبل كل
/// حالة — الاستنساخ (~2s مع الإسقاط) أغلى بكثير من تصفير جداول فارغة (~15ms دافئًا)
/// عندما تكون الحالات كثيرة (مسح الجداول الكبير في `Application.Tests`).</item>
/// </list>
///
/// <para>الاستخدام: <c>await using var db = await PostgresTestDb.CreateAsync();</c>
/// ثم <see cref="CreateContext"/> أو <see cref="ConnectionString"/> لاختبارات
/// الاتصالات المتعددة. تُسقط النسخ عند الخروج من العملية إن لم تُسقط يدويًا.</para>
///
/// <para>يفترض أن يستدعيه اختبار مُعلَّم بـ<see cref="PostgresFactAttribute"/>؛
/// ويُرمى استثناء صريح إن غابت السلسلة كي لا يمرّ اختبار غير مُبوَّب صامتًا.</para>
/// </summary>
public sealed class PostgresTestDb : IAsyncDisposable
{
    private static readonly object TemplateGate = new();
    private static readonly object CleanupGate = new();
    private static readonly List<string> LiveDatabases = [];
    private static string? _baseConnectionString;
    private static string? _templateDatabase;
    private static bool _exitHookRegistered;

    private readonly string _databaseName;
    private bool _disposed;

    private PostgresTestDb(string connectionString, string databaseName)
    {
        ConnectionString = connectionString;
        _databaseName = databaseName;
        Options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
            // مهلة أوامر أوسع في قاعدة الاختبار فقط: بذور القياس (10k) والاستعلامات
            // الثقيلة تتجاوز الافتراضي 30s تحت توازي التشغيل الكامل، فيتحول نجاح
            // الحالة إلى فشل توقيت كاذب. ليست مهلة إنتاجية.
            .UseNpgsql(connectionString, npgsql => npgsql.CommandTimeout(180))
            .Options;
    }

    /// <summary>
    /// سلسلة اتصال النسخة المعزولة. مُجمَّعة (‏`Pooling=true`، حجم 4، عمر خمول 15s):
    /// `Pooling=false` كان يفتح اتصالًا فيزيائيًا لكل عملية `EF` فتتحول مئات الحالات
    /// إلى دقائق إضافية. الإسقاط يستخدم <c>WITH (FORCE)</c> فيقطع أي اتصال خامل.
    /// </summary>
    public string ConnectionString { get; }


    /// <summary>خيارات سياق جاهزة فوق القاعدة نفسها (لاتصالات متعددة).</summary>
    public DbContextOptions<DocGeneratorPostgresDbContext> Options { get; }

    /// <summary>سياق جديد فوق القاعدة (الملكية للمستدعي: <c>await using</c>).</summary>
    public DocGeneratorPostgresDbContext CreateContext() => new(Options);

    /// <summary>
    /// يفرّغ كل جداول المخطط العام ويعيد تسلسلات المعرفات إلى 1، ويُبقي المخطط كما هو.
    /// استبدال سريع لاستنساخ قاعدة جديدة بين حالات الصنف نفسه، بلا صفوف من الحالة السابقة.
    ///
    /// <para>`DELETE` تحت <c>session_replication_role=replica</c> بدل `TRUNCATE`: الأخير
    /// يُعيد إنشاء ملفات كل جدول/فهرس (‏~200 ملف على 51 جدولًا) فيصير ~0.7s على Windows
    /// ويتضاعف تحت توازي الأصناف، بينما الحذف من جداول فارغة ميكانيكي ورخيص. إعادة
    /// التسلسل ضرورية لأن كثيرًا من الحالات تبذر صفًا واحدًا وتتوقع المعرّف 1.</para>
    /// متزامن عن قصد: يُستدعى من باني قاعدة الاختبار حيث لا يجوز حجب خيوط المجمّع.
    /// </summary>
    public void Reset()
    {
        // اتصال مُجمَّع: أول أمر على اتصال جديد يدفع كلفة تحليل/تخطيط (~300ms على
        // Windows)، وبقاء الـbackend دافئًا يجعل التصفير المتكرر ~15ms. القاعدة معزولة
        // لصنف واحد فلا يزاحمها شيء على هذا الاتصال.
        using var connection = new NpgsqlConnection(ConnectionString);
        connection.Open();

        var (tables, sequences) = ListRelations(connection);
        if (tables.Count == 0)
            return;

        var deletes = string.Join(
            "\n",
            tables.Select(t => $"DELETE FROM {t};"));
        using (var wipe = new NpgsqlCommand(
            "SET session_replication_role = replica;\n" + deletes
            + "\nSET session_replication_role = origin;", connection))
        {
            wipe.CommandTimeout = 120;
            wipe.ExecuteNonQuery();
        }

        if (sequences.Count > 0)
        {
            // إعادة التسلسلات في أمر منفصل: دمجها مع حذف وضع `replica` يكلّف مئات
            // المللي ثانية لكل مجموعة، بينما هي وحدها ~3ms.
            using var restart = new NpgsqlCommand(
                "SELECT " + string.Join(", ", sequences.Select(s => $"setval('{s}', 1, false)")) + ";",
                connection);
            restart.CommandTimeout = 120;
            restart.ExecuteNonQuery();
        }
    }

    /// <summary>جداول المخطط العام وتسلسلاته (دون سجل الهجرات) بترتيب ثابت.</summary>
    private static (List<string> Tables, List<string> Sequences) ListRelations(NpgsqlConnection connection)
    {
        var tables = new List<string>();
        using (var list = new NpgsqlCommand(ListTablesSql, connection))
        using (var reader = list.ExecuteReader())
        {
            while (reader.Read())
                tables.Add($"\"{reader.GetString(0)}\".\"{reader.GetString(1)}\"");
        }

        var sequences = new List<string>();
        using (var list = new NpgsqlCommand(ListSequencesSql, connection))
        using (var reader = list.ExecuteReader())
        {
            while (reader.Read())
                sequences.Add($"\"{reader.GetString(0)}\".\"{reader.GetString(1)}\"");
        }

        return (tables, sequences);
    }

    /// <summary>جداول المخطط العام دون سجل الهجرات (لا وجود له في قاعدة <c>EnsureCreated</c>).</summary>
    private const string ListTablesSql =
        "SELECT n.nspname, c.relname "
        + "FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace "
        + "WHERE n.nspname = 'public' AND c.relkind = 'r' "
        + "AND c.relname <> '__EFMigrationsHistory' ORDER BY 1, 2;";

    private const string ListSequencesSql =
        "SELECT schemaname, sequencename FROM pg_sequences "
        + "WHERE schemaname = 'public' ORDER BY 1, 2;";

    /// <summary>ينشئ نسخة معزولة جديدة من القالب (قالب يُهيَّأ مرة واحدة لكل تشغيل).</summary>
    public static PostgresTestDb Create()
    {
        var baseConnectionString = PostgresTestEnvironment.RequireOrSkip()
            ?? throw new InvalidOperationException(
                $"PostgresTestDb يتطلب {PostgresTestEnvironment.ConnectionStringVariable} — "
                + "علّم الاختبار بـ[PostgresFact] كي يُتخطّى صراحةً بدل الفشل.");

        var template = EnsureTemplate(baseConnectionString);
        var databaseName = "docgen_app_" + Guid.NewGuid().ToString("N")[..16];
        var connectionString = PostgresTestDatabase.Clone(
            baseConnectionString, template, databaseName);

        lock (CleanupGate)
        {
            LiveDatabases.Add(databaseName);
        }

        return new PostgresTestDb(connectionString, databaseName);
    }

    /// <summary>نظير غير المتزامن (لا يعتمد عليه مسار البناء لتجنّب حجب خيوط المجمّع).</summary>
    public static Task<PostgresTestDb> CreateAsync() => Task.FromResult(Create());

    private static string EnsureTemplate(string baseConnectionString)
    {
        // قفل عادي لا `SemaphoreSlim`: مسارا الإنشاء (البناء المتزامن والاختبارات غير
        // المتزامنة) يشتركان في القالب نفسه، فلا يُنشأ قالبان ولا يتسابقان على الكتابة.
        lock (TemplateGate)
        {
            if (_templateDatabase is not null)
                return _templateDatabase;

            var templateName = "docgen_app_tpl_" + Guid.NewGuid().ToString("N")[..12];
            // غير مُجمَّع: `CREATE DATABASE … TEMPLATE` يرفض أي جلسة قائمة على القالب،
            // واتصال مجمَّع خامل بعد `EnsureCreated` كان يُفشل أول استنساخ.
            var connectionString = PostgresTestDatabase.Create(
                baseConnectionString, templateName, pooling: false);

            var options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql.CommandTimeout(180))
                .Options;
            using (var template = new DocGeneratorPostgresDbContext(options))
            {
                template.Database.EnsureCreated();
            }

            _baseConnectionString = baseConnectionString;
            _templateDatabase = templateName;
            RegisterExitHook();
            return templateName;
        }
    }

    private static void RegisterExitHook()
    {
        if (_exitHookRegistered)
            return;

        _exitHookRegistered = true;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DropAll();
    }

    private static void DropAll()
    {
        if (_baseConnectionString is null)
            return;

        string[] names;
        lock (CleanupGate)
        {
            names = [.. LiveDatabases];
            LiveDatabases.Clear();
        }

        // الإسقاط متوازٍ: عشرات قواعد الأصناف تُسقط عند الخروج، والتسلسل كان يؤخّر
        // إنهاء المضيف دقيقة كاملة (كل إسقاط ~0.8s على Windows).
        Parallel.ForEach(
            names,
            new ParallelOptions { MaxDegreeOfParallelism = 4 },
            name =>
            {
                try
                {
                    PostgresTestDatabase.Drop(_baseConnectionString, name);
                }
                catch
                {
                    // تنظيف أفضل جهد — لا يُفشل تشغيل الاختبارات.
                }
            });

        if (_templateDatabase is not null)
        {
            try
            {
                PostgresTestDatabase.Drop(_baseConnectionString, _templateDatabase);
            }
            catch
            {
                // كما أعلاه.
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        lock (CleanupGate)
        {
            LiveDatabases.Remove(_databaseName);
        }

        if (_baseConnectionString is not null)
            await PostgresTestDatabase.DropAsync(_baseConnectionString, _databaseName);
    }
}
