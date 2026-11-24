using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Auth;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>جسر اختباري ضيق لتوقيع <c>LoginAsync</c> القديم (`P4-01bu`).</summary>
public static class LoginCommandExtensions
{
    public static Task<LoginResult> LoginAsync(
        this ICommandHandler<LoginCommand, LoginResult> handler,
        LoginRequest request,
        CancellationToken ct = default)
        => handler.HandleAsync(new LoginCommand(request), ct);
}
