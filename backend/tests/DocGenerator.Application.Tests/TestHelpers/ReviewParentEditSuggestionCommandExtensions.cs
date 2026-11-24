using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.PublicEntities;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>جسر اختباري ضيق لتوقيع <c>ReviewParentEditSuggestionAsync</c> القديم (`P4-01bp`).</summary>
public static class ReviewParentEditSuggestionCommandExtensions
{
    public static Task<ParentEditSuggestionDto?> ReviewParentEditSuggestionAsync(
        this ICommandHandler<ReviewParentEditSuggestionCommand, ParentEditSuggestionDto?> handler,
        int suggestionId,
        ReviewParentEditSuggestionRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new ReviewParentEditSuggestionCommand(suggestionId, request, actor), ct);
}
