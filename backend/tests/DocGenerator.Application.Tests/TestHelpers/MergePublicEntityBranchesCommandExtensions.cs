using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.PublicEntities;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>جسر اختباري ضيق لتوقيع <c>MergeBranchesAsync</c> القديم (`P4-01br`).</summary>
public static class MergePublicEntityBranchesCommandExtensions
{
    public static Task<MergeBranchesResponse> MergeBranchesAsync(
        this ICommandHandler<MergePublicEntityBranchesCommand, MergeBranchesResponse> handler,
        int groupId,
        MergeBranchesRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new MergePublicEntityBranchesCommand(groupId, request, actor), ct);
}
