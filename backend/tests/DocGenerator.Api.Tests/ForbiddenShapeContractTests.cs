using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// عقود شكل 403 بعد R0-6 (قرار المالك: `{ message }` من المعالج العام بدل
/// `Forbid()` الفارغ): رفض النطاق المتوقع يُرد 403 مع جسم JSON يحمل رسالة
/// غير فارغة — مثبت هنا ضد انزلاق الشكل مستقبلًا.
/// </summary>
public sealed class ForbiddenShapeContractTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private string _lawyer1 = string.Empty;
    private string _lawyer2 = string.Empty;
    private string _delegate = string.Empty;

    public async Task InitializeAsync()
    {
        var lawyer1 = Unique("law1");
        var lawyer2 = Unique("law2");
        var del = Unique("del");
        await _factory.CreateUserAsync(lawyer1, UserRole.Lawyer, await BranchIdAsync());
        await _factory.CreateUserAsync(lawyer2, UserRole.Lawyer, await BranchIdAsync());
        await _factory.CreateUserAsync(del, UserRole.EntityManager);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var creator = await db.Users.SingleAsync(u => u.Username == lawyer1);
        var group = new PublicEntityGroup
        {
            CanonicalName = "وزارة العقد",
            EntityType = PublicEntityTypeCatalog.Ministry,
        };
        group.Entries.Add(new PublicEntity
        {
            Governorate = "دمشق",
            BranchName = "الفرع الرئيسي",
            Status = EntityStatusCatalog.Final,
            CreatedById = creator.Id,
        });
        db.PublicEntityGroups.Add(group);
        await db.SaveChangesAsync();
        (await db.Users.SingleAsync(u => u.Username == del)).PortalEntryId =
            group.Entries.First().Id;
        await db.SaveChangesAsync();

        _lawyer1 = lawyer1;
        _lawyer2 = lawyer2;
        _delegate = del;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static string Unique(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..24];

    private async Task<int> BranchIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var branch = await db.Branches.SingleAsync(b => b.Code == "DAM");
        if (string.IsNullOrWhiteSpace(branch.Governorate))
        {
            branch.Governorate = "دمشق";
            await db.SaveChangesAsync();
        }
        return branch.Id;
    }

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.GetProperty("message").GetString() ?? string.Empty;
    }

    [Fact]
    public async Task Portal_Files_WithOutOfScopeEntryId_Returns403WithMessage()
    {
        var client = _factory.AuthorizedClient(_delegate);

        var response = await client.GetAsync("/api/portal/files?entryId=2147483647");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await ReadMessageAsync(response)));
    }

    [Fact]
    public async Task ReviewLetters_Create_OnForeignDocument_Returns403WithMessage()
    {
        var token1 = (await _factory.LoginAsync(_lawyer1, "123456"))!.Token!;
        var docId = await _factory.CreateDocumentAsync(token1);
        var lawyer2 = _factory.AuthorizedClient(_lawyer2);

        var response = await lawyer2.PostAsJsonAsync("/api/review-letters", new
        {
            documentId = docId,
            bodyHtml = "<p>محتوى كتاب التجربة</p>",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await ReadMessageAsync(response)));
    }
}
