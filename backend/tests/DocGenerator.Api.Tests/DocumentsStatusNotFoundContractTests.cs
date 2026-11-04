using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// توحيد ردود `404` في عمليات حالة المستند (`W1`): عندما تنجح قراءة المستند ثم يرفض
/// التنفيذ (سباق حذف/تغيّر حالة) يجب أن يحمل الرد جسم <c>{ message }</c> العربي —
/// لا جسمًا فارغًا يعجز العميل عن عرض سببه. تُعزل خدمة الحالات بردّ <c>false</c>
/// لمحاكاة السباق دون الاعتماد على توقيت حقيقي.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class DocumentsStatusNotFoundContractTests
{
    private const string ExpectedMessage = "المستند غير موجود";

    private readonly ApiFactory _factory;

    public DocumentsStatusNotFoundContractTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task StatusEndpoints_ServiceReturnsFalse_Return404WithArabicMessageBody()
    {
        var login = await _factory.LoginAsync("lawyer1", "123456");
        Assert.NotNull(login?.Token);
        var token = login!.Token!;
        var documentId = await _factory.CreateDocumentAsync(token);

        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDocumentStatusService));
                if (descriptor is not null)
                    services.Remove(descriptor);
                services.AddScoped<IDocumentStatusService>(_ => new FalseStatusService());
            }));

        var client = factory.CreateClient();
        client.SetAuthCookie(token);

        var responses = new[]
        {
            await client.PostAsJsonAsync($"/api/documents/{documentId}/status", new { status = "متداول" }),
            await client.PostAsJsonAsync($"/api/documents/{documentId}/revert-status", new { }),
            await client.PostAsJsonAsync($"/api/documents/{documentId}/return-referred-to-start", new { }),
            await client.PostAsJsonAsync(
                $"/api/documents/{documentId}/consider-executed-by-delegation",
                new { fields = new Dictionary<string, string?>() }),
            await client.PostAsJsonAsync($"/api/documents/{documentId}/executed-status", new { status = "منفذ" }),
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(ExpectedMessage, json.RootElement.GetProperty("message").GetString());
        }
    }

    /// <summary>خدمة حالات ترفض كل انتقال — تحاكي سباق الحذف بعد نجاح القراءة.</summary>
    private sealed class FalseStatusService : IDocumentStatusService
    {
        public Task<bool> UpdateStatusAsync(int documentId, string status, Dictionary<string, string?> fields,
            string? actorName, CancellationToken ct = default, long? version = null) =>
            Task.FromResult(false);

        public Task<bool> ConsiderExecutedByDelegationAsync(int documentId, Dictionary<string, string?> fields,
            string? actorName, CancellationToken ct = default, long? version = null) =>
            Task.FromResult(false);

        public Task<bool> RevertStatusAsync(int documentId, Dictionary<string, string?> fields,
            string? actorName, CancellationToken ct = default, long? version = null) =>
            Task.FromResult(false);

        public Task<bool> ReturnFromReferredToStartAsync(int documentId, ReturnReferredToStartRequest request,
            string? actorName, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<bool> UpdateExecutedStatusAsync(int documentId, string status, string? actorName,
            CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<bool> UpdateExecutedStatusAsync(int documentId, string status, ExecutedStatusRequest? request,
            string? actorName, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<bool> RestoreStruckOffAsync(int documentId, string? actorName, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<bool> RestoreStruckOffAsync(int documentId, RenewalRequest renewal, string? actorName,
            CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task ApplyRenewalAsync(Document doc, RenewalRequest? renewal, bool executedLike, int? userId,
            CancellationToken ct) =>
            Task.CompletedTask;

        public Task AddStruckOffOccurrenceAsync(Document doc, int? userId, CancellationToken ct) =>
            Task.CompletedTask;
    }
}
