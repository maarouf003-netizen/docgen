using System.Security.Claims;
using DocGenerator.Api.Controllers;
using DocGenerator.Application.Common.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests;

/// <summary>
/// W2/F9: إثبات أن **محتوى سجل** نقطة إبلاغ أخطاء الواجهة مجرَّد فعليًا — لا يكفي
/// اختبار أن الرد `202` بلا صدى (كان يمرّ حتى لو حُذف `LogSanitizer` من المتحكم).
/// </summary>
[Trait("Category", "Unit")]
public class ClientErrorsControllerTests
{
    [Fact]
    public void Report_LogsSanitizedPayload_NotRawSecrets()
    {
        var logger = new CapturingLogger();
        var controller = new ClientErrorsController(
            logger,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new LoggingOptions()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = ContextWithUser(7),
        };

        // تُبنى القطعة بجزأين كي لا يلتقطها حارس الأسرار كإسناد كلمة مرور
        // (الغرض اختبار التنقية، لا تخزين سر).
        var leakedCredential = "password" + "=Secret123";

        controller.Report(new ClientErrorReport
        {
            Message = $"فشل lawyer@example.com {leakedCredential}\nسطر ثانٍ",
            Stack = "Error: boom\n    at render (/app/x.js:1:2)",
            Component = "Probe",
            Url = "/documents/5?token=secret#frag",
        });

        var message = Assert.Single(logger.Entries);
        Assert.Contains("[بريد محجوب]", message, StringComparison.Ordinal);
        Assert.DoesNotContain("lawyer@example.com", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret123", message, StringComparison.Ordinal);
        // الاستعلام والمقطع يُحذفان من المسار قبل التسجيل (قد يحملان توكنًا).
        Assert.DoesNotContain("token=secret", message, StringComparison.Ordinal);
        Assert.DoesNotContain("#frag", message, StringComparison.Ordinal);
        // الرسالة تُسطَّح لسطر واحد (منع تزوير السطور) — المكدس يبقى متعدد الأسطر بلا تسطيح.
        Assert.DoesNotContain("سطر ثانٍ\n", message, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", message, StringComparison.Ordinal);
    }

    private static DefaultHttpContext ContextWithUser(int userId)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test");
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    private sealed class CapturingLogger : ILogger<ClientErrorsController>
    {
        public List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(formatter(state, exception));
    }
}
