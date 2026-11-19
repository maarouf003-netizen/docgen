using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Testing;

/// <summary>
/// قاعدة <c>PostgreSQL</c> مؤقتة لحالة اختبار واحدة (‏`ADR-0009`):
/// يُبنى **قالب واحد لكل تشغيل** بـ<c>EnsureCreated</c> من
/// <see cref="DocGeneratorPostgresDbContext"/>، ثم كل اختبار
/// يحصل على **نسخة مستنسخة** بـ<c>CREATE DATABASE … TEMPLATE</c> — كلفة منخفضة
/// وعزل كامل بلا مسح جداول مشترك.
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
    private static readonly SemaphoreSlim TemplateGate = new(1, 1);
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
            .UseNpgsql(connectionString)
            .Options;
    }

    /// <summary>سلسلة اتصال النسخة المعزولة (‏<c>Pooling=false</c>).</summary>
    public string ConnectionString { get; }

    /// <summary>خيارات سياق جاهزة فوق القاعدة نفسها (لاتصالات متعددة).</summary>
    public DbContextOptions<DocGeneratorPostgresDbContext> Options { get; }

    /// <summary>سياق جديد فوق القاعدة (الملكية للمستدعي: <c>await using</c>).</summary>
    public DocGeneratorPostgresDbContext CreateContext() => new(Options);

    /// <summary>ينشئ نسخة معزولة جديدة من القالب (قالب يُهيَّأ مرة واحدة لكل تشغيل).</summary>
    public static async Task<PostgresTestDb> CreateAsync()
    {
        var baseConnectionString = PostgresTestEnvironment.RequireOrSkip()
            ?? throw new InvalidOperationException(
                $"PostgresTestDb يتطلب {PostgresTestEnvironment.ConnectionStringVariable} — "
                + "علّم الاختبار بـ[PostgresFact] كي يُتخطّى صراحةً بدل الفشل.");

        var template = await EnsureTemplateAsync(baseConnectionString);
        var databaseName = "docgen_app_" + Guid.NewGuid().ToString("N")[..16];
        var connectionString = await PostgresTestDatabase.CloneAsync(
            baseConnectionString, template, databaseName);

        lock (CleanupGate)
        {
            LiveDatabases.Add(databaseName);
        }

        return new PostgresTestDb(connectionString, databaseName);
    }

    private static async Task<string> EnsureTemplateAsync(string baseConnectionString)
    {
        await TemplateGate.WaitAsync();
        try
        {
            if (_templateDatabase is not null)
                return _templateDatabase;

            _baseConnectionString = baseConnectionString;
            var templateName = "docgen_app_tpl_" + Guid.NewGuid().ToString("N")[..12];
            var connectionString = await PostgresTestDatabase.CreateAsync(
                baseConnectionString, templateName);

            var options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using (var template = new DocGeneratorPostgresDbContext(options))
            {
                await template.Database.EnsureCreatedAsync();
            }

            _templateDatabase = templateName;
            RegisterExitHook();
            return templateName;
        }
        finally
        {
            TemplateGate.Release();
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

        foreach (var name in names)
        {
            try
            {
                PostgresTestDatabase.DropAsync(_baseConnectionString, name).GetAwaiter().GetResult();
            }
            catch
            {
                // تنظيف أفضل جهد — لا يُفشل تشغيل الاختبارات.
            }
        }

        if (_templateDatabase is not null)
        {
            try
            {
                PostgresTestDatabase.DropAsync(_baseConnectionString, _templateDatabase)
                    .GetAwaiter().GetResult();
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
