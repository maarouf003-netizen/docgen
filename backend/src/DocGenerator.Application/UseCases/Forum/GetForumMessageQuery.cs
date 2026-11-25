using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Forum;

/// <summary>رسالة واحدة بالمعرف (`null` للغائبة — دون كشف إضافي).</summary>
public sealed record GetForumMessageQuery(
    int Id,
    int ActorUserId) : IQuery<ForumMessageDto?>;

internal sealed class GetForumMessageQueryHandler
    : QueryHandlerBase<GetForumMessageQuery, ForumMessageDto?>
{
    private readonly IForumRepository _forum;

    public GetForumMessageQueryHandler(
        IForumRepository forum,
        IEnumerable<IPipelineBehavior<GetForumMessageQuery, ForumMessageDto?>> behaviors)
        : base(behaviors)
        => _forum = forum;

    protected override async Task<ForumMessageDto?> HandleCoreAsync(
        GetForumMessageQuery query, CancellationToken ct)
    {
        var message = await _forum.GetByIdAsync(query.Id, ct);
        if (message is null)
            return null;
        var counts = message.AuthorId == query.ActorUserId
            ? await _forum.CountReadsAsync(new[] { message.Id }, ct)
            : new Dictionary<int, int>();
        return ForumService.ToDto(message, counts.GetValueOrDefault(message.Id));
    }
}
