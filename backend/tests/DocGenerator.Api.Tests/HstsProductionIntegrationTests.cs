using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests;

/// <summary>
/// W0-CSP follow-up: ربط <c>HSTS</c> في مضيف إنتاجي حقيقي. <c>TestServer</c> يثبّت مخطط الطلب
/// على <c>http</c> (لا يقبل <c>https</c> من <c>BaseAddress</c>)، فإرسال الترويسة نفسه يُقاس في
/// <see cref="HstsHeaderTests"/> على <c>HttpContext</c> مخططه <c>https</c>؛ وهنا يُقاس ما يخص
/// التركيب: انتقال الرايات إلى <see cref="HstsOptions"/>، وحجب الترويسة على <c>HTTP</c>، ورفض
/// الإقلاع عند <c>preload</c> بلا تفعيل.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class HstsProductionIntegrationTests : IClassFixture<ProductionSpaFactory>
{
    private readonly ProductionSpaFactory _factory;

    public HstsProductionIntegrationTests(ProductionSpaFactory factory) => _factory = factory;

    [Fact]
    public void EnabledHost_RegistersConfiguredHstsOptions()
    {
        using var factory = WithSettings(
            ("Security:HstsEnabled", "true"),
            ("Security:HstsMaxAgeDays", "730"),
            ("Security:HstsIncludeSubDomains", "true"),
            ("Security:HstsPreload", "true"));

        var options = factory.Services.GetRequiredService<IOptions<HstsOptions>>().Value;

        Assert.Equal(TimeSpan.FromDays(730), options.MaxAge);
        Assert.True(options.IncludeSubDomains);
        Assert.True(options.Preload);
    }

    [Fact]
    public async Task HttpDocument_DoesNotCarryHstsEvenWhenEnabled()
    {
        using var factory = WithSettings(("Security:HstsEnabled", "true"));
        var client = factory.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact]
    public void PreloadWithoutEnabled_FailsHostStartup()
    {
        using var factory = WithSettings(
            ("Security:HstsEnabled", "false"),
            ("Security:HstsPreload", "true"));

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("HstsEnabled", exception.ToString(), StringComparison.Ordinal);
    }

    private WebApplicationFactory<Program> WithSettings(params (string Key, string Value)[] settings)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);
        });
    }
}
