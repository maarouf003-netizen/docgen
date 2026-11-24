using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class UserManagementService : IUserManagementService
{
    public async Task<List<LawyerListItemDto>> ListLawyersAsync(int? branchId, CancellationToken ct = default)
        => await ListLawyersAsync(branchId, "branch", 0, null, false, true, ct);

    /// <summary>
    /// قائمتا المحامين (§5.4 — قرار §2.9): `branch` (الافتراضي للنوافذ: كل محامي
    /// الفرع — تسريب الأسماء موثق ومقبول) و`mine` (الافتراضي: له ملف في دوائري
    /// أو أنا أنشأته أو قديم بلا منشئ — القدامى يُرون دائمًا). المدير/المشرف
    /// خارج القاعدتين (بلا دوائر): قائمة الفرع الكاملة. `includeInactive`
    /// افتراضيًا صحيح حفظًا للسلوك القائم (إعادة التفعيل تحتاجه) — يُعلن هنا.
    /// </summary>
    public async Task<List<LawyerListItemDto>> ListLawyersAsync(
        int? branchId,
        string? mode,
        int callerUserId,
        int? callerSectionId,
        bool callerIsHeadOrSubHead,
        bool includeInactive,
        CancellationToken ct = default)
    {
        var normalizedMode = (mode ?? "mine").Trim().ToLowerInvariant();
        if (normalizedMode is not ("mine" or "branch"))
            throw new ArgumentException("وضع غير صالح — mine أو branch");

        if (!callerIsHeadOrSubHead || normalizedMode == "branch")
            return await BranchLawyerListAsync(branchId, includeInactive, ct);

        var circuits = await _circuits.ListAsync(ct);
        var myCircuitIds = circuits
            .Where(c => branchId == null || c.BranchId == branchId)
            .Where(c => c.SectionId == callerSectionId)
            .Select(c => c.Id)
            .ToList();
        var ownerIds = await _documents.ListOwnerIdsByCircuitIdsAsync(myCircuitIds, ct);
        var inBranch = await BranchLawyerListAsync(branchId, true, ct);
        // القدامى بلا منشئ ضمن نطاق الفرع (قرار §2.10 — والنطاق الفرعي للendpoint
        // ثابت: `Head_ListsLawyers_OnlyOwnBranch`).
        var veterans = inBranch.Where(l => l.CreatedById == null);
        return inBranch
            .Concat(veterans)
            .GroupBy(l => l.Id)
            .Select(g => g.First())
            .Where(l => includeInactive || l.IsActive)
            .Where(l => ownerIds.Contains(l.Id) || l.CreatedById == callerUserId || l.CreatedById == null)
            .OrderBy(l => l.FullName)
            .ToList();
    }

    private async Task<List<LawyerListItemDto>> BranchLawyerListAsync(int? branchId, bool includeInactive, CancellationToken ct)
    {
        var lawyers = await _users.ListLawyersAsync(branchId, ct);
        return lawyers
            .Where(l => includeInactive || l.IsActive)
            .OrderBy(l => l.FullName)
            .Select(ToLawyerDto)
            .ToList();
    }
}
