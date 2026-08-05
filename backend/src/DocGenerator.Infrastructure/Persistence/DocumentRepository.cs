using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// بحث ترحّلي يُنفَّذ في قاعدة البيانات (Count + Skip/Take) بدل تحميل كل السجلات.
/// </summary>
public class DocumentRepository : Repository<Document>, IDocumentRepository
{
    public DocumentRepository(DocGeneratorDbContext db) : base(db) { }

    public async Task<(int TotalCount, List<Document> Items)> SearchAsync(
        string? query,
        string? status,
        string? applicant,
        string? court,
        string? lawyer,
        int? branchId,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
        CancellationToken ct = default)
    {
        IQueryable<Document> q = Db.Documents.AsNoTracking();

        if (visibleBranchId.HasValue)
            q = q.Where(d => d.BranchId == visibleBranchId);
        if (visibleUserId.HasValue)
            q = q.Where(d => d.CreatedById == visibleUserId);
        if (branchId.HasValue)
            q = q.Where(d => d.BranchId == branchId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == ExecutionStatusCatalog.ExecutedFilter)
                q = q.Where(d => d.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                    || d.ExecStatus == ExecutionStatusCatalog.ExecutedBySettlement);
            else if (status == ExecutionStatusCatalog.Deferred)
                q = q.Where(d => d.ExecStatus == ExecutionStatusCatalog.Deferred);
            else
                q = q.Where(d =>
                    string.IsNullOrEmpty(d.ExecStatus) &&
                    d.IsDraft == (status == ExecutionStatusCatalog.DraftFilter));
        }

        if (!string.IsNullOrWhiteSpace(applicant))
        {
            var term = applicant.Trim();
            q = q.Where(d => d.Applicant != null && d.Applicant == term);
        }

        if (!string.IsNullOrWhiteSpace(court))
        {
            var term = court.Trim();
            q = q.Where(d => d.Court != null && d.Court == term);
        }

        if (!string.IsNullOrWhiteSpace(lawyer))
        {
            var term = lawyer.Trim();
            q = q.Where(d => d.Lawyer != null && d.Lawyer == term);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(d =>
                (d.SearchText != null && d.SearchText.Contains(term)) ||
                (d.BorrowerName != null &&
                    ((d.BorrowerName + " " + (d.BorrowerFamily ?? string.Empty)).Contains(term) ||
                     (d.BorrowerName + " " + (d.BorrowerFather ?? string.Empty) + " " + (d.BorrowerFamily ?? string.Empty)).Contains(term))) ||
                d.Guarantors.Any(g =>
                    g.GuarantorName != null &&
                    ((g.GuarantorName + " " + (g.GuarantorFamily ?? string.Empty)).Contains(term) ||
                     (g.GuarantorName + " " + (g.GuarantorFather ?? string.Empty) + " " + (g.GuarantorFamily ?? string.Empty)).Contains(term))));
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Include(d => d.Guarantors)
            .Include(d => d.RealEstates)
            .Include(d => d.ExecutionActions)
            .Include(d => d.RegistrationDate)
            .Include(d => d.CreatedBy)
            .Include(d => d.Branch)
            .ToListAsync(ct);

        return (total, items);
    }

    public async Task<(List<string> Applicants, List<string> Courts, List<string> Lawyers)> GetFilterOptionsAsync(
        int? visibleBranchId,
        int? visibleUserId,
        CancellationToken ct = default)
    {
        IQueryable<Document> q = Db.Documents.AsNoTracking();

        if (visibleBranchId.HasValue)
            q = q.Where(d => d.BranchId == visibleBranchId);
        if (visibleUserId.HasValue)
            q = q.Where(d => d.CreatedById == visibleUserId);

        var applicants = await q
            .Where(d => d.Applicant != null && d.Applicant != string.Empty)
            .Select(d => d.Applicant!)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(ct);

        var courts = await q
            .Where(d => d.Court != null && d.Court != string.Empty)
            .Select(d => d.Court!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

        var lawyers = await q
            .Where(d => d.Lawyer != null && d.Lawyer != string.Empty)
            .Select(d => d.Lawyer!)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync(ct);

        return (applicants, courts, lawyers);
    }

    public async Task<Document?> GetDeletedByIdAsync(int id, CancellationToken ct = default)
    {
        return await Db.Documents
            .IgnoreQueryFilters()
            .Include(d => d.Guarantors)
            .Include(d => d.RealEstates)
            .Include(d => d.ExecutionActions)
            .Include(d => d.RegistrationDate)
            .Include(d => d.CreatedBy)
            .Include(d => d.Branch)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<(int TotalCount, List<Document> Items)> SearchDeletedAsync(
        string? query,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
        CancellationToken ct = default)
    {
        IQueryable<Document> q = Db.Documents.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.IsDeleted);

        if (visibleBranchId.HasValue)
            q = q.Where(d => d.BranchId == visibleBranchId);

        if (visibleUserId.HasValue)
            q = q.Where(d => d.CreatedById == visibleUserId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(d =>
                (d.SearchText != null && d.SearchText.Contains(term)) ||
                (d.BorrowerName != null &&
                    ((d.BorrowerName + " " + (d.BorrowerFamily ?? string.Empty)).Contains(term) ||
                     (d.BorrowerName + " " + (d.BorrowerFather ?? string.Empty) + " " + (d.BorrowerFamily ?? string.Empty)).Contains(term))) ||
                d.Guarantors.Any(g =>
                    g.GuarantorName != null &&
                    ((g.GuarantorName + " " + (g.GuarantorFamily ?? string.Empty)).Contains(term) ||
                     (g.GuarantorName + " " + (g.GuarantorFather ?? string.Empty) + " " + (g.GuarantorFamily ?? string.Empty)).Contains(term))));
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(d => d.DeletedAt)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Include(d => d.Guarantors)
            .Include(d => d.RealEstates)
            .Include(d => d.ExecutionActions)
            .Include(d => d.RegistrationDate)
            .Include(d => d.CreatedBy)
            .Include(d => d.Branch)
            .ToListAsync(ct);

        return (total, items);
    }

    public async Task<Document?> TransferOwnerAsync(
        int id,
        int expectedCreatedById,
        int targetId,
        string targetFullName,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var rows = await Db.Documents
            .Where(d => d.Id == id && d.CreatedById == expectedCreatedById)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.CreatedById, targetId)
                .SetProperty(d => d.Lawyer, targetFullName)
                .SetProperty(d => d.UpdatedAt, now), ct);

        if (rows == 0)
            return null;

        return await Db.Documents
            .AsNoTracking()
            .Include(d => d.Guarantors)
            .Include(d => d.RealEstates)
            .Include(d => d.ExecutionActions)
            .Include(d => d.RegistrationDate)
            .Include(d => d.CreatedBy)
            .Include(d => d.Branch)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }
}
