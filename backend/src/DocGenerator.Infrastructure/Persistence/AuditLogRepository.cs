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
        CancellationToken ct = default,
        int? scopeBranchId = null,
        int? scopeSectionId = null)
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
        if (scopeBranchId.HasValue)
        {
            // RF-007: نطاق الفرع — الصف مرئي إن نُسِب لمستند الفرع (ولو حُذف منطقيًا، فتاريخ
            // الفرع يبقى لفرعه) أو لفاعل من الفرع. غير المنسوب مخفي (افتراض آمن).
            var branch = scopeBranchId.Value;
            q = q.Where(a =>
                (a.DocumentId != null && Db.Documents.IgnoreQueryFilters()
                    .Any(d => d.Id == a.DocumentId && d.BranchId == branch))
                || (a.UserName != null && Db.Users
                    .Any(u => u.Username == a.UserName && u.BranchId == branch)));
        }
        else if (scopeSectionId.HasValue)
        {
            // F6: نطاق الشعبة الدائري (قرار 23) — مرآة نطاق الفرع: الصف مرئي إن نُسِب
            // لمستند دائرته في الشعبة (ولو حُذف منطقيًا، فتاريخ الشعبة يبقى لشعبته)
            // أو لفاعل من الشعبة. غير المنسوب مخفي (افتراض آمن).
            var section = scopeSectionId.Value;
            q = q.Where(a =>
                (a.DocumentId != null && Db.Documents.IgnoreQueryFilters()
                    .Any(d => d.Id == a.DocumentId && d.ExecutionCircuitId != null && d.ExecutionCircuit!.SectionId == section))
                || (a.UserName != null && Db.Users
                    .Any(u => u.Username == a.UserName && u.SectionId == section)));
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToListAsync(ct);

        return (total, items);
    }

    public async Task<(int TotalCount, List<AuditLog> Items)> PageDocumentChangeGroupsAsync(
        int documentId,
        int page,
        int perPage,
        CancellationToken ct = default)
    {
        var q = Db.AuditLogs
            .AsNoTracking()
            .Where(a => a.DocumentId == documentId && a.FieldChanges.Any());

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(a => a.Id)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Include(a => a.FieldChanges.OrderBy(c => c.Id))
            .ToListAsync(ct);

        return (total, items);
    }
}
