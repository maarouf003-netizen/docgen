using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DocGenerator.Api.Middleware;
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace DocGenerator.Api.Tests;

public class ExceptionHandlerIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ExceptionHandlerIntegrationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task UnhandledException_ThroughPipeline_ReturnsJson500WithMessage()
    {
        var fake = new ThrowingStatisticsService();
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IStatisticsService>(_ => fake)));

        var loginBody = JsonSerializer.Serialize(new { username = "lawyer1", password = "123456" });
        var loginResponse = await factory.CreateClient().PostAsync("/api/auth/login",
            new StringContent(loginBody, Encoding.UTF8, "application/json"));
        loginResponse.EnsureSuccessStatusCode();
        using var loginDoc = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var token = loginDoc.RootElement.GetProperty("token").GetString();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var message = doc.RootElement.GetProperty("message").GetString();
        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    [Fact]
    public async Task ArgumentException_MapsTo400AndEchoesMessage()
    {
        var (status, body) = await HandleAsync(new ArgumentException("قيمة غير صالحة"));

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("قيمة غير صالحة", ReadMessage(body));
    }

    [Fact]
    public async Task KeyNotFoundException_MapsTo404AndEchoesMessage()
    {
        var (status, body) = await HandleAsync(new KeyNotFoundException("المستند غير موجود"));

        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Equal("المستند غير موجود", ReadMessage(body));
    }

    [Fact]
    public async Task GenericException_Production_Returns500GenericMessageWithoutLeak()
    {
        var (status, body) = await HandleAsync(new InvalidOperationException("تفاصيل داخلية حساسة"));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Equal("حدث خطأ غير متوقع في الخادم", ReadMessage(body));
        Assert.DoesNotContain("تفاصيل داخلية حساسة", body);
    }

    [Fact]
    public async Task GenericException_Development_Returns500WithExceptionMessage()
    {
        var (status, body) = await HandleAsync(
            new InvalidOperationException("تفاصيل تصحيح"), environment: "Development");

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.Equal("تفاصيل تصحيح", ReadMessage(body));
    }

    [Fact]
    public async Task DocumentConflictException_MapsTo409AndEchoesMessage()
    {
        var (status, body) = await HandleAsync(
            new DocumentConflictException("تغيّر المحامي المختص للملف أثناء النقل — أعد المحاولة"));

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal("تغيّر المحامي المختص للملف أثناء النقل — أعد المحاولة", ReadMessage(body));
    }

    [Fact]
    public async Task ResponseAlreadyStarted_ReturnsFalse()
    {
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            new FakeEnvironment { EnvironmentName = "Production" });
        var ctx = new DefaultHttpContext();
        ctx.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/api/test";

        var handled = await handler.TryHandleAsync(ctx, new Exception("boom"), CancellationToken.None);

        Assert.False(handled);
    }

    private static async Task<(int Status, string Body)> HandleAsync(
        Exception exception, string environment = "Production")
    {
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            new FakeEnvironment { EnvironmentName = environment });
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

    private sealed class ThrowingStatisticsService : IStatisticsService
    {
        public Task<DashboardStatsDto> GetDashboardStatsAsync(int? branchId, CancellationToken ct = default)
            => throw new InvalidOperationException("تعطلت خدمة الإحصاءات");

        public Task<List<MonthlyStatDto>> GetMonthlyStatsAsync(int? branchId, CancellationToken ct = default)
            => throw new InvalidOperationException("تعطلت خدمة الإحصاءات");

        public Task<List<BranchSummaryDto>> GetBranchesSummaryAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("تعطلت خدمة الإحصاءات");

        public Task<List<UserActivityDto>> GetUserActivityAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("تعطلت خدمة الإحصاءات");

        public Task<List<ReminderDto>> GetRemindersAsync(int? branchId, int? userId, CancellationToken ct = default)
            => throw new InvalidOperationException("تعطلت خدمة الإحصاءات");

        public Task<ManagerStatsDto> GetManagerStatsAsync(StatsPeriod period, int? branchId, CancellationToken ct = default)
            => throw new InvalidOperationException("تعطلت خدمة الإحصاءات");

        public Task<List<ManagerLawyerStatDto>> GetManagerLawyerStatsAsync(StatsPeriod period, int branchId, CancellationToken ct = default)
            => throw new InvalidOperationException("تعطلت خدمة الإحصاءات");
    }
}
