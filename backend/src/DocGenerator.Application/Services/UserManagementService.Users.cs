using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class UserManagementService : IUserManagementService
{
    public async Task<List<UserListItemDto>> ListUsersAsync(CancellationToken ct = default)
    {
        var users = await _users.ListAllUsersAsync(ct);
        return users.Select(ToUserDto).ToList();
    }
}
