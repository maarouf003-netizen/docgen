using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.UseCases.Auth;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>جسر اختباري ضيق لتوقيع <c>ChangePasswordAsync</c> القديم (`P4-01bj`).</summary>
public static class ChangePasswordCommandExtensions
{
    public static Task<bool> ChangePasswordAsync(
        this ICommandHandler<ChangePasswordCommand, bool> handler,
        int userId,
        string oldPassword,
        string newPassword,
        CancellationToken ct = default)
        => handler.HandleAsync(new ChangePasswordCommand(userId, oldPassword, newPassword), ct);
}
