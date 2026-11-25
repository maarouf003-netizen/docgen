using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;

namespace DocGenerator.Application.UseCases.Correspondences;

/// <summary>عدد المراسلات غير المؤكَّدة المشاهدة (كمستلم) — عدّاد الجرس والشارة.</summary>
public sealed record CountUnseenCorrespondencesQuery(int ActorUserId) : IQuery<int>;

internal sealed class CountUnseenCorrespondencesQueryHandler
    : QueryHandlerBase<CountUnseenCorrespondencesQuery, int>
{
    private readonly ICorrespondenceRepository _letters;

    public CountUnseenCorrespondencesQueryHandler(
        ICorrespondenceRepository letters,
        IEnumerable<IPipelineBehavior<CountUnseenCorrespondencesQuery, int>> behaviors)
        : base(behaviors)
        => _letters = letters;

    protected override Task<int> HandleCoreAsync(
        CountUnseenCorrespondencesQuery query, CancellationToken ct)
        => _letters.CountUnseenForTargetAsync(query.ActorUserId, ct);
}
