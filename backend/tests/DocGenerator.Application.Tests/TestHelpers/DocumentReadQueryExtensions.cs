using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Documents;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق يحافظ على توقيعات <c>IDocumentService</c> القديمة (`P4-01cq`):
/// يُمرَّر المعالج صراحةً ويُبنى الاستعلام داخليًا — فلا يتغير أي توقّع اختباري.
/// </summary>
public static class DocumentReadQueryExtensions
{
    public static Task<DocumentResponse?> GetAsync(
        this IQueryHandler<GetDocumentQuery, DocumentResponse?> handler,
        int documentId,
        CancellationToken ct = default)
        => handler.HandleAsync(new GetDocumentQuery(documentId), ct);

    public static Task<DocumentAccessInfo?> GetAccessInfoAsync(
        this IQueryHandler<GetDocumentAccessInfoQuery, DocumentAccessInfo?> handler,
        int documentId,
        CancellationToken ct = default)
        => handler.HandleAsync(new GetDocumentAccessInfoQuery(documentId), ct);

    public static Task<DocumentResponse?> GetDeletedAsync(
        this IQueryHandler<GetDeletedDocumentQuery, DocumentResponse?> handler,
        int documentId,
        CancellationToken ct = default)
        => handler.HandleAsync(new GetDeletedDocumentQuery(documentId), ct);

    public static Task<int> CountFilesByOwnerAsync(
        this IQueryHandler<CountFilesByOwnerQuery, int> handler,
        int ownerId,
        int? scopeBranchId,
        CancellationToken ct = default,
        int? ownerSectionId = null)
        => handler.HandleAsync(new CountFilesByOwnerQuery(ownerId, scopeBranchId, ownerSectionId), ct);

    public static Task<PagedResult<RotationDocumentDto>> GetRotationListAsync(
        this IQueryHandler<GetDocumentRotationListQuery, PagedResult<RotationDocumentDto>> handler,
        int userId,
        int page,
        int perPage,
        CancellationToken ct = default)
        => handler.HandleAsync(new GetDocumentRotationListQuery(userId, page, perPage), ct);

    public static Task<List<BaseNumberHistoryDto>> GetBaseNumberHistoryAsync(
        this IQueryHandler<GetDocumentBaseNumberHistoryQuery, List<BaseNumberHistoryDto>> handler,
        int documentId,
        CancellationToken ct = default)
        => handler.HandleAsync(new GetDocumentBaseNumberHistoryQuery(documentId), ct);

    public static Task<List<BaseNumberHistoryDto>> GetDeletedBaseNumberHistoryAsync(
        this IQueryHandler<GetDeletedDocumentBaseNumberHistoryQuery, List<BaseNumberHistoryDto>> handler,
        int documentId,
        CancellationToken ct = default)
        => handler.HandleAsync(new GetDeletedDocumentBaseNumberHistoryQuery(documentId), ct);
}
