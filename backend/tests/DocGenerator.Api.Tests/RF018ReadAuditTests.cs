using System.Net;
using System.Net.Http.Json;
using static DocGenerator.Api.Tests.TestHelpers.ApiFixtureExtensions;
using System.Text.Json;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبارات إظهار RF-018 (SEC-003 + قرار BQ-021): فتح التفاصيل يُدوَّن، والقوائم لا.
/// تفشل قبل الإصلاح (لا صف) وتخضر بعده.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class RF018ReadAuditTests
{
    private readonly ApiFactory _factory;

    public RF018ReadAuditTests(ApiFactory factory) => _factory = factory;


    private Task<int> CountViewRowsAsync(string userName, int documentId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return Task.FromResult(db.AuditLogs.Count(a =>
            a.ActionType == "view_document" && a.UserName == userName && a.DocumentId == documentId));
    }

    [Fact]
    public async Task GetDetail_WritesViewRow()
    {
        var user = await _factory.CreateUserAsync(UniqueName("lawyer_rd"), UserRole.Lawyer, await _factory.GetBranchIdAsync("DAM"));
        var token = (await _factory.LoginAsync(user.Username, "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var docId = await _factory.CreateDocumentAsync(token, borrowerName: $"مقروء {Guid.NewGuid():N}"[..30]);

        var response = await client.GetAsync($"/api/documents/{docId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(1, await CountViewRowsAsync(user.Username, docId));
    }

    [Fact]
    public async Task SearchList_WritesNoViewRows()
    {
        var user = await _factory.CreateUserAsync(UniqueName("lawyer_sr"), UserRole.Lawyer, await _factory.GetBranchIdAsync("DAM"));
        var token = (await _factory.LoginAsync(user.Username, "123456"))!.Token!;
        var client = _factory.WithToken(token);
        await _factory.CreateDocumentAsync(token, borrowerName: $"قائمة {Guid.NewGuid():N}"[..30]);

        var response = await client.GetAsync("/api/documents?perPage=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        Assert.Equal(0, db.AuditLogs.Count(a => a.ActionType == "view_document" && a.UserName == user.Username));
    }
}
