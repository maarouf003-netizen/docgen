using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DocGenerator.Api.Tests;

/// <summary>
/// `P2-03` عبر المسار الكامل: مضيف إنتاجي بإعداد أمني مكتمل يقلع، وأي راية غير آمنة
/// تفشل الإقلاع برسالة تسمّي المفتاح — لا تحذير يُتجاهل.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class ProductionPostureIntegrationTests : IClassFixture<ProductionSpaFactory>
{
    private readonly ProductionSpaFactory _factory;

    public ProductionPostureIntegrationTests(ProductionSpaFactory factory) => _factory = factory;

    [PostgresTheory]
    [InlineData("Security:HstsEnabled", "false")]
    [InlineData("Security:HttpsRedirectionEnabled", "false")]
    [InlineData("Security:CspReportOnly", "true")]
    [InlineData("AllowedHosts", "*")]
    [InlineData("Swagger:Enabled", "true")]
    public void InsecureProductionSetting_RefusesStartup(string key, string value)
    {
        using var factory = WithSettings((key, value));

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains(key, exception.ToString(), StringComparison.Ordinal);
    }

    [PostgresFact]
    public void CompliantProductionSettings_StartHost()
    {
        // المصنع مهيّأ أصلًا بوضع آمن (HSTS/إعادة توجيه/CSP صارم/مضيفون/بروكسي موثوق).
        using var client = _factory.CreateClient();

        Assert.NotNull(client);
    }

    [PostgresFact]
    public async Task Production_DoesNotAllowLocalhostCors()
    {
        // P2-03: سياسة `Vite` (localhost + credentials) للتطوير فقط؛ الإنتاج يخدم SPA
        // من الأصل نفسه، فلا يجوز أن يعيد أي ترويسة CORS لأصل محلي.
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Origin", "http://localhost:5173");

        using var response = await client.SendAsync(request);

        Assert.DoesNotContain(
            response.Headers.Select(header => header.Key),
            key => key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase));
    }

    private WebApplicationFactory<Program> WithSettings(params (string Key, string Value)[] settings)
        => _factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);
        });
}
