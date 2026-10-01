using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبارات إظهار RF-017 (SEC-001/SEC-002/SEC-009/SEC-015 + INT-005): مسارات الكتابة
/// الصامتة يجب أن تُدوَّن. تفشل قبل الإصلاح (لا صف) وتخضر بعده.
/// هوية فريدة لكل اختبار (لا تصادم مع القاعدة المشتركة)؛ الاستعلام من AuditLogs مباشرة.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class RF017SilentWriteAuditTests
{
    private readonly ApiFactory _factory;

    public RF017SilentWriteAuditTests(ApiFactory factory) => _factory = factory;

    private static string NewName(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 16, 40)];

    private Task<int> BranchIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return Task.FromResult(db.Branches.Single(b => b.Code == code).Id);
    }

    private Task<bool> HasAuditRowAsync(string actionType, string userName, string? detailsContains = null, int? documentId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return Task.FromResult(db.AuditLogs.Any(a =>
            a.ActionType == actionType
            && a.UserName == userName
            && (detailsContains == null || (a.Details != null && a.Details.Contains(detailsContains)))
            && (documentId == null || a.DocumentId == documentId)));
    }

    [Fact]
    public async Task Generate_WritesGenerateDocumentRow()
    {
        var user = await _factory.CreateUserAsync(NewName("lawyer_gen"), UserRole.Lawyer, await BranchIdAsync("DAM"));
        var token = (await _factory.LoginAsync(user.Username, "123456"))!.Token!;
        var client = _factory.WithToken(token);
        var docId = await _factory.CreateDocumentAsync(token, borrowerName: $"توليد {Guid.NewGuid():N}"[..30]);

        var response = await client.GetAsync($"/api/documents/{docId}/generate?template=004");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(await HasAuditRowAsync("generate_document", user.Username, "004", docId));
    }

    [Fact]
    public async Task Export_WritesExportDocumentsRow()
    {
        var admin = await _factory.CreateUserAsync(NewName("admin_exp"), UserRole.Admin);
        var client = _factory.AuthorizedClient(admin.Username);

        var response = await client.GetAsync("/api/documents/export");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.True(await HasAuditRowAsync("export_documents", admin.Username));
    }

    [Fact]
    public async Task Logout_WritesLogoutRow()
    {
        var user = await _factory.CreateUserAsync(NewName("lawyer_out"), UserRole.Lawyer, await BranchIdAsync("DAM"));
        var login = await _factory.LoginAsync(user.Username, "123456");
        Assert.Equal((int)HttpStatusCode.OK, login!.StatusCode);

        var logout = await login.Client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        Assert.True(await HasAuditRowAsync("logout", user.Username));
    }

    [Fact]
    public async Task MarkSuggestionRead_WritesReadRow()
    {
        var lawyer = await _factory.CreateUserAsync(NewName("lawyer_sug"), UserRole.Lawyer, await BranchIdAsync("DAM"));
        var lawyerClient = _factory.AuthorizedClient(lawyer.Username);
        var created = await lawyerClient.PostAsJsonAsync("/api/app-suggestions", new { message = "اقتراح صامت للتدقيق" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var body = await created.Content.ReadFromJsonAsync<JsonDocument>();
        var id = body!.RootElement.GetProperty("id").GetInt32();

        var admin = await _factory.CreateUserAsync(NewName("admin_sug"), UserRole.Admin);
        var adminClient = _factory.AuthorizedClient(admin.Username);
        var read = await adminClient.PatchAsync($"/api/app-suggestions/{id}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);

        Assert.True(await HasAuditRowAsync("app-suggestion.read", admin.Username));
    }

    [Fact]
    public async Task CompleteDelegation_AuditDetailsContainSalePrice()
    {
        var source = await _factory.CreateUserAsync(NewName("lawyer_src"), UserRole.Lawyer, await BranchIdAsync("DAM"));
        var sourceToken = (await _factory.LoginAsync(source.Username, "123456"))!.Token!;
        var sourceClient = _factory.WithToken(sourceToken);
        var docId = await _factory.CreateDocumentAsync(sourceToken, borrowerName: $"منيب بيع {Guid.NewGuid():N}"[..30],
            borrowerFather: "أب", borrowerFamily: "العائلة", withEstate: true, registered: true);
        int assetId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            assetId = db.Assets.Single(a => a.DocumentId == docId).Id;
        }

        var created = await sourceClient.PostAsJsonAsync($"/api/documents/{docId}/delegations", new
        {
            delegatedCourt = "دائرة تنفيذ حلب",
            isExternal = false,
            externalBranchId = (int?)null,
            delegationDate = "1/8/2026",
            delegationText = "الإنابة على العقار",
            depositBookNumber = "كتاب-1",
            depositBookDate = "2/8/2026",
            assetIds = new[] { assetId },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var delegation = await created.Content.ReadFromJsonAsync<DelegationDto>();

        var head = _factory.AuthorizedClient("head1");
        var target = await _factory.CreateUserAsync(NewName("lawyer_tgt"), UserRole.Lawyer, await BranchIdAsync("DAM"));
        var assign = await head.PostAsJsonAsync($"/api/delegations/{delegation!.Id}/assign",
            new { assignedLawyerId = target.Id });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        var targetClient = _factory.AuthorizedClient(target.Username);
        var register = await targetClient.PostAsJsonAsync($"/api/delegations/{delegation.Id}/register",
            new { fileNumber = "890", fileYear = "2026", fileRegistrationDate = "5/8/2026" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var registered = await register.Content.ReadFromJsonAsync<DelegationDto>();
        var assetDto = registered!.Assets.Single();

        var complete = await targetClient.PostAsJsonAsync($"/api/delegations/{delegation.Id}/complete", new
        {
            returnDate = "10/8/2026",
            sales = new[] { new { delegationAssetId = assetDto.Id, salePrice = 750000m } },
            forcedExecutionDate = "12/8/2026",
            saleCoversFullDebt = true,
        });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        Assert.True(await HasAuditRowAsync("complete_delegation", target.Username, "750000", docId));
    }
}
