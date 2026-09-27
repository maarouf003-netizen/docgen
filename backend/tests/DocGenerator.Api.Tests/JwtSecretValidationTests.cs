namespace DocGenerator.Api.Tests;

/// <summary>
/// صلابة سر <c>JWT</c> (S5): الإقلاع يُرفض عند غياب السر أو قصره عن 32 بايت (256 بت) —
/// لا سر افتراضي ولا مفتاح ضعيف في أي بيئة.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class JwtSecretValidationTests
{
    private readonly ApiFactory _factory;

    public JwtSecretValidationTests(ApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("1234567890123456789012345678901")]
    public void ShortJwtSecret_RefusesStartup(string secret)
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseSetting("Jwt:Secret", secret));
        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("Jwt:Secret", ex.ToString());
    }
}
