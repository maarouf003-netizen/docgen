using DocGenerator.Api.Tests.TestHelpers;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Respawn.Graph;

namespace DocGenerator.Api.Tests;

/// <summary>
/// مصنع تكامل على <c>PostgreSQL</c> حقيقي: قاعدة فريدة لكل مصنع، وهجرات EF الفعلية
/// (لا <c>EnsureCreated</c>)، وبذر التطوير نفسه، ثم <c>Respawn</c> لإعادة الضبط بين
/// الاختبارات. يتطلب <c>DOCGEN_TEST_POSTGRES</c>؛ وفي <c>CI</c> يفرض
/// <c>DOCGEN_REQUIRE_POSTGRES=1</c> فشلًا صريحًا بدل التخطي الصامت.
/// </summary>
public sealed class PostgresApiFactory : ApiFactory
{
    private string? _baseConnectionString;
    private string? _connectionString;
    private string? _databaseName;

    public PostgresApiFactory() : base(usePostgres: true)
    {
    }

    /// <summary>سلسلة اتصال القاعدة المعزولة لهذا المصنع (بعد بناء المضيف).</summary>
    public string ConnectionString =>
        _connectionString
        ?? throw new InvalidOperationException("لم تُهيَّأ قاعدة Postgres بعد.");

    protected override void ConfigureDatabaseSettings(IWebHostBuilder builder)
    {
        // WebApplicationFactory قد يبني المضيف أكثر من مرة (WithWebHostBuilder)؛
        // نعيد استخدام القاعدة نفسها بدل إنشاء قاعدة جديدة في كل نداء.
        if (_connectionString is not null)
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
            builder.UseSetting("Database:AutoMigrate", "true");
            return;
        }

        _baseConnectionString = PostgresTestEnvironment.RequireOrSkip()
            ?? throw new InvalidOperationException(
                $"PostgresApiFactory يتطلب {PostgresTestEnvironment.ConnectionStringVariable} "
                + "لقاعدة اختبار حيّة — لا تُشغَّل هذه المجموعة على SQLite.");

        _databaseName = "docgen_it_" + Guid.NewGuid().ToString("N")[..12];
        _connectionString = PostgresTestDatabase
            .CreateAsync(_baseConnectionString, _databaseName)
            .GetAwaiter().GetResult();

        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        // الهجرات الحقيقية مقصودة: هذا المسار يختبر مخطط الإنتاج لا EnsureCreated.
        builder.UseSetting("Database:AutoMigrate", "true");
    }

    protected override void ConfigureDatabaseServices(IServiceCollection services)
    {
        // لا استبدال: يبقى DocGeneratorPostgresDbContext وDatabaseInitializer الحقيقيان،
        // فيطبّق Startup الهجرات ثم يبذر البيانات كما يفعل التطبيق.
    }

    /// <summary>
    /// يفرّغ كل الجداول (عدا سجل الهجرات) عبر <c>Respawn</c> ثم يعيد بذر بيانات
    /// التطوير الثابتة، فيبدأ كل اختبار من حالة معروفة بلا الاعتماد على ترتيب التنفيذ.
    /// </summary>
    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        var respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            TablesToIgnore = [new Table("public", "__EFMigrationsHistory")],
            WithReseed = true,
        });
        await respawner.ResetAsync(connection);

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await DbSeeder.SeedAsync(db, hasher, devPassword: "123456");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || _databaseName is null || _baseConnectionString is null)
            return;

        try
        {
            PostgresTestDatabase.DropAsync(_baseConnectionString, _databaseName)
                .GetAwaiter().GetResult();
        }
        catch
        {
            // لا تُفشل جولة الاختبار بسبب تنظيف قاعدة مؤقتة.
        }
    }
}
