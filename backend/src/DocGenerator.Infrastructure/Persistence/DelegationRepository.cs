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
            // أصول الملف المنيب: يبني منها تعديل الإنابة لقطته ويتحقق من تبعيتها (ApplyDelegationAssets)
            // — إسقاط هذا السطر يُفشل أي تعديل في الإنتاج برسالة «لا يتبع الملف المنيب».
            .Include(d => d.SourceDocument)
                .ThenInclude(s => s!.Assets)
            .Include(d => d.TargetDocument)
                .ThenInclude(t => t!.RegistrationDate)
            .Include(d => d.TargetDocument)
                .ThenInclude(t => t!.BaseNumbers)
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
                .ThenInclude(t => t!.BaseNumbers)
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
            // المناب نفسه: رقمه الفعّال يُعرض في بطاقته عبر المحلل المركزي —
            // بدون هذا الجلب يسقط العرض بصمت إلى رقم الملف الأصلي.
            .Include(d => d.SourceDelegation)
                .ThenInclude(dl => dl!.TargetDocument)
                .ThenInclude(t => t!.BaseNumbers)
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

    public async Task<List<DocumentDelegation>> ListPendingBySourceWithTargetsAsync(int sourceDocumentId, CancellationToken ct = default)
    {
        // المرآة تلامس مجموعات الملف المناب المحلية (الكفلاء/الورثة/الجهات) فتُحمَّل
        // مسبقًا — دونها تتفكك المجموعات بلا قيد (N+1) ويُفشل الدمج بالكيان المتتبع.
        return await Db.DocumentDelegations
            .Where(d => d.SourceDocumentId == sourceDocumentId
                && d.Status != DelegationStatusCatalog.Executed
                && d.TargetDocument != null)
            .Include(d => d.TargetDocument)
                .ThenInclude(t => t!.RegistrationDate)
            .Include(d => d.TargetDocument)
                .ThenInclude(t => t!.BaseNumbers)
            .Include(d => d.TargetDocument)
                .ThenInclude(t => t!.Guarantors)
            .Include(d => d.TargetDocument)
                .ThenInclude(t => t!.Heirs)
            .Include(d => d.TargetDocument)
                .ThenInclude(t => t!.ApplicantPublicEntities)
            .ToListAsync(ct);
    }
}
