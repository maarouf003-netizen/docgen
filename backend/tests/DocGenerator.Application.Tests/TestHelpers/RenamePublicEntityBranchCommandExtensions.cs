using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.PublicEntities;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>جسر اختباري ضيق لتوقيع <c>RenameBranchAsync</c> القديم (`P4-01bq`).</summary>
public static class RenamePublicEntityBranchCommandExtensions
{
    public static Task<RenameBranchResponse> RenameBranchAsync(
        this ICommandHandler<RenamePublicEntityBranchCommand, RenameBranchResponse> handler,
        int groupId,
        int entryId,
        RenameBranchRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new RenamePublicEntityBranchCommand(groupId, entryId, request, actor), ct);
}
