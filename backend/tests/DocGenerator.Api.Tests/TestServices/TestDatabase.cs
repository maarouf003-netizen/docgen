using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests.TestServices;

/// <summary>
/// حيز اختبار فقط لاستبدال تسجيل <see cref="DocGeneratorDbContext"/> بمزوّد SQLite.
///
/// منذ ADR-0001 لم يعد للتطبيق مسار SQLite إنتاجي: <c>AddInfrastructure</c> يسجّل Npgsql
/// حصرًا، ومضيف الاختبار يمرّر سلسلة Postgres شكلية (بلا اتصال) لتجاوز حارس الإقلاع ثم
/// يستبدل السياق هنا. تُزال هذه الأداة مع بقية SQLite في P1-08 بعد انتقال الاختبارات
/// التكاملية إلى PostgreSQL (P1-01).
/// </summary>
public static class TestDatabase
{
    /// <summary>
    /// سلسلة Postgres شكلية صالحة نحويًا — لا يُفتح بها اتصال. وجودها يلبي حارس الإقلاع
    /// في <c>Program.cs</c> فقط، لأن السياق الفعلي يُستبدل بـSQLite في <see cref="UseSqlite"/>.
    /// </summary>
    public const string PostgresPlaceholder =
        "Host=localhost;Port=5432;Database=docgen_test;Username=placeholder";

    /// <summary>
    /// يزيل تسجيلات سياق Postgres ثم يسجّل <see cref="DocGeneratorDbContext"/> على SQLite.
    /// </summary>
    public static void UseSqlite(IServiceCollection services, string connectionString)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var serviceType = services[i].ServiceType;
            if (serviceType == typeof(DocGeneratorDbContext)
                || serviceType == typeof(DocGeneratorPostgresDbContext)
                || serviceType == typeof(DbContextOptions)
                || serviceType == typeof(DbContextOptions<DocGeneratorDbContext>)
                || serviceType == typeof(DbContextOptions<DocGeneratorPostgresDbContext>)
                || serviceType == typeof(IDatabaseInitializer))
            {
                services.RemoveAt(i);
            }
        }

        services.AddScoped<DocGeneratorDbContext>(_ =>
        {
            var options = new DbContextOptionsBuilder<DocGeneratorDbContext>()
                .UseSqlite(connectionString, sqlite =>
                    sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                .Options;
            return new DocGeneratorDbContext(options);
        });
        services.AddScoped<IDatabaseInitializer, EnsureCreatedDatabaseInitializer>();
    }
}
