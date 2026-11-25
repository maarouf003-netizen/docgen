using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.ReviewLetters;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسور اختبارية لمعالجات قراءات كتب المطالعة (`P4-01cl`): تحافظ على توقيعات
/// خدمة المطالعة القديمة فوق معالجات الاستعلام — منطق الاختبارات كما هو.
/// </summary>
public static class ReviewLetterReadQueryExtensions
{
    public static Task<PagedResult<ReviewLetterListItemDto>> SearchAsync(
        this IQueryHandler<SearchReviewLettersQuery, PagedResult<ReviewLetterListItemDto>> handler,
        int actorUserId,
        UserRole role,
        int? actorBranchId,
        string? q,
        int page,
        int perPage,
        string? administrativeBranch,
        CancellationToken ct = default,
        int? actorSectionId = null)
        => handler.HandleAsync(
            new SearchReviewLettersQuery(actorUserId, role, actorBranchId, q, page, perPage,
                administrativeBranch, actorSectionId),
            ct);

    public static Task<ReviewLetterDto> GetByIdAsync(
        this IQueryHandler<GetReviewLetterQuery, ReviewLetterDto> handler,
        int id,
        int actorUserId,
        UserRole role,
        int? actorBranchId,
        CancellationToken ct = default,
        int? actorSectionId = null)
        => handler.HandleAsync(new GetReviewLetterQuery(id, actorUserId, role, actorBranchId, actorSectionId), ct);

    public static Task<int> CountPendingForHeadAsync(
        this IQueryHandler<CountPendingReviewLettersQuery, int> handler,
        int branchId,
        int? ownerSectionId = null,
        CancellationToken ct = default)
        => handler.HandleAsync(new CountPendingReviewLettersQuery(branchId, ownerSectionId), ct);

    public static Task<int> CountUnseenRepliesForLawyerAsync(
        this IQueryHandler<CountUnseenReviewRepliesQuery, int> handler,
        int actorUserId,
        CancellationToken ct = default)
        => handler.HandleAsync(new CountUnseenReviewRepliesQuery(actorUserId), ct);

    public static Task<List<ReviewLetterListItemDto>> ListByDocumentAsync(
        this IQueryHandler<ListReviewLettersByDocumentQuery, List<ReviewLetterListItemDto>> handler,
        int documentId,
        int actorUserId,
        UserRole role,
        int? actorBranchId,
        CancellationToken ct = default,
        int? actorSectionId = null)
        => handler.HandleAsync(
            new ListReviewLettersByDocumentQuery(documentId, actorUserId, role, actorBranchId, actorSectionId),
            ct);

    public static Task<List<string>> GetAdministrativeBranchesAsync(
        this IQueryHandler<GetReviewLetterAdministrativeBranchesQuery, List<string>> handler,
        CancellationToken ct = default)
        => handler.HandleAsync(new GetReviewLetterAdministrativeBranchesQuery(), ct);
}
