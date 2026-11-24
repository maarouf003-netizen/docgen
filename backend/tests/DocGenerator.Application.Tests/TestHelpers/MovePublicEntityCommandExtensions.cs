using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.PublicEntities;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>جسر اختباري ضيق لتوقيعَي <c>MoveEntryAsync</c>/<c>MoveAllEntriesAsync</c> القديمين (`P4-01bw`).</summary>
public static class MovePublicEntityCommandExtensions
{
    public static Task<MoveEntryResponse> MoveEntryAsync(
        this ICommandHandler<MovePublicEntityEntryCommand, MoveEntryResponse> handler,
        int entryId,
        MoveEntryRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new MovePublicEntityEntryCommand(entryId, request, actor), ct);

    public static Task<MoveAllEntriesResponse> MoveAllEntriesAsync(
        this ICommandHandler<MoveAllPublicEntityEntriesCommand, MoveAllEntriesResponse> handler,
        MoveAllEntriesRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new MoveAllPublicEntityEntriesCommand(request, actor), ct);
}
