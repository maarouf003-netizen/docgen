using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Forum;

/// <summary>مرشحو الذِكر بالاسم — نشطون داخليون فقط بلا الذات ولا المندوب.</summary>
public sealed record SearchForumUsersQuery(
    string? Q,
    int ActorUserId) : IQuery<List<ForumUserLookupDto>>;

internal sealed class SearchForumUsersQueryHandler
    : QueryHandlerBase<SearchForumUsersQuery, List<ForumUserLookupDto>>
{
    private readonly IUserRepository _users;

    public SearchForumUsersQueryHandler(
        IUserRepository users,
        IEnumerable<IPipelineBehavior<SearchForumUsersQuery, List<ForumUserLookupDto>>> behaviors)
        : base(behaviors)
        => _users = users;

    protected override async Task<List<ForumUserLookupDto>> HandleCoreAsync(
        SearchForumUsersQuery query, CancellationToken ct)
    {
        await ForumMembership.RequireAsync(_users, query.ActorUserId, ct);
        var users = await _users.SearchForumMentionTargetsAsync(
            query.ActorUserId, query.Q, ForumService.MentionLookupLimit, ct);
        return users.Select(u => new ForumUserLookupDto(
            u.Id,
            ForumIdentityParts.DisplayName(u),
            u.Role.ToString().ToLowerInvariant(),
            ForumIdentityParts.ResolveLocation(u),
            ForumIdentityParts.ResolveSection(u))).ToList();
    }
}
