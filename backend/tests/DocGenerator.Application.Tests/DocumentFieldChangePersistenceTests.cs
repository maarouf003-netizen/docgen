using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// تكامل سجل تعديلات الحقول: حفظ صفوف التغييرات مع إدخال التدقيق،
/// وترقيم مجموعات «سجل التعديلات» لملف محدد.
/// </summary>
public class DocumentFieldChangePersistenceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly AuditLogger _logger;
    private readonly IAuditLogRepository _logs;

    public DocumentFieldChangePersistenceTests()
    {
        _db = TestDb.Create();
        _logger = new AuditLogger(_db);
        _logs = new AuditLogRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    private static DocumentFieldChange Change(string key, string label, string? oldV, string? newV)
        => new() { FieldKey = key, FieldLabel = label, OldValue = oldV, NewValue = newV };

    [Fact]
    public async Task LogDocumentChangeAsync_PersistsRowsLinkedToAuditEntry()
    {
        await _logger.LogDocumentChangeAsync("محامي", "update", 7, "سند دين",
            "عدّل المستند (رقم 7) — غيّر 2 حقلًا",
        [
            Change(nameof(Document.BorrowerName), "اسم المنفذ عليه", "أحمد", "أحمد محمد"),
            Change("__Col_Guarantors", "الكفلاء", null, "خالد زكي"),
        ]);

        var stored = await _db.AuditLogs.Include(a => a.FieldChanges).SingleAsync();
        Assert.Equal(7, stored.DocumentId);
        Assert.Equal("update", stored.ActionType);
        Assert.Equal(2, stored.FieldChanges.Count);
        Assert.All(stored.FieldChanges, c => { Assert.Equal(stored.Id, c.AuditLogId); Assert.Equal(7, c.DocumentId); });
    }

    [Fact]
    public async Task LogDocumentChangeAsync_WithEmptyChanges_FallsBackToPlainLog()
    {
        await _logger.LogDocumentChangeAsync("محامي", "update", 3, null, "عدّل المستند", []);

        var stored = await _db.AuditLogs.SingleAsync();
        Assert.Empty(await _db.DocumentFieldChanges.ToListAsync());
        Assert.Equal("عدّل المستند", stored.Details);
    }

    [Fact]
    public async Task PageDocumentChangeGroups_ReturnsNewestFirst_WithRowsPaged()
    {
        for (var round = 1; round <= 3; round++)
        {
            await _logger.LogDocumentChangeAsync($"مستخدم{round}", "update", 42, null,
                $"تعديل جولة {round}",
                [Change(nameof(Document.Notes), "الملاحظات", $"قديم{round}", $"جديد{round}")]);
            // إدخال تدقيق آخر بلا تغييرات حقول يجب ألا يظهر في المجموعات
            await _logger.LogAsync($"مستخدم{round}", "create", 42, null, "أنشأ");
        }

        var page1 = await _logs.PageDocumentChangeGroupsAsync(42, page: 1, perPage: 2);
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.True(page1.Items[0].Id > page1.Items[1].Id);
        Assert.Single(page1.Items[0].FieldChanges);

        var page2 = await _logs.PageDocumentChangeGroupsAsync(42, page: 2, perPage: 2);
        Assert.Single(page2.Items);

        // ملف آخر لا يتسرّب إليه شيء
        Assert.Equal(0, (await _logs.PageDocumentChangeGroupsAsync(99, 1, 10)).TotalCount);
    }
}
