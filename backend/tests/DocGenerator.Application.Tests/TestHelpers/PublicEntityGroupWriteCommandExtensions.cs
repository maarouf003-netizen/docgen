using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.PublicEntities;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>جسور نداء أوامر كتابات هوية الجهة العامة (`P4-01cb`/`P4-01cc`/`P4-01cd`/`P4-01ce`/`P4-01cf`/`P4-01ch`).</summary>
public static class PublicEntityGroupWriteCommandExtensions
{
    public static Task<UnifyNamesResponse> UnifyNamesAsync(
        this ICommandHandler<UnifyPublicEntityNamesCommand, UnifyNamesResponse> handler,
        UnifyNamesRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default,
        string? idempotencyKey = null)
        => handler.HandleAsync(new UnifyPublicEntityNamesCommand(request, actor, idempotencyKey), ct);
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

    public static Task<AbolishAndReplaceResponse> AbolishAndReplaceAsync(
        this ICommandHandler<AbolishAndReplacePublicEntityGroupCommand, AbolishAndReplaceResponse> handler,
        AbolishAndReplaceRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new AbolishAndReplacePublicEntityGroupCommand(request, actor), ct);

    public static Task<PublicEntityEntryDto?> ProposeEditAsync(
        this ICommandHandler<ProposePublicEntityEditCommand, PublicEntityEntryDto?> handler,
        int entryId,
        ProposeEditRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new ProposePublicEntityEditCommand(entryId, request, actor), ct);

    public static Task<ParentEditSuggestionDto> SuggestParentEditAsync(
        this ICommandHandler<SuggestPublicEntityParentEditCommand, ParentEditSuggestionDto> handler,
        int entryId,
        SuggestParentEditRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new SuggestPublicEntityParentEditCommand(entryId, request, actor), ct);
}
