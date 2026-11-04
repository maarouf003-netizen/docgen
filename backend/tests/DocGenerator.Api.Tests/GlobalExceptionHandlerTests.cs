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

    [Fact]
    public async Task ClientErrorMessage_SanitizedBeforeReflection()
    {
        // `SEC-012`: رسالة `4xx` المنعكسة تُسطَّح وتُجرَّد وتُقصّ — لا مدخلات خام للمستخدم.
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await Handler().TryHandleAsync(
            context,
            new ArgumentException("مدخل user@example.com خاطئ\nسطر ثانٍ" + new string('z', 2000)),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        var body = JsonDocument.Parse(await ReadBodyAsync(context)).RootElement;
        var message = body.GetProperty("message").GetString() ?? "";
        Assert.DoesNotContain('\n', message);
        Assert.DoesNotContain("user@example.com", message);
        Assert.Contains("[بريد محجوب]", message);
        Assert.True(message.Length <= LogSanitizer.ResponseMessageLimit);
    }

    [Fact]
    public async Task DevelopmentMessage_SanitizedBeforeReflection()
    {
        // حتى مسار التطوير لا يعكس رسالة خام: التسطيح/التجريد/القصّ يمنعان
        // تزوير السطور وتسريب الأسرار إن شُغّل `Development` على خادم منشور بالخطأ.
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await Handler().TryHandleAsync(
            context,
            new InvalidOperationException(
                "فشل الاتصال بـ user@example.com\nAuthorization: Bearer abc.def" + new string('z', 2000)),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        var body = JsonDocument.Parse(await ReadBodyAsync(context)).RootElement;
        var message = body.GetProperty("message").GetString() ?? "";
        Assert.DoesNotContain('\n', message);
        Assert.DoesNotContain("user@example.com", message);
        Assert.DoesNotContain("abc.def", message);
        Assert.Contains("[بريد محجوب]", message);
        Assert.True(message.Length <= LogSanitizer.ResponseMessageLimit);
    }

    [Fact]
    public async Task ClientDisconnect_MapsTo499WithoutWritingBody()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();

        var context = new DefaultHttpContext();
        context.RequestAborted = aborted.Token;
        context.Response.Body = new MemoryStream();

        var handled = await Handler().TryHandleAsync(
            context, new OperationCanceledException(aborted.Token), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(499, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task ServerSideCancellation_WithoutClientAbort_Stays500()
    {
        // إلغاء داخلي (مهلة خادم) ليس انقطاع عميل: يبقى 500 كي لا يُخفى عطل حقيقي.
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await Handler().TryHandleAsync(
            context, new OperationCanceledException("انتهت المهلة"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    private static async Task<string> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }
}
