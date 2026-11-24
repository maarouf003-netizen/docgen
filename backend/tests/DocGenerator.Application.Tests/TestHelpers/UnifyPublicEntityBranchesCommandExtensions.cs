using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.PublicEntities;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>جسر اختباري ضيق لتوقيع <c>UnifyBranchesAsync</c> القديم (`P4-01bv`).</summary>
public static class UnifyPublicEntityBranchesCommandExtensions
{
    public static Task<UnifyBranchesResponse> UnifyBranchesAsync(
        this ICommandHandler<UnifyPublicEntityBranchesCommand, UnifyBranchesResponse> handler,
        int groupId,
        UnifyBranchesRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new UnifyPublicEntityBranchesCommand(groupId, request, actor), ct);
}
