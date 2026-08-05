using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// استعلام سجل التدقيق (قراءة) مع ترقيم صفحات وترشيح على مستوى قاعدة البيانات.
/// </summary>
public class AuditLogRepository : Repository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(DocGeneratorDbContext db) : base(db) { }

    public async Task<(int TotalCount, List<AuditLog> Items)> SearchAsync(
        string? userName,
        string? actionType,
        int page,
        int perPage,
        CancellationToken ct = default)
    {
        IQueryable<AuditLog> q = Db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(userName))
        {
            var term = userName.Trim();
            q = q.Where(a => a.UserName != null && a.UserName.ToLower().Contains(term.ToLower()));
        }
        if (!string.IsNullOrWhiteSpace(actionType))
        {
            var type = actionType.Trim();
            q = q.Where(a => a.ActionType == type);
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToListAsync(ct);

        return (total, items);
    }
}
