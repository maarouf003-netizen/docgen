using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// استعلامات الاستئنافات على مستوى قاعدة البيانات: الجلب بروابطه، قوائم الملف،
/// بحث نطاق الرؤية مع مطابقة لقطات الأطراف، وفحص متابعة المحامي المسند إليه.
/// </summary>
public class AppealRepository : Repository<DocumentAppeal>, IAppealRepository
{
    public AppealRepository(DocGeneratorDbContext db) : base(db) { }

    public Task<DocumentAppeal?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default)
        => WithIncludes(Db.DocumentAppeals).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<List<DocumentAppeal>> ListByDocumentAsync(int documentId, CancellationToken ct = default)
    {
        var items = await WithIncludes(Db.DocumentAppeals.AsNoTracking())
            .Where(a => a.DocumentId == documentId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
        return items;
    }

    public async Task<(int Total, List<DocumentAppeal> Items)> SearchAsync(
        string? query,
        string? status,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var q = WithIncludes(Db.DocumentAppeals.AsNoTracking());

        if (visibleUserId is not null)
        {
            // محامٍ: الاستئنافات التي أنشأها أو أُسندت إليه للمتابعة.
            q = q.Where(a => a.CreatedById == visibleUserId.Value || a.AssignedLawyerId == visibleUserId.Value);
        }
        else if (visibleBranchId is not null)
        {
            // رئيس قسم: استئنافات ملفات فرعه.
            q = q.Where(a => a.Document.BranchId == visibleBranchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var term = status.Trim();
            q = q.Where(a => a.Status == term);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(a =>
                (a.AppellantsJson != null && a.AppellantsJson.Contains(term)) ||
                (a.AppelleesJson != null && a.AppelleesJson.Contains(term)) ||
                (a.AppealBaseNumber != null && a.AppealBaseNumber.Contains(term)) ||
                (a.AppellateCourt != null && a.AppellateCourt.Contains(term)));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToListAsync(ct);

        return (total, items);
    }

    public Task<bool> IsAssignedFollowerAsync(int documentId, int userId, CancellationToken ct = default)
        => Db.DocumentAppeals.AnyAsync(a => a.DocumentId == documentId && a.AssignedLawyerId == userId, ct);

    public Task<int> CountByAssigneeAsync(int assigneeId, int? branchId = null, CancellationToken ct = default)
    {
        var q = Db.DocumentAppeals.AsNoTracking()
            .Where(a => a.AssignedLawyerId == assigneeId);
        if (branchId is not null)
            q = q.Where(a => a.Document.BranchId == branchId.Value);
        return q.CountAsync(ct);
    }

    public async Task<List<DocumentAppeal>> ListByAssigneeAsync(
        int assigneeId, int? branchId = null, bool asNoTracking = true, CancellationToken ct = default)
    {
        var source = asNoTracking ? Db.DocumentAppeals.AsNoTracking() : Db.DocumentAppeals;
        var q = WithIncludes(source)
            .Where(a => a.AssignedLawyerId == assigneeId);
        if (branchId is not null)
            q = q.Where(a => a.Document.BranchId == branchId.Value);
        return await q
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<DocumentAppeal>> ListByDocumentIdsAsync(
        IReadOnlyCollection<int> documentIds, CancellationToken ct = default)
    {
        if (documentIds.Count == 0)
            return new List<DocumentAppeal>();

        return await Db.DocumentAppeals
            .Where(a => documentIds.Contains(a.DocumentId))
            .ToListAsync(ct);
    }

    public async Task<Dictionary<int, int>> MapFirstAppealIdByDocumentIdsAsync(
        IReadOnlyCollection<int> documentIds, CancellationToken ct = default)
    {
        if (documentIds.Count == 0)
            return new Dictionary<int, int>();

        var rows = await Db.DocumentAppeals.AsNoTracking()
            .Where(a => documentIds.Contains(a.DocumentId))
            .OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.DocumentId })
            .ToListAsync(ct);

        var map = new Dictionary<int, int>();
        foreach (var row in rows)
        {
            if (!map.ContainsKey(row.DocumentId))
                map[row.DocumentId] = row.Id;
        }
        return map;
    }

    private static IQueryable<DocumentAppeal> WithIncludes(IQueryable<DocumentAppeal> q) =>
        q.Include(a => a.Document)
            .Include(a => a.AssignedLawyer)
            .Include(a => a.CreatedBy)
            .Include(a => a.BaseNumbers)
            .Include(a => a.Actions)
                .ThenInclude(ac => ac.CreatedBy);
}
