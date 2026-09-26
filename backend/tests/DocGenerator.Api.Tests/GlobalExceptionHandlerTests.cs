using System.Text.Json;
using DocGenerator.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocGenerator.Api.Tests;

/// <summary>
/// تعيين المعالج العام: الرفض الصريح (`UnauthorizedAccessException`) يُردّ 403
/// لا 500 — فأي رمي يفلت من المتحكمات (وسائط المصادقة/الحماية) يبقى رفض وصول.
/// </summary>
public sealed class GlobalExceptionHandlerTests
{
    private sealed class StubEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(AppContext.BaseDirectory);
    }

    private static GlobalExceptionHandler Handler()
        => new(LoggerFactory.Create(_ => { }).CreateLogger<GlobalExceptionHandler>(),
            new StubEnvironment());

    [Fact]
    public async Task UnauthorizedAccess_ReturnsForbidden_WithReason()
    {
        var context = new DefaultHttpContext();
        // جسم الذاكرة: الافتراضي `Stream.Null` لا يُقرأ بعد الكتابة.
        context.Response.Body = new MemoryStream();

        var handled = await Handler().TryHandleAsync(
            context, new UnauthorizedAccessException("مرفوض"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        var body = JsonDocument.Parse(await ReadBodyAsync(context)).RootElement;
        Assert.Equal("مرفوض", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ArgumentError_StillReturnsBadRequest()
    {
        // تثبيت التعيينات القائمة: لا يتزحزح 400/404/409 بإضافة 403.
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await Handler().TryHandleAsync(
            context, new ArgumentException("مدخل خاطئ"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    private static async Task<string> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }
}
