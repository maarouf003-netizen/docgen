using System.Net;
using System.Net.Http.Json;
using static DocGenerator.Api.Tests.TestHelpers.ApiFixtureExtensions;
using System.Text.Json;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using DocGenerator.Api.Tests.TestHelpers;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبارات إظهار RF-018 (SEC-003 + قرار BQ-021): فتح التفاصيل يُدوَّن، والقوائم لا.
/// تفشل قبل الإصلاح (لا صف) وتخضر بعده.
/// </summary>
[Collection(PostgresApiTestCollection.Name)]
public class RF018ReadAuditTests : IAsyncLifetime
{
    private readonly PostgresApiFactory _factory;

    public RF018ReadAuditTests(PostgresApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;


    private Task<int> CountViewRowsAsync(string userName, int documentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return Task.FromResult(db.AuditLogs.Count(a =>
            a.ActionType == "view_document" && a.UserName == userName && a.DocumentId == documentId));
    }

    [PostgresFact]
    public async Task GetDetail_WritesViewRow()
    {
        var user = await _factory.CreateUserAsync(UniqueName("lawyer_rd"), UserRole.Lawyer, await _factory.GetBranchIdAsync("DAM"));
        var token = (await _factory.LoginAsync(user.Username, "123456"))!.Token!;
        var client = _factory.ClientWithToken(token);
        var docId = await _factory.CreateDocumentAsync(token, borrowerName: $"مقروء {Guid.NewGuid():N}"[..30]);

        var response = await client.GetAsync($"/api/documents/{docId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(1, await CountViewRowsAsync(user.Username, docId));
    }

    [PostgresFact]
    public async Task SearchList_WritesNoViewRows()
    {
        var user = await _factory.CreateUserAsync(UniqueName("lawyer_sr"), UserRole.Lawyer, await _factory.GetBranchIdAsync("DAM"));
        var token = (await _factory.LoginAsync(user.Username, "123456"))!.Token!;
        var client = _factory.ClientWithToken(token);
        await _factory.CreateDocumentAsync(token, borrowerName: $"قائمة {Guid.NewGuid():N}"[..30]);

        var response = await client.GetAsync("/api/documents?perPage=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        Assert.Equal(0, db.AuditLogs.Count(a => a.ActionType == "view_document" && a.UserName == user.Username));
    }
}
