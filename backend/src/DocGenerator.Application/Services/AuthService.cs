using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Services;

public interface IAuthService
{
    Task<UserDto?> GetUserAsync(int userId, CancellationToken ct = default);
}

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;

    public AuthService(IUserRepository users)
    {
        _users = users;
    }

    public async Task<UserDto?> GetUserAsync(int userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        return user is null ? null : UserDtoMapper.ToDto(user);
    }
}
