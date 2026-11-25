using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;

namespace DocGenerator.Application.UseCases.ReviewLetters;

/// <summary>عدد كتب النطاق بانتظار الرد (جرس المالك الأحمر §10).</summary>
public sealed record CountPendingReviewLettersQuery(
    int BranchId,
    int? OwnerSectionId = null) : IQuery<int>;

internal sealed class CountPendingReviewLettersQueryHandler
    : QueryHandlerBase<CountPendingReviewLettersQuery, int>
{
    private readonly IReviewLetterRepository _letters;

    public CountPendingReviewLettersQueryHandler(
        IReviewLetterRepository letters,
        IEnumerable<IPipelineBehavior<CountPendingReviewLettersQuery, int>> behaviors)
        : base(behaviors)
        => _letters = letters;

    protected override Task<int> HandleCoreAsync(
        CountPendingReviewLettersQuery query, CancellationToken ct)
        => _letters.CountPendingForScopeAsync(query.BranchId, query.OwnerSectionId, ct);
}
