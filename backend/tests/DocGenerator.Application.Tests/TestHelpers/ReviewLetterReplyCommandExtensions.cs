using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.ReviewLetters;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيعَي <c>AddAddendumAsync</c>/<c>ReplyAsync</c>
/// القديمين فوق معالجي الأوامر — منطق الاختبارات كما هو (`P4-01bl`).
/// </summary>
public static class ReviewLetterReplyCommandExtensions
{
    public static Task<ReviewLetterMessageDto> AddAddendumAsync(
        this ICommandHandler<AddReviewLetterAddendumCommand, ReviewLetterMessageDto> handler,
        int letterId,
        AddReviewLetterAddendumRequest request,
        int actorUserId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new AddReviewLetterAddendumCommand(letterId, request, actorUserId, actorName), ct);

    public static Task<ReviewLetterMessageDto> ReplyAsync(
        this ICommandHandler<ReplyReviewLetterCommand, ReviewLetterMessageDto> handler,
        int letterId,
        ReplyReviewLetterRequest request,
        int actorUserId,
        string? actorName,
        int actorBranchId,
        CancellationToken ct = default,
        UserRole role = UserRole.Head,
        int? actorSectionId = null)
        => handler.HandleAsync(
            new ReplyReviewLetterCommand(letterId, request, actorUserId, actorName, actorBranchId, role, actorSectionId),
            ct);
}
