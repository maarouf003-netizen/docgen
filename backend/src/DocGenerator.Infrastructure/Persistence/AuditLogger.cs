using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// يسجل أحداث التدقيق في جدول AuditLogs.
/// </summary>
public class AuditLogger : IAuditLogger
{
    private readonly DocGeneratorDbContext _db;

    public AuditLogger(DocGeneratorDbContext db) => _db = db;

    public async Task LogAsync(string? userName, string actionType, int? documentId = null,
        string? documentType = null, string? details = null, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            UserName = userName,
            ActionType = actionType,
            DocumentId = documentId,
            DocumentType = documentType,
            Details = details,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task LogManyAsync(IReadOnlyList<AuditLogEntry> entries, CancellationToken ct = default)
    {
        if (entries is null || entries.Count == 0)
            return;

        foreach (var entry in entries)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                UserName = entry.UserName,
                ActionType = entry.ActionType,
                DocumentId = entry.DocumentId,
                DocumentType = entry.DocumentType,
                Details = entry.Details,
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task LogDocumentChangeAsync(string? userName, string actionType, int documentId,
        string? documentType, string details, IReadOnlyList<DocumentFieldChange> changes,
        CancellationToken ct = default)
    {
        if (changes is null || changes.Count == 0)
        {
            await LogAsync(userName, actionType, documentId, documentType, details, ct);
            return;
        }

        var log = new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            UserName = userName,
            ActionType = actionType,
            DocumentId = documentId,
            DocumentType = documentType,
            Details = details,
        };
        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);

        foreach (var change in changes)
        {
            change.AuditLogId = log.Id;
            change.DocumentId = documentId;
            _db.DocumentFieldChanges.Add(change);
        }
        await _db.SaveChangesAsync(ct);
    }
}
