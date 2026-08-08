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
}
