using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// استعلامات الإنابات على مستوى قاعدة البيانات: جلب السجل مع كامل روابطه وأصحاب الرؤية.
/// </summary>
public class DelegationRepository : Repository<DocumentDelegation>, IDelegationRepository
{
    public DelegationRepository(DocGeneratorDbContext db) : base(db) { }

    public async Task<DocumentDelegation?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default)
    {
        return await Db.DocumentDelegations
            .Include(d => d.SourceDocument)
                .ThenInclude(s => s!.BaseNumbers)
            .Include(d => d.SourceDocument)
                .ThenInclude(s => s!.ApplicantPublicEntities)
            .Include(d => d.SourceDocument)
                .ThenInclude(s => s!.Guarantors)
            .Include(d => d.SourceDocument)
                .ThenInclude(s => s!.Heirs)
            .Include(d => d.TargetDocument)
                .ThenInclude(t => t!.RegistrationDate)
            .Include(d => d.ExternalBranch)
            .Include(d => d.AssignedLawyer)
            .Include(d => d.CreatedBy)
            .Include(d => d.Assets)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<List<DocumentDelegation>> ListBySourceAsync(int sourceDocumentId, CancellationToken ct = default)
    {
        return await Db.DocumentDelegations
            .Where(d => d.SourceDocumentId == sourceDocumentId)
            .OrderByDescending(d => d.CreatedAt)
            .Include(d => d.SourceDocument)
                .ThenInclude(s => s!.BaseNumbers)
            .Include(d => d.ExternalBranch)
            .Include(d => d.AssignedLawyer)
            .Include(d => d.CreatedBy)
            .Include(d => d.TargetDocument)
            .Include(d => d.Assets)
            .ToListAsync(ct);
    }

    public async Task<DocumentDelegation?> FindByTargetAsync(int targetDocumentId, CancellationToken ct = default)
    {
        return await Db.Documents
            .Include(d => d.SourceDelegation)
                .ThenInclude(dl => dl!.SourceDocument)
                .ThenInclude(s => s!.BaseNumbers)
            .Include(d => d.SourceDelegation)
                .ThenInclude(dl => dl!.ExternalBranch)
            .Include(d => d.SourceDelegation)
                .ThenInclude(dl => dl!.AssignedLawyer)
            .Include(d => d.SourceDelegation)
                .ThenInclude(dl => dl!.CreatedBy)
            .Include(d => d.SourceDelegation)
                .ThenInclude(dl => dl!.Assets)
            .Where(d => d.Id == targetDocumentId && d.SourceDelegation != null)
            .Select(d => d.SourceDelegation!)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<DocumentDelegation>> ListPendingByBranchAsync(int branchId, CancellationToken ct = default)
    {
        return await Db.DocumentDelegations
            .Where(d => d.Status == DelegationStatusCatalog.PendingHead
                && ((!d.IsExternal && d.SourceDocument.BranchId == branchId)
                    || (d.IsExternal && d.ExternalBranchId == branchId)))
            .OrderByDescending(d => d.CreatedAt)
            .Include(d => d.SourceDocument)
                .ThenInclude(s => s!.BaseNumbers)
            .Include(d => d.ExternalBranch)
            .Include(d => d.AssignedLawyer)
            .Include(d => d.CreatedBy)
            .Include(d => d.Assets)
            .ToListAsync(ct);
    }
}
