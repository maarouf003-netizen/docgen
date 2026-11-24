using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.PublicEntities;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>جسر اختباري ضيق لتوقيعَي `CommitMergeAsync`/`RenameGroupAsync` القديمين (`P4-01cb`/`P4-01cc`).</summary>
public static class PublicEntityGroupWriteCommandExtensions
{
    public static Task<MergeCommitResponse> CommitMergeAsync(
        this ICommandHandler<CommitPublicEntityMergeCommand, MergeCommitResponse> handler,
        MergeCommitRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default,
        string? idempotencyKey = null)
        => handler.HandleAsync(new CommitPublicEntityMergeCommand(request, actor, idempotencyKey), ct);

    public static Task<RenameGroupResponse> RenameGroupAsync(
        this ICommandHandler<RenamePublicEntityGroupCommand, RenameGroupResponse> handler,
        RenameGroupRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new RenamePublicEntityGroupCommand(request, actor), ct);
}
