using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Forum;

/// <summary>الرسالة المثبّتة للشريط (`null` عند عدمها).</summary>
public sealed record GetPinnedForumMessageQuery(
    int ActorUserId) : IQuery<ForumMessageDto?>;

internal sealed class GetPinnedForumMessageQueryHandler
    : QueryHandlerBase<GetPinnedForumMessageQuery, ForumMessageDto?>
{
    private readonly IForumRepository _forum;

    public GetPinnedForumMessageQueryHandler(
        IForumRepository forum,
        IEnumerable<IPipelineBehavior<GetPinnedForumMessageQuery, ForumMessageDto?>> behaviors)
        : base(behaviors)
        => _forum = forum;

    protected override async Task<ForumMessageDto?> HandleCoreAsync(
        GetPinnedForumMessageQuery query, CancellationToken ct)
    {
        var pinned = await _forum.GetPinnedAsync(ct);
        if (pinned is null)
            return null;
        var counts = pinned.AuthorId == query.ActorUserId
            ? await _forum.CountReadsAsync(new[] { pinned.Id }, ct)
            : new Dictionary<int, int>();
        return ForumService.ToDto(pinned, counts.GetValueOrDefault(pinned.Id));
    }
}
