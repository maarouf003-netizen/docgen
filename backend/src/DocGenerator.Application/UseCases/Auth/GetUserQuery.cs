using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Auth;

/// <summary>ملف المستخدم الحالي (`/api/auth/me`) — `null` يعني غير موجود (يُترجم 404).</summary>
public sealed record GetUserQuery(int UserId) : IQuery<UserDto?>;

internal sealed class GetUserQueryHandler : QueryHandlerBase<GetUserQuery, UserDto?>
{
    private readonly IUserRepository _users;

    public GetUserQueryHandler(
        IUserRepository users,
        IEnumerable<IPipelineBehavior<GetUserQuery, UserDto?>> behaviors)
        : base(behaviors)
        => _users = users;

    protected override async Task<UserDto?> HandleCoreAsync(GetUserQuery query, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(query.UserId, ct);
        return user is null ? null : UserDtoMapper.ToDto(user);
    }
}
