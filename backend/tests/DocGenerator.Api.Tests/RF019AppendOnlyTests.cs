using System.Net.Http.Json;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبارات RF-019 (SEC-008): سجل التدقيق إلحاق-فقط على مستوى القاعدة.
/// تفشل قبل الإصلاح (التعديل/الحذف المباشر ينجح) وتخضر بعده.
/// القاعدة مهاجَرة عند الإقلاع (`MigrateAsync`) فتحمل المشغّلات بعد الإصلاح.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class RF019AppendOnlyTests
{
    private readonly ApiFactory _factory;

    public RF019AppendOnlyTests(ApiFactory factory) => _factory = factory;

    private async Task SeedFieldChangeAsync(string token)
    {
        var client = _factory.WithToken(token);
        var id = await _factory.CreateDocumentAsync(token, borrowerName: "تدقيق ثابت");
        var update = await client.PutAsJsonAsync($"/api/documents/{id}", new
        {
            borrowerName = "تدقيق متغير",
        });
        update.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task UpdateAuditLog_Throws()
    {
        await _factory.LoginAsync("lawyer1", "123456");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();

        var log = await db.AuditLogs.FirstAsync();
        log.Details = "تزوير";
        db.AuditLogs.Update(log);
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("إلحاق", ex.ToString());
    }

    [Fact]
    public async Task DeleteAuditLog_Throws()
    {
        await _factory.LoginAsync("lawyer1", "123456");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();

        var log = await db.AuditLogs.FirstAsync();
        db.AuditLogs.Remove(log);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task UpdateFieldChange_Throws()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        await SeedFieldChangeAsync(token);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();

        var change = await db.DocumentFieldChanges.FirstAsync();
        change.NewValue = "تزوير";
        db.DocumentFieldChanges.Update(change);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task DeleteFieldChange_Throws()
    {
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        await SeedFieldChangeAsync(token);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();

        var change = await db.DocumentFieldChanges.FirstAsync();
        db.DocumentFieldChanges.Remove(change);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task InsertAuditLog_StillWorks()
    {
        // توصيف (يخضر قبل/بعد): الإدخال والقراءة العاديان لا يمسّهما المنع.
        var token = (await _factory.LoginAsync("lawyer1", "123456"))!.Token!;
        var id = await _factory.CreateDocumentAsync(token, borrowerName: "تدقيق حي");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.ActionType == "create"));
        Assert.True(id > 0);
    }
}
