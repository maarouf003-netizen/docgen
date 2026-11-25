using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;

namespace DocGenerator.Application.UseCases.ReviewLetters;

/// <summary>عدد كتب المحامي التي فيها ردّ لم يطّلع عليه بعد — شارة بند المطالعات.</summary>
public sealed record CountUnseenReviewRepliesQuery(int ActorUserId) : IQuery<int>;

internal sealed class CountUnseenReviewRepliesQueryHandler
    : QueryHandlerBase<CountUnseenReviewRepliesQuery, int>
{
    private readonly IReviewLetterRepository _letters;

    public CountUnseenReviewRepliesQueryHandler(
        IReviewLetterRepository letters,
        IEnumerable<IPipelineBehavior<CountUnseenReviewRepliesQuery, int>> behaviors)
        : base(behaviors)
        => _letters = letters;

    protected override Task<int> HandleCoreAsync(
        CountUnseenReviewRepliesQuery query, CancellationToken ct)
        => _letters.CountUnseenReplyLettersForLawyerAsync(query.ActorUserId, ct);
}
