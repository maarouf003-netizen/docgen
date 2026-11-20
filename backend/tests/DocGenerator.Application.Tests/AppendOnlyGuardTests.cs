using DocGenerator.Infrastructure.Persistence;
using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// P0-08 — إلحاق-only على مستوى التطبيق: أي تعديل/حذف لسجل تدقيق أو تغيّر حقلي
/// يُرفض قبل الوصول لقاعدة البيانات (`DbUpdateException`)، وتبقى مشغّلات القاعدة طبقة ثانية.
/// </summary>
public class AppendOnlyGuardTests : PostgresDbTest
{

    private AuditLog SeedLog()
    {
        var log = new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            UserName = "lawyer1",
            ActionType = "update_document",
            Details = "تعديل",
        };
        log.FieldChanges.Add(new DocumentFieldChange
        {
            DocumentId = 1,
            FieldKey = nameof(Document.BorrowerName),
            FieldLabel = "اسم المنفذ عليه",
            OldValue = "قديم",
            NewValue = "جديد",
        });
        _db.AuditLogs.Add(log);
        _db.SaveChanges();
        return log;
    }

    [PostgresFact]
    public async Task ModifiedAuditLog_IsRejectedBeforeDatabase()
    {
        var log = SeedLog();
        log.Details = "تزوير";

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
        Assert.Contains("إلحاق", ex.Message, StringComparison.Ordinal);
    }

    [PostgresFact]
    public async Task DeletedAuditLog_IsRejectedBeforeDatabase()
    {
        var log = SeedLog();
        _db.AuditLogs.Remove(log);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
        Assert.Contains("إلحاق", ex.Message, StringComparison.Ordinal);
    }

    [PostgresFact]
    public async Task ModifiedFieldChange_IsRejectedBeforeDatabase()
    {
        SeedLog();
        var change = await _db.DocumentFieldChanges.SingleAsync();
        change.NewValue = "تزوير";

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
        Assert.Contains("إلحاق", ex.Message, StringComparison.Ordinal);
    }
}
