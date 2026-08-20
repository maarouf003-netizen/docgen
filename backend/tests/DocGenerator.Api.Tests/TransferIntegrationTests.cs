using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

[Collection(ApiTestCollection.Name)]
public class TransferIntegrationTests
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

    private Task<int> LawyerIdAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        return Task.FromResult(db.Users.Single(u => u.Username == username).Id);
    }

    private sealed record TransferAllResponse(int TransferredCount);

    private sealed record CountResponse(int Count);

    [Theory]
    [InlineData("lawyer1")]
    [InlineData("manager")]
    [InlineData("admin")]
    public async Task TransferAll_NonHeadRoles_Forbidden(string username)
    {
        var lawyerToken = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        await _factory.CreateDocumentAsync(lawyerToken);
        var sourceId = await LawyerIdAsync("lawyer1");
        var target = await CreateLawyerAsync("DAM", "محامي النقل الجماعي");

        var client = _factory.AuthorizedClient(username);
        var response = await client.PostAsJsonAsync("/api/documents/transfer-all",
            new { sourceLawyerId = sourceId, targetLawyerId = target.Id });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TransferAll_Count_ByHead_ReturnsFileCount()
    {
        var source = await CreateLawyerAsync("DAM", "محامي العد");
        var token = (await _factory.LoginAsync(source.Username, "123456"))!.Token!;
        await _factory.CreateDocumentAsync(token);
        await _factory.CreateDocumentAsync(token);

        var head = _factory.AuthorizedClient("head1");
        var response = await head.GetAsync($"/api/documents/owner/{source.Id}/count");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CountResponse>();
        Assert.Equal(2, result!.Count);
    }

    [Fact]
    public async Task TransferAll_Count_FromAnotherBranch_BadRequest()
    {
        var aleppoLawyer = await CreateLawyerAsync("ALP", "محامي حلب الجماعي");
        var aleppoToken = (await _factory.LoginAsync(aleppoLawyer.Username, "123456"))!.Token!;
        await _factory.CreateDocumentAsync(aleppoToken, borrowerName: "ملف حلب جماعي");

        var head = _factory.AuthorizedClient("head1");
        var response = await head.GetAsync($"/api/documents/owner/{aleppoLawyer.Id}/count");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TransferAll_ByHead_Succeeds()
    {
        var source = await CreateLawyerAsync("DAM", "محامي النقل الجماعي");
        var token = (await _factory.LoginAsync(source.Username, "123456"))!.Token!;
        var doc1 = await _factory.CreateDocumentAsync(token, borrowerName: "نقل جماعي 1");
        var doc2 = await _factory.CreateDocumentAsync(token, borrowerName: "نقل جماعي 2");
        var target = await CreateLawyerAsync("DAM", "محامي الهدف الجماعي");

        var head = _factory.AuthorizedClient("head1");
        var response = await head.PostAsJsonAsync("/api/documents/transfer-all",
            new { sourceLawyerId = source.Id, targetLawyerId = target.Id });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TransferAllResponse>();
        Assert.Equal(2, result!.TransferredCount);

        var moved1 = await head.GetFromJsonAsync<DocumentResponse>($"/api/documents/{doc1}");
        var moved2 = await head.GetFromJsonAsync<DocumentResponse>($"/api/documents/{doc2}");
        Assert.Equal(target.Id, moved1!.CreatedById);
        Assert.Equal(target.Id, moved2!.CreatedById);
        Assert.Equal("محامي الهدف الجماعي", moved1.Lawyer);
        Assert.Equal("محامي الهدف الجماعي", moved1.CreatedByName);

        // سجل التدقيق: حدث من نوع "transfer" لكل ملف بالعبارة المطلوبة.
        var manager = _factory.AuthorizedClient("manager");
        var auditResponse = await manager.GetAsync("/api/audit-logs?userName=head1&perPage=50");
        var auditBody = await auditResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var transfers = auditBody!.RootElement.GetProperty("items").EnumerateArray()
            .Where(e => e.GetProperty("actionType").GetString() == "transfer")
            .Select(e => e.GetProperty("details").GetString() ?? string.Empty)
            .ToList();
        Assert.Equal(2, transfers.Count);
        Assert.All(transfers, d =>
        {
            Assert.Contains("تم إحالة هذا الملف إلى المحامي: محامي الهدف الجماعي بتاريخ", d);
            Assert.Contains("المنفذ عليه", d);
        });
        auditBody.Dispose();
    }

    [Fact]
    public async Task TransferAll_ToOtherBranchLawyer_BadRequest()
    {
        var lawyerToken = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        await _factory.CreateDocumentAsync(lawyerToken);
        var sourceId = await LawyerIdAsync("lawyer1");
        var foreign = await CreateLawyerAsync("ALP", "محامي حلب الجماعي");

        var head = _factory.AuthorizedClient("head1");
        var response = await head.PostAsJsonAsync("/api/documents/transfer-all",
            new { sourceLawyerId = sourceId, targetLawyerId = foreign.Id });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TransferAll_ToInactiveLawyer_BadRequest()
    {
        var lawyerToken = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        await _factory.CreateDocumentAsync(lawyerToken);
        var sourceId = await LawyerIdAsync("lawyer1");
        var target = await CreateLawyerAsync("DAM", "محامي موقوف جماعي");

        var admin = _factory.AuthorizedClient("admin");
        await admin.PatchAsJsonAsync($"/api/users/{target.Id}/active", new { isActive = false });

        var head = _factory.AuthorizedClient("head1");
        var response = await head.PostAsJsonAsync("/api/documents/transfer-all",
            new { sourceLawyerId = sourceId, targetLawyerId = target.Id });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TransferAll_SourceFromAnotherBranch_BadRequest()
    {
        var aleppoLawyer = await CreateLawyerAsync("ALP", "محامي حلب الجماعي");
        var aleppoToken = (await _factory.LoginAsync(aleppoLawyer.Username, "123456"))!.Token!;
        await _factory.CreateDocumentAsync(aleppoToken, borrowerName: "ملف حلب جماعي");
        var target = await CreateLawyerAsync("DAM", "محامي النقل الجماعي");

        var head = _factory.AuthorizedClient("head1");
        var response = await head.PostAsJsonAsync("/api/documents/transfer-all",
            new { sourceLawyerId = aleppoLawyer.Id, targetLawyerId = target.Id });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TransferAll_SourceIsTarget_BadRequest()
    {
        var lawyerToken = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        await _factory.CreateDocumentAsync(lawyerToken);
        var sourceId = await LawyerIdAsync("lawyer1");

        var head = _factory.AuthorizedClient("head1");
        var response = await head.PostAsJsonAsync("/api/documents/transfer-all",
            new { sourceLawyerId = sourceId, targetLawyerId = sourceId });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TransferAll_HeadWithoutBranch_Forbidden()
    {
        var branchlessHead = await _factory.CreateUserAsync("head_nobranch", UserRole.Head, branchId: null);
        var token = (await _factory.LoginAsync(branchlessHead.Username, "123456"))!.Token!;
        var client = _factory.WithToken(token);

        var transfer = await client.PostAsJsonAsync("/api/documents/transfer-all",
            new { sourceLawyerId = 1, targetLawyerId = 2 });
        Assert.Equal(HttpStatusCode.Forbidden, transfer.StatusCode);

        var count = await client.GetAsync("/api/documents/owner/1/count");
        Assert.Equal(HttpStatusCode.Forbidden, count.StatusCode);
    }
}
