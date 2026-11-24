using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Delegations;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>جسر اختباري ضيق لتوقيعَي `RegisterAsync`/`CompleteAsync` القديمين (`P4-01bz`/`P4-01ca`).</summary>
public static class DelegationLifecycleCommandExtensions
{
    public static Task<DelegationDto?> RegisterAsync(
        this ICommandHandler<RegisterDocumentDelegationCommand, DelegationDto?> handler,
        int delegationId,
        RegisterDelegationRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new RegisterDocumentDelegationCommand(delegationId, request, userId, actorName), ct);

    public static Task<DelegationDto?> CompleteAsync(
        this ICommandHandler<CompleteDocumentDelegationCommand, DelegationDto?> handler,
        int delegationId,
        CompleteDelegationRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new CompleteDocumentDelegationCommand(delegationId, request, userId, actorName), ct);
}
