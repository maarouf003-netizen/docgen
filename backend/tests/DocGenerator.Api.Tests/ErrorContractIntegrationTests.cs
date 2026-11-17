using System.Net;
using System.Text;
using System.Text.Json;
using DocGenerator.Api.Http;
using Microsoft.AspNetCore.Http;

namespace DocGenerator.Api.Tests;

/// <summary>
/// عقد الخطأ عبر المسار الكامل (P4-03): فشل ربط النموذج، وJSON غير صالح، و404 من
/// متحكم — كلها تحمل الوثيقة نفسها (<c>status</c>/<c>code</c>/<c>message</c>/<c>traceId</c>)
/// بنوع وسائط <c>application/problem+json</c> وبلا <c>title</c>/<c>detail</c> إنجليزيين.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class ErrorContractIntegrationTests
{
    private readonly ApiFactory _factory;

    public ErrorContractIntegrationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task InvalidModelState_ReturnsUnifiedProblemWithFieldErrors()
    {
        var client = _factory.AuthorizedClient("lawyer1");

        var response = await client.PostAsync(
            "/api/client-errors",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var root = await ReadProblemAsync(response);

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal(ApiErrorCodes.ValidationFailed, root.GetProperty("code").GetString());
        Assert.Equal("message", root.GetProperty("field").GetString());
        Assert.Equal("الرسالة إلزامية", root.GetProperty("message").GetString());
        Assert.Equal(
            "الرسالة إلزامية",
            root.GetProperty("errors").GetProperty("message")[0].GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.False(root.TryGetProperty("title", out _));
        Assert.False(root.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task MalformedJson_ReturnsUnifiedProblemWithArabicMessage()
    {
        var client = _factory.AuthorizedClient("lawyer1");

        var response = await client.PostAsync(
            "/api/client-errors",
            new StringContent("{", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var root = await ReadProblemAsync(response);

        Assert.Equal(ApiErrorCodes.ValidationFailed, root.GetProperty("code").GetString());
        Assert.Equal("request", root.GetProperty("field").GetString());
        var message = root.GetProperty("message").GetString();
        // نصوص ربط النموذج الإطارية إنجليزية، فلا تُعرض: البديل العربي إلزامي.
        Assert.Equal(ApiErrorMessages.Fallback, message);
        Assert.False(root.TryGetProperty("title", out _));
    }

    [Fact]
    public async Task ControllerNotFound_ReturnsUnifiedProblem()
    {
        var client = _factory.AuthorizedClient("lawyer1");

        var response = await client.GetAsync("/api/alerts/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var root = await ReadProblemAsync(response);

        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Equal(ApiErrorCodes.NotFound, root.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
        Assert.False(root.TryGetProperty("title", out _));
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(ApiProblem.ContentType, response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
