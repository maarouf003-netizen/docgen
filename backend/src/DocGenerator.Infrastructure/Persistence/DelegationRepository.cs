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
            // سلاسل النطاق (§7): دائرة المصدر وشعبتها + المنابة وشعبتها.
            .Include(d => d.SourceDocument)
                .ThenInclude(s => s!.ExecutionCircuit)
                .ThenInclude(c => c!.Section)
            .Include(d => d.DelegatedCircuit)
                .ThenInclude(c => c!.Section)
            .Include(d => d.RedirectedToSection)
            .Include(d => d.TargetDocument)
                .ThenInclude(t => t!.RegistrationDate)
            .Include(d => d.TargetDocument)
                .ThenInclude(t => t!.BaseNumbers)
            // فرع المناب الحي لبطاقة §5.7.
            .Include(d => d.TargetDocument)
                .ThenInclude(t => t!.Branch)
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
            // فرع المناب الحي لبطاقة §5.7.
            .Include(d => d.TargetDocument)
                .ThenInclude(t => t!.Branch)
            // الشعبة الموجَّه لها (§7.3) لاسمها في بطاقة الملف.
            .Include(d => d.RedirectedToSection)
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
            // فرع المناب الحي لبطاقة §5.7 (توحيد الدلالة عبر كل الاستعلامات).
            .Include(d => d.SourceDelegation)
                .ThenInclude(dl => dl!.TargetDocument)
                .ThenInclude(t => t!.Branch)
            .Where(d => d.Id == targetDocumentId && d.SourceDelegation != null)
            .Select(d => d.SourceDelegation!)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// نطاق «المعلّقة بانتظار الاعتماد» لنطاق رئيسٍ معيّن (§7.1 + §7.4) — مشترك
    /// بين القائمة والعدّاد: داخلية بدائرة لرئيس شعبته (أو قسمه إن بلا شعبة)،
    /// وداخلية بلا دائرة (انتقالية/قديمة/مفكوكة) لرئيس قسم فرع المنيب، وخارجية
    /// لرئيس قسم الفرع المناب ما لم تُوجَّه لشعبة. `rejectedOnly` لفلتر
    /// «مرفوض بانتظار التصحيح» (الافتراضي: القابلة للاعتماد فقط).
    /// </summary>
    private IQueryable<DocumentDelegation> PendingByScopeQuery(int branchId, int? ownerSectionId, bool rejectedOnly)
        => Db.DocumentDelegations
            .Where(d => d.Status == DelegationStatusCatalog.PendingHead
                && (rejectedOnly ? d.RejectReason != null : d.RejectReason == null)
                && ((!d.IsExternal
                        && ((d.DelegatedCircuitId == null
                                && !ownerSectionId.HasValue
                                && d.SourceDocument.BranchId == branchId)
                            || (d.DelegatedCircuitId != null
                                && d.DelegatedCircuit!.BranchId == branchId
                                && d.DelegatedCircuit!.SectionId == ownerSectionId)))
                    || (d.IsExternal
                        && d.ExternalBranchId == branchId
                        && d.RedirectedToSectionId == ownerSectionId)));

    public async Task<List<DocumentDelegation>> ListPendingByBranchAsync(
        int branchId, int? ownerSectionId = null, bool rejectedOnly = false, CancellationToken ct = default)
    {
        return await PendingByScopeQuery(branchId, ownerSectionId, rejectedOnly)
            .OrderByDescending(d => d.CreatedAt)
            .Include(d => d.SourceDocument)
                .ThenInclude(s => s!.BaseNumbers)
            // دائرة المصدر وشعبتها + الدائرة المنابة وشعبتها للتوجيه بالنطاق (§7).
            .Include(d => d.SourceDocument)
                .ThenInclude(s => s!.ExecutionCircuit)
                .ThenInclude(c => c!.Section)
            .Include(d => d.DelegatedCircuit)
                .ThenInclude(c => c!.Section)
            .Include(d => d.RedirectedToSection)
            .Include(d => d.ExternalBranch)
            .Include(d => d.AssignedLawyer)
            .Include(d => d.CreatedBy)
            .Include(d => d.Assets)
            .ToListAsync(ct);
    }

    public Task<int> CountPendingByBranchAsync(
        int branchId, int? ownerSectionId = null, bool rejectedOnly = false, CancellationToken ct = default)
        => PendingByScopeQuery(branchId, ownerSectionId, rejectedOnly).CountAsync(ct);

    public async Task<List<DocumentDelegation>> ListPendingBySourceWithTargetsAsync(int sourceDocumentId, CancellationToken ct = default)
    {
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
            // فرع المناب الحي لبطاقة §5.7 (توحيد الدلالة عبر كل الاستعلامات).
            .Include(d => d.TargetDocument)
                .ThenInclude(t => t!.Branch)
            .ToListAsync(ct);
    }

    public async Task<List<DocumentDelegation>> ListByDelegatedCircuitAsync(int circuitId, CancellationToken ct = default)
    {
        return await Db.DocumentDelegations
            .Where(d => d.DelegatedCircuitId == circuitId)
            .ToListAsync(ct);
    }

    public async Task<int> CountPendingIncomingByCircuitAsync(int circuitId, CancellationToken ct = default)
    {
        return await Db.DocumentDelegations
            .CountAsync(d => d.DelegatedCircuitId == circuitId && d.Status == DelegationStatusCatalog.PendingHead, ct);
    }

    public async Task<List<DocumentDelegation>> ListByDelegatedCircuitIncludingDeletedAsync(int circuitId, CancellationToken ct = default)
    {
        return await Db.DocumentDelegations
            .IgnoreQueryFilters()
            .Where(d => d.DelegatedCircuitId == circuitId)
            .ToListAsync(ct);
    }

    public async Task<int> CountPendingIncomingByCircuitIncludingDeletedAsync(int circuitId, CancellationToken ct = default)
    {
        return await Db.DocumentDelegations
            .IgnoreQueryFilters()
            .CountAsync(d => d.DelegatedCircuitId == circuitId && d.Status == DelegationStatusCatalog.PendingHead, ct);
    }
}
