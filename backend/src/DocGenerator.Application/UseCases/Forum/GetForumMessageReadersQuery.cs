using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Forum;

/// <summary>نافذة «شوهدت بواسطة» — الكاتب فقط.</summary>
public sealed record GetForumMessageReadersQuery(
    int Id,
    int ActorUserId) : IQuery<List<ForumReaderDto>>;

internal sealed class GetForumMessageReadersQueryHandler
    : QueryHandlerBase<GetForumMessageReadersQuery, List<ForumReaderDto>>
{
    private readonly IForumRepository _forum;

    public GetForumMessageReadersQueryHandler(
        IForumRepository forum,
        IEnumerable<IPipelineBehavior<GetForumMessageReadersQuery, List<ForumReaderDto>>> behaviors)
        : base(behaviors)
        => _forum = forum;

    protected override async Task<List<ForumReaderDto>> HandleCoreAsync(
        GetForumMessageReadersQuery query, CancellationToken ct)
    {
        var message = await _forum.GetByIdAsync(query.Id, ct)
            ?? throw new KeyNotFoundException("الرسالة غير موجودة");
        if (message.AuthorId != query.ActorUserId)
            throw new UnauthorizedAccessException("نافذة «شوهدت بواسطة» متاحة لكاتب الرسالة فقط");
        var reads = await _forum.ListReadersAsync(query.Id, ct);
        return reads.Select(r => new ForumReaderDto(r.UserId, r.UserName, r.ReadAtUtc)).ToList();
    }
}
