using DocGenerator.Application.Common;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests;

/// <summary>
/// يحرس إعداد قاعدة مضيف الاختبار المشترك بعد `P1-08/8b`: قاعدة `PostgreSQL` حقيقية
/// تُبنى **بالهجرات** (‏`AutoMigrate=true`) لا بـ`EnsureCreated`، ثم تُبذر بيانات
/// التطوير — فما يمرّ هنا يمرّ على مخطط الإنتاج نفسه.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class ApiFactoryDatabaseConfigurationTests
{
    private readonly ApiFactory _factory;

    public ApiFactoryDatabaseConfigurationTests(ApiFactory factory) => _factory = factory;

    [PostgresFact]
    public void AutoMigrate_IsEnabled_AndMigratedSchemaIsPresent()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        Assert.True(options.AutoMigrate, "مضيف التكامل يبني المخطط بالهجرات الحقيقية.");

        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        Assert.True(db.Branches.Any(), "المخطط المهاجَر لم يُنشأ قبل التهيئة/البذر.");
        Assert.Contains(
            db.Database.GetAppliedMigrations(),
            migration => migration.EndsWith("InitialPostgresBaseline", StringComparison.Ordinal));
    }
}
