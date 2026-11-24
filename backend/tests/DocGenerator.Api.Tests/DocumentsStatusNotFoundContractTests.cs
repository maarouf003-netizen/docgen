using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Documents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// توحيد ردود `404` في عمليات حالة المستند (`W1`): عندما تنجح قراءة المستند ثم يرفض
/// التنفيذ (سباق حذف/تغيّر حالة) يجب أن يحمل الرد جسم <c>{ message }</c> العربي —
/// لا جسمًا فارغًا يعجز العميل عن عرض سببه. تُعزل أوامر الحالة بردّ <c>false</c>
/// لمحاكاة السباق دون الاعتماد على توقيت حقيقي (`P4-01bd`).
/// </summary>
[Collection(ApiTestCollection.Name)]
public class DocumentsStatusNotFoundContractTests
{
    private const string ExpectedMessage = "المستند غير موجود";

    private readonly ApiFactory _factory;

    public DocumentsStatusNotFoundContractTests(ApiFactory factory) => _factory = factory;

    [PostgresFact]
    public async Task StatusEndpoints_HandlersReturnFalse_Return404WithArabicMessageBody()
    {
        var login = await _factory.LoginAsync("lawyer1", "123456");
        Assert.NotNull(login?.Token);
        var token = login!.Token!;
        var documentId = await _factory.CreateDocumentAsync(token);

        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                ReplaceHandler<UpdateDocumentStatusCommand>(services);
                ReplaceHandler<RevertDocumentStatusCommand>(services);
                ReplaceHandler<ReturnFromReferredToStartCommand>(services);
                ReplaceHandler<ConsiderExecutedByDelegationCommand>(services);
                ReplaceHandler<UpdateExecutedStatusCommand>(services);
                ReplaceHandler<RestoreStruckOffDocumentCommand>(services);
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

    private static void ReplaceHandler<TCommand>(IServiceCollection services)
        where TCommand : ICommand<bool>
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ICommandHandler<TCommand, bool>));
        if (descriptor is not null)
            services.Remove(descriptor);
        services.AddScoped<ICommandHandler<TCommand, bool>>(_ => new FalseHandler<TCommand>());
    }

    /// <summary>معالج يرفض كل انتقال — يحاكي سباق الحذف بعد نجاح القراءة.</summary>
    private sealed class FalseHandler<TCommand> : ICommandHandler<TCommand, bool>
        where TCommand : ICommand<bool>
    {
        public Task<bool> HandleAsync(TCommand command, CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
