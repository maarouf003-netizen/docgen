using DocGenerator.Application.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests;

/// <summary>
/// يحرس إعداد قاعدة بيانات مضيف الاختبار المشترك: الترحيل التلقائي مفعّل صراحةً
/// في <see cref="ApiFactory"/> لأن <c>appsettings.Development.json</c> مستبعد من Git
/// (إعدادات محلية)، وقيمة <c>appsettings.json</c> الأساسية <c>false</c> عمدًا للإنتاج.
/// غياب التفعيل لا يظهر محليًا (الملف موجود) لكنه يُسقط كل اختبارات القاعدة في CI.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class ApiFactoryDatabaseConfigurationTests
{
    private readonly ApiFactory _factory;

    public ApiFactoryDatabaseConfigurationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void AutoMigrate_IsEnabledExplicitlyInTheTestHost()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        Assert.True(
            options.AutoMigrate,
            "مضيف الاختبار يجب أن يفعّل Database:AutoMigrate صراحةً: "
            + "appsettings.Development.json غير موجود في CI وappsettings.json يضبطها false.");
    }
}
