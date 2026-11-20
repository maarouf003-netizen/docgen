namespace DocGenerator.Api.Tests.TestServices;

/// <summary>
/// قاعدة <c>PostgreSQL</c> معزولة لمصنع مضيف واحد: تستنسخ القالب **المهاجَر** المشترك
/// (~1s) بدل تشغيل الهجرات في كل مصنع، ثم تُسقط النسخة عند التخلص. بديل
/// <c>TestDatabase</c> بعد إزالة <c>SQLite</c> من الاختبارات (‏`P1-08/8c`).
///
/// <para>`migrate: false` يعطي قاعدة فارغة تمامًا (بلا مخطط) — يحتاجها اختبار
/// <c>DatabaseInitializer</c> الذي يتحقق من مسار الهجرات الكامل بنفسه.</para>
///
/// <para><b>كل المسارات متزامنة عن قصد</b> (‏<c>Create</c>/<c>Dispose</c>): تُستدعى من
/// خيط xUnit الذي يلتقط <c>SynchronizationContext</c>؛ أي <c>await</c> داخلي يعود إلى
/// السياق المحجوب ⇒ جمود (السبب الجذري الموثَّق في `docs/audit/80-…` §3).</para>
/// </summary>
public sealed class PostgresHostDatabase : IDisposable
{
    /// <summary>سلسلة اتصال شكلية صالحة نحويًا — للحراس الذين يرفضون الإقلاع قبل أي اتصال.</summary>
    public const string PlaceholderConnectionString =
        "Host=localhost;Port=5432;Database=docgen_guard;Username=placeholder";

    private readonly PostgresTestDb? _clone;
    private readonly string? _baseConnectionString;
    private readonly string? _databaseName;
    private bool _disposed;

    private PostgresHostDatabase(PostgresTestDb clone)
    {
        _clone = clone;
        ConnectionString = clone.ConnectionString;
    }

    private PostgresHostDatabase(string baseConnectionString, string databaseName, string connectionString)
    {
        _baseConnectionString = baseConnectionString;
        _databaseName = databaseName;
        ConnectionString = connectionString;
    }

    /// <summary>سلسلة اتصال القاعدة المعزولة الجاهزة لتسليمها للمضيف.</summary>
    public string ConnectionString { get; }

    /// <summary>
    /// ينشئ قاعدة معزولة: بالافتراضي نسخة من القالب المهاجَر (مخطط جاهز)، أو قاعدة
    /// فارغة عند <paramref name="migrate"/> = <c>false</c>.
    /// </summary>
    public static PostgresHostDatabase Create(bool migrate = true)
    {
        if (migrate)
            return new PostgresHostDatabase(PostgresTestDb.CreateMigrated());

        var baseConnectionString = PostgresTestEnvironment.RequireOrSkip()
            ?? throw new InvalidOperationException(
                $"PostgresHostDatabase يتطلب {PostgresTestEnvironment.ConnectionStringVariable} "
                + "لقاعدة اختبار حيّة — لا تُشغَّل هذه المجموعة على SQLite.");

        var databaseName = "docgen_host_" + Guid.NewGuid().ToString("N")[..16];
        var connectionString = PostgresTestDatabase.Create(baseConnectionString, databaseName);

        return new PostgresHostDatabase(baseConnectionString, databaseName, connectionString);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_clone is not null)
        {
            _clone.Dispose();
            return;
        }

        if (_baseConnectionString is null || _databaseName is null)
            return;

        try
        {
            PostgresTestDatabase.Drop(_baseConnectionString, _databaseName);
        }
        catch
        {
            // لا تُفشل جولة الاختبار بسبب تنظيف قاعدة مؤقتة.
        }
    }
}
