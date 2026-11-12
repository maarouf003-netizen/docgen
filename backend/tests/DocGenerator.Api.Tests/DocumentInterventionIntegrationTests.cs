using System.Net;
using System.Net.Http.Json;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DocGenerator.Api.Tests;

/// <summary>
/// عقد مسارات «المتدخلين» عبر الـAPI الحقيقي: الحفظ الفوري، الوقعة البنفسجية،
/// تعديلها/حذفها، وحراسة الصلاحيات (المحامي المالك فقط) وتحقق المدخلات.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class DocumentInterventionIntegrationTests
{
    private readonly ApiFactory _factory;

    public DocumentInterventionIntegrationTests(ApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, int DocumentId)> CreateDocAsync()
    {
        var login = await _factory.LoginAsync("lawyer1", "123456");
        var token = login!.Token!;
        var documentId = await _factory.CreateDocumentAsync(token, registered: true);
        return (_factory.ClientWithToken(token), documentId);
    }

    private static object NaturalPayload(string name = "سامر") => new
    {
        nature = PartyNatureCatalog.Natural,
        receiptNumber = "123",
        receiptDate = "1/8/2026",
        acceptanceDate = "10/8/2026",
        subject = "طلب تدخل",
        notes = "ملاحظة",
        name,
        father = "محمود",
        family = "الحسن",
        mother = "ليلى",
        birth = "دمشق 1980",
        register = "دمشق 123",
        nationalId = "01020304",
        addressType = "موطن مختار",
        address = "المزة",
        heirs = Array.Empty<object>(),
    };

    [Fact]
    public async Task Add_ThenDocumentReflectsInterventionAndOccurrence()
    {
        var (client, documentId) = await CreateDocAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/documents/{documentId}/interventions", NaturalPayload());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<DocumentInterventionDto>();
        Assert.NotNull(created);
        Assert.Equal("سامر محمود الحسن", created!.DisplayName);
        Assert.Equal("2026-08-01", created.ReceiptDate);

        var doc = await client.GetFromJsonAsync<DocumentResponse>($"/api/documents/{documentId}");
        Assert.NotNull(doc);
        var intervention = Assert.Single(doc!.Interventions);
        Assert.Equal(created.Id, intervention.Id);

        var occurrence = Assert.Single(doc.Occurrences, o => o.OccurrenceType == OccurrenceTypeCatalog.Intervention);
        Assert.Equal(created.Id, occurrence.InterventionId);
        Assert.Equal("طلب تدخل", occurrence.OccurrenceTypeLabel);
    }

    [Fact]
    public async Task Update_ThenDelete_SyncedOccurrenceAndPartyList()
    {
        var (client, documentId) = await CreateDocAsync();
        var added = await (await client.PostAsJsonAsync(
            $"/api/documents/{documentId}/interventions", NaturalPayload()))
            .Content.ReadFromJsonAsync<DocumentInterventionDto>();

        var update = await client.PutAsJsonAsync(
            $"/api/documents/{documentId}/interventions/{added!.Id}",
            NaturalPayload(name: "سامر سعيد"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<DocumentInterventionDto>();
        Assert.Equal("سامر سعيد محمود الحسن", updated!.DisplayName);

        var docAfterUpdate = await client.GetFromJsonAsync<DocumentResponse>($"/api/documents/{documentId}");
        var occurrence = Assert.Single(
            docAfterUpdate!.Occurrences, o => o.OccurrenceType == OccurrenceTypeCatalog.Intervention);
        Assert.Equal(added.Id, occurrence.InterventionId);

        var delete = await client.DeleteAsync(
            $"/api/documents/{documentId}/interventions/{added.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var docAfterDelete = await client.GetFromJsonAsync<DocumentResponse>($"/api/documents/{documentId}");
        Assert.Empty(docAfterDelete!.Interventions);
        Assert.DoesNotContain(
            docAfterDelete.Occurrences, o => o.OccurrenceType == OccurrenceTypeCatalog.Intervention);

        var deleteAgain = await client.DeleteAsync(
            $"/api/documents/{documentId}/interventions/{added.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deleteAgain.StatusCode);
    }

    [Fact]
    public async Task Add_WithoutReceiptDate_ReturnsBadRequestWithArabicMessage()
    {
        var (client, documentId) = await CreateDocAsync();
        var payload = new
        {
            nature = PartyNatureCatalog.Natural,
            receiptDate = " ",
            name = "سامر",
        };

        var response = await client.PostAsJsonAsync(
            $"/api/documents/{documentId}/interventions", payload);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("تاريخ ورود طلب التدخل", body);
    }

    [Fact]
    public async Task Head_CannotAddIntervention()
    {
        var (_, documentId) = await CreateDocAsync();
        var head = await _factory.LoginAsync("head1", "123456");
        var headClient = _factory.ClientWithToken(head!.Token!);

        var response = await headClient.PostAsJsonAsync(
            $"/api/documents/{documentId}/interventions", NaturalPayload());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OtherLawyer_CannotAddIntervention()
    {
        var (_, documentId) = await CreateDocAsync();
        int? branchId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGenerator.Infrastructure.Persistence.DocGeneratorDbContext>();
            branchId = db.Users.Single(u => u.Username == "lawyer1").BranchId;
        }
        await _factory.CreateUserAsync("lawyer2", DocGenerator.Domain.Enums.UserRole.Lawyer, branchId: branchId);
        var other = await _factory.LoginAsync("lawyer2", "123456");
        var otherClient = _factory.ClientWithToken(other!.Token!);

        var response = await otherClient.PostAsJsonAsync(
            $"/api/documents/{documentId}/interventions", NaturalPayload());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
