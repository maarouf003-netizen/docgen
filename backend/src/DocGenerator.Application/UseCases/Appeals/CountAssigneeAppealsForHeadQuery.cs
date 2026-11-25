using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;

namespace DocGenerator.Application.UseCases.Appeals;

/// <summary>عدد استئنافات محامٍ ضمن نطاق الرئيس — لمعاينة النقل الجملة (تطابق المنقول).</summary>
public sealed record CountAssigneeAppealsForHeadQuery(
    int AssigneeId, int? HeadBranchId, int? OwnerSectionId) : IQuery<int>;

internal sealed class CountAssigneeAppealsForHeadQueryHandler
    : QueryHandlerBase<CountAssigneeAppealsForHeadQuery, int>
{
    private readonly IAppealRepository _appeals;

    public CountAssigneeAppealsForHeadQueryHandler(
        IAppealRepository appeals,
        IEnumerable<IPipelineBehavior<CountAssigneeAppealsForHeadQuery, int>> behaviors)
        : base(behaviors)
        => _appeals = appeals;

    protected override async Task<int> HandleCoreAsync(
        CountAssigneeAppealsForHeadQuery query, CancellationToken ct)
    {
        if (query.HeadBranchId is null)
            throw new ArgumentException("رئيس القسم دون فرع لا يمكنه الاطلاع على الاستئنافات");
        // المعاينة بالنطاق نفسه (§5.5): عدّ قاعدي يطابق المنقول فعلًا.
        return await _appeals.CountPendingByAssigneeInScopeAsync(
            query.AssigneeId, query.HeadBranchId.Value, query.OwnerSectionId, ct);
    }
}
