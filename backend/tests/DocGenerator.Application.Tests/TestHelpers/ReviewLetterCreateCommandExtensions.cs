using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.ReviewLetters;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>جسر اختباري ضيق لتوقيع <c>CreateAsync</c> القديم (`P4-01bm`).</summary>
public static class ReviewLetterCreateCommandExtensions
{
    public static Task<ReviewLetterDto> CreateAsync(
        this ICommandHandler<CreateReviewLetterCommand, ReviewLetterDto> handler,
        CreateReviewLetterRequest request,
        int actorUserId,
        string? actorName,
        int actorBranchId,
        CancellationToken ct = default)
        => handler.HandleAsync(
            new CreateReviewLetterCommand(request, actorUserId, actorName, actorBranchId), ct);
}
