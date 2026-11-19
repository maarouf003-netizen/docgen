using System.Text.Json;
using DocGenerator.Api.Tests.TestHelpers;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبار تثبيت RF-008 (SEC-011 + قرار BQ-022): عمر الجلسة 4 ساعات.
/// يفشل قبل الإصلاح (480) ويخضر بعده (240). قراءة exp-iat من الحمولة فقط.
/// </summary>
[Collection(PostgresApiTestCollection.Name)]
public class RF008SessionLifetimeTests : IAsyncLifetime
{
    private readonly PostgresApiFactory _factory;

    public RF008SessionLifetimeTests(PostgresApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [PostgresFact]
    public async Task Login_TokenLifetime_IsFourHours()
    {
        var login = await _factory.LoginAsync("lawyer1", "123456");
        Assert.NotNull(login!.Token);

        var payload = login.Token.Split('.')[1]
            .Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        using var json = JsonDocument.Parse(Convert.FromBase64String(payload));
        var lifetime = json.RootElement.GetProperty("exp").GetInt64()
            - json.RootElement.GetProperty("nbf").GetInt64();

        Assert.InRange(lifetime, 240 * 60 - 120, 240 * 60 + 120);
    }
}
