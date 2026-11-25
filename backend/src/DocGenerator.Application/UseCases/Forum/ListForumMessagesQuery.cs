using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Forum;

/// <summary>شريحة التيار بمفتاح `Id` (الأحدث أولًا بلا مؤشر) + `readCount` لرسائلي.</summary>
public sealed record ListForumMessagesQuery(
    int ActorUserId,
    int Limit,
    int? Before = null,
    int? After = null) : IQuery<ForumMessagesPageDto>;

internal sealed class ListForumMessagesQueryHandler
    : QueryHandlerBase<ListForumMessagesQuery, ForumMessagesPageDto>
{
    private readonly IForumRepository _forum;

    public ListForumMessagesQueryHandler(
        IForumRepository forum,
        IEnumerable<IPipelineBehavior<ListForumMessagesQuery, ForumMessagesPageDto>> behaviors)
        : base(behaviors)
        => _forum = forum;

    protected override async Task<ForumMessagesPageDto> HandleCoreAsync(
        ListForumMessagesQuery query, CancellationToken ct)
    {
        var limit = query.Limit;
        if (limit < 1 || limit > ForumService.MaxLimit)
            limit = ForumService.DefaultLimit;
        var items = await _forum.ListSliceAsync(limit, query.Before, query.After, ct);
        var counts = await _forum.CountReadsAsync(
            items.Where(m => m.AuthorId == query.ActorUserId).Select(m => m.Id).ToList(), ct);
        var dtos = items.Select(m => ForumService.ToDto(m, counts.GetValueOrDefault(m.Id))).ToList();
        var hasOlder = dtos.Count > 0 && await _forum.HasOlderAsync(dtos[0].Id, ct);
        return new ForumMessagesPageDto(dtos, hasOlder);
    }
}
