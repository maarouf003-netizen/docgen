using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;

namespace DocGenerator.Application.UseCases.Delegations;

/// <summary>
/// عدد طلبات الإنابة المعلّقة لنطاق الرئيس (§7.1 + §7.4) — نفس نطاق
/// <see cref="GetPendingDelegationsQuery"/> دون تحميل القوائم المرتبطة.
/// </summary>
public sealed record CountPendingDelegationsQuery(
    int BranchId,
    int? OwnerSectionId = null,
    bool RejectedOnly = false) : IQuery<int>;

internal sealed class CountPendingDelegationsQueryHandler
    : QueryHandlerBase<CountPendingDelegationsQuery, int>
{
    private readonly IDelegationRepository _delegations;

    public CountPendingDelegationsQueryHandler(
        IDelegationRepository delegations,
        IEnumerable<IPipelineBehavior<CountPendingDelegationsQuery, int>> behaviors)
        : base(behaviors)
        => _delegations = delegations;

    protected override Task<int> HandleCoreAsync(CountPendingDelegationsQuery query, CancellationToken ct)
        => _delegations.CountPendingByBranchAsync(query.BranchId, query.OwnerSectionId, query.RejectedOnly, ct);
}
