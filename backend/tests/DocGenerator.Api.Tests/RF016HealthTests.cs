namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبار إظهار RF-016 (ARC-008): نقطة الصحة متاحة بلا مصادقة.
/// يفشل قبل الإصلاح (404) ويخضر بعده (200 + Healthy).
/// </summary>
public class RF016HealthTests
{
    [Fact]
    public async Task Healthz_WithoutAuth_ReturnsHealthy()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", await response.Content.ReadAsStringAsync());
    }
}
