using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Users;

/// <summary>قائمة المستخدمين الكاملة — إدارة المستخدمين (مشرف/مدير).</summary>
public sealed record ListUsersQuery : IQuery<List<UserListItemDto>>;

internal sealed class ListUsersQueryHandler : QueryHandlerBase<ListUsersQuery, List<UserListItemDto>>
{
    private readonly IUserRepository _users;

    public ListUsersQueryHandler(
        IUserRepository users,
        IEnumerable<IPipelineBehavior<ListUsersQuery, List<UserListItemDto>>> behaviors)
        : base(behaviors)
        => _users = users;

    protected override async Task<List<UserListItemDto>> HandleCoreAsync(
        ListUsersQuery query, CancellationToken ct)
    {
        var users = await _users.ListAllUsersAsync(ct);
        return users.Select(UserProjection.ToUserDto).ToList();
    }
}
