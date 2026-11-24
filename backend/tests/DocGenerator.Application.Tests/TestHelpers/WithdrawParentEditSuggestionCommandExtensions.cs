using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.PublicEntities;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>جسر اختباري ضيق لتوقيع <c>WithdrawParentEditSuggestionAsync</c> القديم (`P4-01bo`).</summary>
public static class WithdrawParentEditSuggestionCommandExtensions
{
    public static Task<ParentEditSuggestionDto?> WithdrawParentEditSuggestionAsync(
        this ICommandHandler<WithdrawParentEditSuggestionCommand, ParentEditSuggestionDto?> handler,
        int suggestionId,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new WithdrawParentEditSuggestionCommand(suggestionId, actor), ct);
}
