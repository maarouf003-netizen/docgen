using System.Net;
using System.Text;
using DocGenerator.Api.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocGenerator.Api.Tests;

/// <summary>
/// W1 follow-up: `RequestSizeLimit(16KB)` على جامع `CSP` لا يُفرَض في `TestServer`،
/// فيُقاس على `Kestrel` حقيقي بمنفذ ديناميكي — الحمولة الأكبر تُرد `413` قبل التحليل.
/// </summary>
public class CspReportPayloadLimitTests
{
    [Fact]
    public async Task PayloadOver16KB_Returns413FromKestrel()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(CspReportsController).Assembly);

        await using var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();

        try
        {
            var address = Assert.Single(app.Urls);
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            const string path = "/api/csp-report";

            using var accepted = await client.PostAsync(
                path,
                new StringContent("{}", Encoding.UTF8, "application/csp-report"));
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);

            var oversized = "{\"csp-report\":{\"document-uri\":\"" + new string('a', 20_000) + "\"}}";
            using var rejected = await client.PostAsync(
                path,
                new StringContent(oversized, Encoding.UTF8, "application/csp-report"));

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, rejected.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
