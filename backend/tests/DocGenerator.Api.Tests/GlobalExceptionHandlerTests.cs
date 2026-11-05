using System.Text.Json;
using DocGenerator.Api.Http;
using DocGenerator.Api.Middleware;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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

    private static GlobalExceptionHandler Handler(
        IDbExceptionClassifier? dbErrors = null,
        ILogger<GlobalExceptionHandler>? logger = null)
        => new(logger ?? LoggerFactory.Create(_ => { }).CreateLogger<GlobalExceptionHandler>(),
            new StubEnvironment(),
            dbErrors ?? new StubDbErrors());

    /// <summary>
    /// ملتقط سجلات المعالج: يثبت أن ردود <c>4xx</c> المتوقعة لا تُسجل <c>Error</c>
    /// (كي لا تلوّث لوحات المراقبة) وأن الأعطال الحقيقية تبقى <c>Error</c>.
    /// </summary>
    private sealed class CapturingLogger : ILogger<GlobalExceptionHandler>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class StubDbErrors(bool unique = false) : IDbExceptionClassifier
    {
        public bool IsUniqueViolation(Exception ex) => unique;
        public bool IsConcurrencyViolation(Exception ex) => false;
        public bool IsForeignKeyViolation(Exception ex) => false;
    }

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
    public async Task ArgumentError_EmptyMessage_UsesArabicFallback()
    {
        // تكافؤ الرسالة بين مساري المتحكم والوسيط: النص الفارغ بعد التطهير يُستبدل
        // بالرسالة البديلة نفسها، فلا يصل جسم `400` بلا `message`.
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await Handler().TryHandleAsync(
            context, new ArgumentException(""), CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        var message = JsonDocument.Parse(await ReadBodyAsync(context))
            .RootElement.GetProperty("message").GetString();
        Assert.Equal(ApiErrorMessages.Fallback, message);
    }

    [Fact]
    public async Task ArgumentError_LogsInformation_NotError()
    {
        // حذف كتل `catch` من المتحكمات لا يحوّل تحقّقًا متوقعًا إلى خطأ أحمر:
        // كل `4xx` (غير 499/403) يُسجَّل Information.
        var logger = new CapturingLogger();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await Handler(logger: logger).TryHandleAsync(
            context, new ArgumentException("مدخل خاطئ"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information);
    }

    [Fact]
    public async Task ServerError_StillLogsError()
    {
        // التصنيف الجديد محصور في `4xx`: العطل الحقيقي يبقى Error.
        var logger = new CapturingLogger();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await Handler(logger: logger).TryHandleAsync(
            context, new InvalidOperationException("عطل داخلي"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error);
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

    [Fact]
    public async Task UniqueViolation_MapsTo409WithGenericMessage()
    {
        // W1-7: تفرد غير ملتقط = تعارض بيانات 409 لا 500، وبلا كشف اسم القيد/الجدول.
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var ex = new DbUpdateException(
            "فشل حفظ التغييرات",
            new InvalidOperationException(
                "SQLite Error 19: 'UNIQUE constraint failed: PublicEntities.BranchName'"));

        await Handler(new StubDbErrors(unique: true)).TryHandleAsync(
            context, ex, CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        var message = JsonDocument.Parse(await ReadBodyAsync(context))
            .RootElement.GetProperty("message").GetString();
        Assert.Equal("تعارض في البيانات — القيمة مكررة، حدّث الصفحة وحاول مجددًا", message);
        Assert.DoesNotContain("UNIQUE", message);
        Assert.DoesNotContain("SQLite", message);
    }

    [Fact]
    public async Task NonUniqueDbUpdateException_Stays500()
    {
        // التصنيف الدقيق: أي عطل حفظ غير تفرد يبقى 500 (لا تُخفى الأعطال الحقيقية).
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await Handler(new StubDbErrors(unique: false)).TryHandleAsync(
            context,
            new DbUpdateException("فشل حفظ", new InvalidOperationException("انقطاع اتصال")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    [Fact]
    public async Task SqliteUniqueViolation_WithRealClassifier_MapsTo409()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var ex = new DbUpdateException(
            "فشل حفظ",
            new Microsoft.Data.Sqlite.SqliteException("UNIQUE constraint failed", 19, 2067));

        await Handler(new DbExceptionClassifier()).TryHandleAsync(
            context, ex, CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }

    [Fact]
    public async Task SqliteForeignKeyViolation_WithRealClassifier_Stays500()
    {
        // H-1: الرمز الأساسي 19 عام — خرق المفتاح الأجنبي ليس تفردًا.
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var ex = new DbUpdateException(
            "فشل حفظ",
            new Microsoft.Data.Sqlite.SqliteException("FOREIGN KEY constraint failed", 19, 787));

        await Handler(new DbExceptionClassifier()).TryHandleAsync(
            context, ex, CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    private static async Task<string> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }
}
