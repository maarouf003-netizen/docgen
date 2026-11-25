using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.Users;

/// <summary>
/// قائمتا المحامين (§5.4 — قرار §2.9): `branch` (الافتراضي للنوافذ: كل محامي
/// الفرع — تسريب الأسماء موثق ومقبول) و`mine` (الافتراضي: له ملف في دوائري
/// أو أنا أنشأته أو قديم بلا منشئ — القدامى يُرون دائمًا). المدير/المشرف
/// خارج القاعدتين (بلا دوائر): قائمة الفرع الكاملة. `includeInactive`
/// افتراضيًا صحيح حفظًا للسلوك القائم (إعادة التفعيل تحتاجه) — يُعلن هنا.
/// </summary>
public sealed record ListLawyersQuery(
    int? BranchId,
    string? Mode,
    int CallerUserId,
    int? CallerSectionId,
    bool CallerIsHeadOrSubHead,
    bool IncludeInactive) : IQuery<List<LawyerListItemDto>>;

internal sealed class ListLawyersQueryHandler
    : QueryHandlerBase<ListLawyersQuery, List<LawyerListItemDto>>
{
    private readonly IUserRepository _users;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IDocumentRepository _documents;

    public ListLawyersQueryHandler(
        IUserRepository users,
        IRepository<ExecutionCircuit> circuits,
        IDocumentRepository documents,
        IEnumerable<IPipelineBehavior<ListLawyersQuery, List<LawyerListItemDto>>> behaviors)
        : base(behaviors)
    {
        _users = users;
        _circuits = circuits;
        _documents = documents;
    }

    protected override async Task<List<LawyerListItemDto>> HandleCoreAsync(
        ListLawyersQuery query, CancellationToken ct)
    {
        var normalizedMode = (query.Mode ?? "mine").Trim().ToLowerInvariant();
        if (normalizedMode is not ("mine" or "branch"))
            throw new ArgumentException("وضع غير صالح — mine أو branch");

        if (!query.CallerIsHeadOrSubHead || normalizedMode == "branch")
            return await BranchLawyerListAsync(query.BranchId, query.IncludeInactive, ct);

        var circuits = await _circuits.ListAsync(ct);
        var myCircuitIds = circuits
            .Where(c => query.BranchId == null || c.BranchId == query.BranchId)
            .Where(c => c.SectionId == query.CallerSectionId)
            .Select(c => c.Id)
            .ToList();
        var ownerIds = await _documents.ListOwnerIdsByCircuitIdsAsync(myCircuitIds, ct);
        var inBranch = await BranchLawyerListAsync(query.BranchId, true, ct);
        // القدامى بلا منشئ ضمن نطاق الفرع (قرار §2.10 — والنطاق الفرعي للendpoint
        // ثابت: `Head_ListsLawyers_OnlyOwnBranch`).
        var veterans = inBranch.Where(l => l.CreatedById == null);
        return inBranch
            .Concat(veterans)
            .GroupBy(l => l.Id)
            .Select(g => g.First())
            .Where(l => query.IncludeInactive || l.IsActive)
            .Where(l => ownerIds.Contains(l.Id) || l.CreatedById == query.CallerUserId || l.CreatedById == null)
            .OrderBy(l => l.FullName)
            .ToList();
    }

    private async Task<List<LawyerListItemDto>> BranchLawyerListAsync(
        int? branchId, bool includeInactive, CancellationToken ct)
    {
        var lawyers = await _users.ListLawyersAsync(branchId, ct);
        return lawyers
            .Where(l => includeInactive || l.IsActive)
            .OrderBy(l => l.FullName)
            .Select(UserProjection.ToLawyerDto)
            .ToList();
    }
}
