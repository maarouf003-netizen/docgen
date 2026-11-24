using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.PublicEntities;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>جسر اختباري ضيق لتوقيع <c>AbolishBranchAsync</c> القديم (`P4-01bs`).</summary>
public static class AbolishPublicEntityBranchCommandExtensions
{
    public static Task<AbolishBranchResponse> AbolishBranchAsync(
        this ICommandHandler<AbolishPublicEntityBranchCommand, AbolishBranchResponse> handler,
        int groupId,
        int entryId,
        AbolishBranchRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new AbolishPublicEntityBranchCommand(groupId, entryId, request, actor), ct);
}
