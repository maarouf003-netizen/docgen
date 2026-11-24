using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Users;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيعَي <c>CreateUserAsync</c>/<c>UpdateUserAsync</c>
/// القديمين فوق معالجي الأوامر — منطق الاختبارات كما هو (`P4-01bh`).
/// </summary>
public static class UserWriteCommandExtensions
{
    public static Task<UserListItemDto> CreateUserAsync(
        this ICommandHandler<CreateUserCommand, UserListItemDto> handler,
        CreateUserRequest request,
        string? actorName,
        CancellationToken ct = default,
        int? actorUserId = null)
        => handler.HandleAsync(new CreateUserCommand(request, actorName, actorUserId), ct);

    public static Task<UserListItemDto?> UpdateUserAsync(
        this ICommandHandler<UpdateUserCommand, UserListItemDto?> handler,
        int userId,
        UpdateUserRequest request,
        int actorUserId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new UpdateUserCommand(userId, request, actorUserId, actorName), ct);
}
