using System.Net;
using System.Net.Http.Json;
using DocGenerator.Application.DTOs;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

public class TransferIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TransferIntegrationTests(ApiFactory factory) => _factory = factory;

    private Task<int> BranchIdAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return Task.FromResult(db.Branches.Single(b => b.Code == code).Id);
    }

    private async Task<LawyerListItemDto> CreateLawyerAsync(string branchCode, string fullName)
    {
        var admin = _factory.AuthorizedClient("admin");
        var username = $"l_{Guid.NewGuid():N}"[..20];
        var response = await admin.PostAsJsonAsync("/api/users/lawyers", new
        {
            username,
            fullName,
            password = "123456",
            branchId = await BranchIdAsync(branchCode),
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LawyerListItemDto>())!;
    }

    [Theory]
    [InlineData("lawyer1")]
    [InlineData("manager")]
    public async Task Transfer_NonHeadRoles_Forbidden(string username)
    {
        var lawyerToken = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var docId = await _factory.CreateDocumentAsync(lawyerToken);

        var client = _factory.AuthorizedClient(username);
        var response = await client.PostAsJsonAsync($"/api/documents/{docId}/transfer", new { targetLawyerId = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_ByHead_ToOwnBranchLawyer_Succeeds()
    {
        var lawyerToken = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var docId = await _factory.CreateDocumentAsync(lawyerToken, borrowerName: "نقل ناجح");
        var target = await CreateLawyerAsync("DAM", "محامي النقل");

        var headToken = (await _factory.LoginAsync("head1", "123456"))!.Token!;
        var head = _factory.WithToken(headToken);
        var response = await head.PostAsJsonAsync($"/api/documents/{docId}/transfer", new { targetLawyerId = target.Id });
        response.EnsureSuccessStatusCode();

        var doc = await response.Content.ReadFromJsonAsync<DocumentResponse>();
        Assert.Equal(target.Id, doc!.CreatedById);
        Assert.Equal("محامي النقل", doc.Lawyer);
        Assert.Equal("محامي النقل", doc.CreatedByName);
    }

    [Fact]
    public async Task Transfer_ToCurrentOwner_BadRequest()
    {
        var lawyerToken = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var lawyer = _factory.WithToken(lawyerToken);
        var docId = await _factory.CreateDocumentAsync(lawyerToken);
        var doc = await lawyer.GetFromJsonAsync<DocumentResponse>($"/api/documents/{docId}");

        var head = _factory.AuthorizedClient("head1");
        var response = await head.PostAsJsonAsync($"/api/documents/{docId}/transfer", new { targetLawyerId = doc!.CreatedById });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_ToOtherBranchLawyer_BadRequest()
    {
        var lawyerToken = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var docId = await _factory.CreateDocumentAsync(lawyerToken);
        var foreign = await CreateLawyerAsync("ALP", "محامي حلب");

        var headToken = (await _factory.LoginAsync("head1", "123456"))!.Token!;
        var head = _factory.WithToken(headToken);
        var response = await head.PostAsJsonAsync($"/api/documents/{docId}/transfer", new { targetLawyerId = foreign.Id });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_NonExistentDocument_NotFound()
    {
        var head = _factory.AuthorizedClient("head1");
        var response = await head.PostAsJsonAsync("/api/documents/999999/transfer", new { targetLawyerId = 1 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_DocumentFromAnotherBranch_Forbidden()
    {
        var admin = _factory.AuthorizedClient("admin");
        var username = $"l_{Guid.NewGuid():N}"[..20];
        var created = await admin.PostAsJsonAsync("/api/users/lawyers", new
        {
            username,
            fullName = "محامي حلب",
            password = "123456",
            branchId = await BranchIdAsync("ALP"),
        });
        var aleppoLawyer = (await created.Content.ReadFromJsonAsync<LawyerListItemDto>())!;

        var aleppoToken = (await _factory.LoginAsync(username, "123456"))!.Token!;
        var docId = await _factory.CreateDocumentAsync(aleppoToken, borrowerName: "ملف حلب");

        var head = _factory.AuthorizedClient("head1");
        var response = await head.PostAsJsonAsync($"/api/documents/{docId}/transfer", new { targetLawyerId = aleppoLawyer.Id });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
