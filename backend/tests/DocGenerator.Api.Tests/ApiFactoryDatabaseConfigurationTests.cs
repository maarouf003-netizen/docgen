using DocGenerator.Application.Common;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests;

/// <summary>
/// يحرس إعداد قاعدة مضيف الاختبار المشترك بعد P0-02: المخطط يُبنى بـ<see cref="EnsureCreatedDatabaseInitializer"/>
/// (EnsureCreated) لا بالهجرات، والترحيل التلقائي معطَّل صراحةً.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class ApiFactoryDatabaseConfigurationTests
{
    private readonly ApiFactory _factory;

    public ApiFactoryDatabaseConfigurationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void AutoMigrate_IsDisabled_AndEnsureCreatedSchemaIsPresent()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        Assert.False(options.AutoMigrate, "المضيف الاختباري يبني المخطط بـEnsureCreated لا بالهجرات.");

        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        Assert.True(db.Branches.Any(), "مخطط EnsureCreated لم يُنشأ قبل التهيئة/البذر.");
    }
}
