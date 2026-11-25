using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Correspondences;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسور اختبارية لمعالجات قراءات المراسلات (`P4-01cn`): تحافظ على توقيعات
/// الخدمة القديمة فوق معالجات الاستعلام — منطق الاختبارات كما هو.
/// </summary>
public static class CorrespondenceReadQueryExtensions
{
    public static Task<PagedResult<CorrespondenceListItemDto>> SearchAsync(
        this IQueryHandler<SearchCorrespondencesQuery, PagedResult<CorrespondenceListItemDto>> handler,
        int actorUserId,
        UserRole role,
        int? actorBranchId,
        string? q,
        string? governorate,
        string? importance,
        int page,
        int perPage,
        CancellationToken ct = default,
        int? actorSectionId = null)
        => handler.HandleAsync(
            new SearchCorrespondencesQuery(
                actorUserId, role, actorBranchId, q, governorate, importance, page, perPage, actorSectionId),
            ct);

    public static Task<CorrespondenceDto> GetByIdAsync(
        this IQueryHandler<GetCorrespondenceQuery, CorrespondenceDto> handler,
        int id,
        int actorUserId,
        UserRole role,
        int? actorBranchId,
        CancellationToken ct = default,
        int? actorSectionId = null)
        => handler.HandleAsync(
            new GetCorrespondenceQuery(id, actorUserId, role, actorBranchId, actorSectionId), ct);

    public static Task<int> CountUnseenAsync(
        this IQueryHandler<CountUnseenCorrespondencesQuery, int> handler,
        int actorUserId,
        CancellationToken ct = default)
        => handler.HandleAsync(new CountUnseenCorrespondencesQuery(actorUserId), ct);

    public static Task<List<CorrespondenceListItemDto>> ListByDocumentAsync(
        this IQueryHandler<ListCorrespondencesByDocumentQuery, List<CorrespondenceListItemDto>> handler,
        int documentId,
        int actorUserId,
        UserRole role,
        int? actorBranchId,
        CancellationToken ct = default,
        int? actorSectionId = null)
        => handler.HandleAsync(
            new ListCorrespondencesByDocumentQuery(documentId, actorUserId, role, actorBranchId, actorSectionId),
            ct);

    public static Task<List<string>> GetGovernoratesAsync(
        this IQueryHandler<GetCorrespondenceGovernoratesQuery, List<string>> handler,
        CancellationToken ct = default)
        => handler.HandleAsync(new GetCorrespondenceGovernoratesQuery(), ct);

    public static Task<List<CorrespondenceTargetDto>> SearchTargetsAsync(
        this IQueryHandler<SearchCorrespondenceTargetsQuery, List<CorrespondenceTargetDto>> handler,
        int actorUserId,
        UserRole role,
        int? actorBranchId,
        string? q,
        int? documentId,
        CancellationToken ct = default,
        int? actorSectionId = null)
        => handler.HandleAsync(
            new SearchCorrespondenceTargetsQuery(actorUserId, role, actorBranchId, q, documentId, actorSectionId),
            ct);
}
