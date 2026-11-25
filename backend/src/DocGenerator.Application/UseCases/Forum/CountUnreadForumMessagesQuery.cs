using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Forum;

/// <summary>العدّاد — رسائل بلا إيصال لي (شارة الأيقونة).</summary>
public sealed record CountUnreadForumMessagesQuery(
    int ActorUserId) : IQuery<int>;

internal sealed class CountUnreadForumMessagesQueryHandler
    : QueryHandlerBase<CountUnreadForumMessagesQuery, int>
{
    private readonly IForumRepository _forum;
    private readonly IUserRepository _users;

    public CountUnreadForumMessagesQueryHandler(
        IForumRepository forum,
        IUserRepository users,
        IEnumerable<IPipelineBehavior<CountUnreadForumMessagesQuery, int>> behaviors)
        : base(behaviors)
    {
        _forum = forum;
        _users = users;
    }

    protected override async Task<int> HandleCoreAsync(
        CountUnreadForumMessagesQuery query, CancellationToken ct)
    {
        await ForumMembership.RequireAsync(_users, query.ActorUserId, ct);
        var max = await _forum.MaxReadMessageIdAsync(query.ActorUserId, ct);
        return await _forum.CountUnreadAsync(query.ActorUserId, max, ct);
    }
}
