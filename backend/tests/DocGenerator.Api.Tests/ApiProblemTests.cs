using System.Text.Json;
using DocGenerator.Api.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Tests;

/// <summary>
/// عقد الخطأ الموحّد (P4-03): شكل الوثيقة على السلك، والرموز الافتراضية، وتطبيع
/// أسماء الحقول. الحراس المصدريّة (منع بناء أجسام خطأ يدويًا) في
/// <see cref="ErrorContractGuardTests"/>، والسلوك عبر المسار الكامل في
/// <see cref="ErrorContractIntegrationTests"/>.
/// </summary>
[Trait("Category", "Unit")]
public class ApiProblemTests
{
    [Fact]
    public void Create_SerializesTheMandatoryKeysOnly()
    {
        var problem = ApiProblem.Create(
            new DefaultHttpContext { TraceIdentifier = "trace-1" },
            StatusCodes.Status400BadRequest,
            ApiErrorCodes.ValidationFailed,
            "سنة غير صالحة");

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(problem));
        var root = json.RootElement;

        Assert.Equal(ApiProblem.DefaultType, root.GetProperty("type").GetString());
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal(ApiErrorCodes.ValidationFailed, root.GetProperty("code").GetString());
        Assert.Equal("سنة غير صالحة", root.GetProperty("message").GetString());
        Assert.Equal("trace-1", root.GetProperty("traceId").GetString());

        // لا مفاتيح فارغة: العقد لا يحمل title/detail/instance الإطارية، والحقول
        // الاختيارية تغيب بدل أن تُرسل null (كانت ستكسر حارس الجسم العربي).
        Assert.False(root.TryGetProperty("title", out _));
        Assert.False(root.TryGetProperty("detail", out _));
        Assert.False(root.TryGetProperty("instance", out _));
        Assert.False(root.TryGetProperty("field", out _));
        Assert.False(root.TryGetProperty("errors", out _));
        Assert.False(root.TryGetProperty("warningToken", out _));
    }

    [Fact]
    public void Create_IncludesOptionalKeysWhenProvided()
    {
        var problem = ApiProblem.Create(
            null,
            StatusCodes.Status400BadRequest,
            ApiErrorCodes.ValidationFailed,
            "اسم المستخدم مطلوب",
            field: "username",
            errors: new Dictionary<string, string[]> { ["username"] = ["اسم المستخدم مطلوب"] },
            warningToken: "w-1");

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(problem));
        var root = json.RootElement;

        Assert.Equal("username", root.GetProperty("field").GetString());
        Assert.Equal(
            "اسم المستخدم مطلوب",
            root.GetProperty("errors").GetProperty("username")[0].GetString());
        Assert.Equal("w-1", root.GetProperty("warningToken").GetString());
        Assert.False(root.TryGetProperty("traceId", out _));
    }

    [Fact]
    public void Create_FallsBackToArabicMessageAndStatusDerivedCode()
    {
        var problem = ApiProblem.Create(null, StatusCodes.Status404NotFound, message: "   ");

        Assert.Equal(ApiErrorCodes.NotFound, problem.Code);
        Assert.Equal(ApiErrorMessages.Fallback, problem.Message);
    }

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest, ApiErrorCodes.ValidationFailed)]
    [InlineData(StatusCodes.Status401Unauthorized, ApiErrorCodes.Unauthorized)]
    [InlineData(StatusCodes.Status403Forbidden, ApiErrorCodes.Forbidden)]
    [InlineData(StatusCodes.Status404NotFound, ApiErrorCodes.NotFound)]
    [InlineData(StatusCodes.Status409Conflict, ApiErrorCodes.Conflict)]
    [InlineData(StatusCodes.Status423Locked, ApiErrorCodes.LockedOut)]
    [InlineData(StatusCodes.Status429TooManyRequests, ApiErrorCodes.RateLimited)]
    [InlineData(StatusCodes.Status500InternalServerError, ApiErrorCodes.ServerError)]
    public void DefaultCodeFor_MapsEveryStatusUsedByTheApi(int status, string expected)
        => Assert.Equal(expected, ApiProblem.DefaultCodeFor(status));

    [Theory]
    [InlineData("Message", "message")]
    [InlineData("Request.FileNumber", "request.fileNumber")]
    [InlineData("Items[0].Name", "items[0].name")]
    [InlineData("$.message", "message")]
    [InlineData("$", "request")]
    [InlineData("", "request")]
    [InlineData(null, "request")]
    public void NormalizeFieldName_CamelCasesEachSegment(string? key, string expected)
        => Assert.Equal(expected, ApiProblem.NormalizeFieldName(key));

    [Fact]
    public void Result_SetsStatusAndProblemContentType()
    {
        var result = ApiProblem.Result(
            new DefaultHttpContext(),
            StatusCodes.Status403Forbidden,
            ApiErrorCodes.Forbidden,
            "غير مسموح");

        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal(ApiProblem.ContentType, Assert.Single(result.ContentTypes));
        var problem = Assert.IsType<ApiProblem>(result.Value);
        Assert.Equal(ApiErrorCodes.Forbidden, problem.Code);
    }

    [Fact]
    public async Task WriteAsync_WritesProblemJsonWithTheSameShape()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-2" };
        context.Response.Body = new MemoryStream();

        await ApiProblem.WriteAsync(
            context,
            StatusCodes.Status429TooManyRequests,
            ApiErrorCodes.RateLimited,
            "طلبات كثيرة");

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.StartsWith(ApiProblem.ContentType, context.Response.ContentType, StringComparison.Ordinal);

        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(ApiErrorCodes.RateLimited, json.RootElement.GetProperty("code").GetString());
        Assert.Equal(429, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("trace-2", json.RootElement.GetProperty("traceId").GetString());
        Assert.Equal("طلبات كثيرة", json.RootElement.GetProperty("message").GetString());
    }
}
