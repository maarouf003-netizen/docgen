using System.Net;
using System.Text;
using System.Text.Json;
using DocGenerator.Api.Http;
using DocGenerator.Api.Middleware;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Statistics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Api.Tests;

[Collection(ApiTestCollection.Name)]
public class ExceptionHandlerIntegrationTests
{
    private readonly ApiFactory _factory;

    public ExceptionHandlerIntegrationTests(ApiFactory factory) => _factory = factory;

    [PostgresFact]
    public async Task UnhandledException_ThroughPipeline_ReturnsJson500WithMessage()
    {
        var fake = new ThrowingDashboardStatsQueryHandler();
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IQueryHandler<GetDashboardStatsQuery, DashboardStatsDto>>(_ => fake)));

        var loginBody = JsonSerializer.Serialize(new { username = "lawyer1", password = "123456" });
        var loginResponse = await factory.CreateClient().PostAsync("/api/auth/login",
            new StringContent(loginBody, Encoding.UTF8, "application/json"));
        loginResponse.EnsureSuccessStatusCode();
        var token = ApiFactory.ExtractCookieValue(loginResponse, "docgen_token");

        var client = factory.CreateClient();
        client.SetAuthCookie(token!);

        var response = await client.GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(ApiProblem.ContentType, response.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(StatusCodes.Status500InternalServerError, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(ApiErrorCodes.ServerError, doc.RootElement.GetProperty("code").GetString());
        var message = doc.RootElement.GetProperty("message").GetString();
        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    [PostgresFact]
    public async Task ArgumentException_MapsTo400AndEchoesMessage()
    {
        var (status, body) = await HandleAsync(new ArgumentException("قيمة غير صالحة"));

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("قيمة غير صالحة", ReadMessage(body));
    }

    [PostgresFact]
    public async Task KeyNotFoundException_MapsTo404AndEchoesMessage()
    {
        var (status, body) = await HandleAsync(new KeyNotFoundException("المستند غير موجود"));

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Equal("المستند غير موجود", ReadMessage(body));
    }

    [PostgresFact]
    public async Task GenericException_Production_Returns500GenericMessageWithoutLeak()
    {
        var (status, body) = await HandleAsync(new InvalidOperationException("تفاصيل داخلية حساسة"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Equal("حدث خطأ غير متوقع في الخادم", ReadMessage(body));
        Assert.DoesNotContain("تفاصيل داخلية حساسة", body);
    }

    [PostgresFact]
    public async Task GenericException_Development_Returns500WithExceptionMessage()
    {
        var (status, body) = await HandleAsync(
            new InvalidOperationException("تفاصيل تصحيح"), environment: "Development");

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Equal("تفاصيل تصحيح", ReadMessage(body));
    }

    [PostgresFact]
    public async Task GenericException_Development_IsSanitizedLike4xx()
    {
        // مسار التطوير يُعرض للتشخيص لكنه ليس استثناءً من سياسة `SEC-012`.
        var (status, body) = await HandleAsync(
            new InvalidOperationException("اتصال user@example.com\nسطر ثانٍ"),
            environment: "Development");

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        var message = ReadMessage(body) ?? "";
        Assert.DoesNotContain('\n', message);
        Assert.DoesNotContain("user@example.com", message);
        Assert.Contains("[بريد محجوب]", message);
    }

    [PostgresFact]
    public async Task ClientDisconnect_Returns499WithoutBody()
    {
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            new FakeEnvironment { EnvironmentName = "Production" },
            new FakeDbErrors());
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/api/test";
        ctx.RequestAborted = aborted.Token;
        ctx.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(
            ctx, new OperationCanceledException(aborted.Token), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(499, ctx.Response.StatusCode);
        Assert.Equal(0, ctx.Response.Body.Length);
    }

    [PostgresFact]
    public async Task DbUpdateException_Development_ReturnsRealConstraintFromInnerException()
    {
        var inner = new InvalidOperationException(
            "23505: duplicate key value violates unique constraint \"IX_PublicEntities_GroupId_Governorate_BranchName\"");
        var ex = new DbUpdateException(
            "An error occurred while saving the entity changes. See the inner exception for details.", inner);

        var (status, body) = await HandleAsync(ex, environment: "Development");

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Contains("duplicate key value violates unique constraint", ReadMessage(body));
    }

    [PostgresFact]
    public async Task DbUpdateException_Development_PrefersDeepestInnerException()
    {
        var deepest = new InvalidOperationException(
            "23505: duplicate key value violates unique constraint \"IX_PublicEntities_BranchName\"");
        var wrapped = new InvalidOperationException("السبب الوسيط بلا فائدة تشخيصية", deepest);
        var ex = new DbUpdateException(
            "An error occurred while saving the entity changes. See the inner exception for details.", wrapped);

        var (status, body) = await HandleAsync(ex, environment: "Development");

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Contains("duplicate key value violates unique constraint", ReadMessage(body));
        Assert.DoesNotContain("السبب الوسيط", ReadMessage(body));
    }

    [PostgresFact]
    public async Task DbUpdateException_Development_SanitizesDeepestMessage()
    {
        var deep = new InvalidOperationException(
            "23505: duplicate key value violates unique constraint \"IX_Users_Email\"\nuser@example.com");
        var ex = new DbUpdateException(
            "An error occurred while saving the entity changes. See the inner exception for details.", deep);

        var (status, body) = await HandleAsync(ex, environment: "Development");

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        var message = ReadMessage(body) ?? "";
        Assert.Contains("duplicate key value violates unique constraint", message);
        Assert.DoesNotContain('\n', message);
        Assert.DoesNotContain("user@example.com", message);
    }

    [PostgresFact]
    public async Task DbUpdateException_Production_ReturnsGenericSaveMessageWithoutLeakingInner()
    {
        var ex = new DbUpdateException(
            "An error occurred while saving the entity changes. See the inner exception for details.",
            new InvalidOperationException("23505: duplicate key value violates unique constraint \"IX_PublicEntities_GroupId\""));

        var (status, body) = await HandleAsync(ex, environment: "Production");

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Equal("فشل حفظ التغييرات في قاعدة البيانات", ReadMessage(body));
        Assert.DoesNotContain("unique constraint", body);
    }

    [PostgresFact]
    public async Task DbUpdateException_UniqueViolation_MapsTo409WithGenericMessage()
    {
        // W1-7: حتى في Development لا تُعكس تفاصيل القيد للمستخدم — التفاصيل في السجل.
        var ex = new DbUpdateException(
            "An error occurred while saving the entity changes.",
            new InvalidOperationException("23505: duplicate key value violates unique constraint \"IX_Users_Email\""));

        var (status, body) = await HandleAsync(
            ex, environment: "Development", uniqueViolation: true);

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal("تعارض في البيانات — القيمة مكررة، حدّث الصفحة وحاول مجددًا", ReadMessage(body));
        Assert.DoesNotContain("unique constraint", body);
        Assert.DoesNotContain("IX_Users_Email", body);
    }

    [PostgresFact]
    public async Task DocumentConflictException_MapsTo409AndEchoesMessage()
    {
        var (status, body) = await HandleAsync(
            new DocumentConflictException("تغيّر المحامي المختص للملف أثناء النقل — أعد المحاولة"));

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal("تغيّر المحامي المختص للملف أثناء النقل — أعد المحاولة", ReadMessage(body));
    }

    [PostgresFact]
    public async Task DocumentConflictException_WithDbInnerChain_ProjectsFriendlyMessageWithoutLeak()
    {
        // الشكل الفعلي لمسار نفاد إعادة التوليد في المراسلات: غلاف الخدمة يحمل سلسلة
        // DbUpdateException ← سبب SQL العميق (انتهاك قيد فريد). المعالج يجب
        // أن يردّ رسالة 409 الودّية لا أن يُسرّب تفاصيل القيد/المزود في الإنتاج.
        var deep = new InvalidOperationException(
            "23505: duplicate key value violates unique constraint \"IX_Correspondences_CorrespondenceNumber\"");
        var ex = new DocumentConflictException(
            "تعذر توليد رقم فريد للمراسلة، حاول مجدداً",
            new DbUpdateException("An error occurred while saving the entity changes.", deep));

        var (status, body) = await HandleAsync(ex);

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal("تعذر توليد رقم فريد للمراسلة، حاول مجدداً", ReadMessage(body));
        Assert.DoesNotContain("unique constraint", body);
        Assert.DoesNotContain("IX_Correspondences", body);
    }

    [PostgresFact]
    public async Task ResponseAlreadyStarted_ReturnsFalse()
    {
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            new FakeEnvironment { EnvironmentName = "Production" },
            new FakeDbErrors());
        var ctx = new DefaultHttpContext();
        ctx.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/api/test";

        var handled = await handler.TryHandleAsync(ctx, new Exception("boom"), CancellationToken.None);

        Assert.False(handled);
    }

    private static async Task<(int Status, string Body)> HandleAsync(
        Exception exception, string environment = "Production", bool uniqueViolation = false)
    {
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            new FakeEnvironment { EnvironmentName = environment },
            new FakeDbErrors(uniqueViolation));
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/api/test";
        ctx.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(ctx, exception, CancellationToken.None);

        Assert.True(handled);
        var body = Encoding.UTF8.GetString(((MemoryStream)ctx.Response.Body).ToArray());
        return (ctx.Response.StatusCode, body);
    }

    private static string? ReadMessage(string body)
        => JsonDocument.Parse(body).RootElement.GetProperty("message").GetString();

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class FakeDbErrors(bool uniqueViolation = false, bool missingCircuit = false) : IDbExceptionClassifier
    {
        public bool IsUniqueViolation(Exception ex) => uniqueViolation;
        public bool IsConcurrencyViolation(Exception ex) => false;
        public bool IsForeignKeyViolation(Exception ex) => false;
        public bool IsMissingDocumentCircuitViolation(Exception ex) => missingCircuit;
        public bool IsAmountConstraintViolation(Exception ex) => false;
    }

    private sealed class StartedResponseFeature : IHttpResponseFeature
    {
        public int StatusCode { get; set; }
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted => true;
        public void OnStarting(Func<object, Task> callback, object state) { }
        public void OnCompleted(Func<object, Task> callback, object state) { }
    }

    private sealed class ThrowingDashboardStatsQueryHandler
        : IQueryHandler<GetDashboardStatsQuery, DashboardStatsDto>
    {
        public Task<DashboardStatsDto> HandleAsync(
            GetDashboardStatsQuery query, CancellationToken ct = default)
            => throw new InvalidOperationException("تعطلت قراءة الإحصاءات");
    }
}
