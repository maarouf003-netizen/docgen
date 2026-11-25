using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Documents;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق يحافظ على توقيعات خدمة البحث القديمة (`P4-01cr`):
/// يُمرَّر سجل معالجات البحث صراحةً ويُبنى الاستعلام داخليًا — فلا يتغير أي توقّع اختباري.
/// </summary>
public static class DocumentSearchQueryExtensions
{
    public static Task<PagedResult<DocumentResponse>> SearchDeletedAsync(
        this DocumentSearchHandlers handlers,
        string? query,
        int page,
        int perPage,
        int? visibleBranchId = null,
        int? visibleUserId = null,
        CancellationToken ct = default,
        int? ownerSectionId = null)
        => handlers.SearchDeleted.HandleAsync(
            new SearchDeletedDocumentsQuery(query, page, perPage, visibleBranchId, visibleUserId, ownerSectionId), ct);

    public static Task<PagedResult<DocumentListItemDto>> SearchDeletedListAsync(
        this DocumentSearchHandlers handlers,
        string? query,
        int page,
        int perPage,
        int? visibleBranchId = null,
        int? visibleUserId = null,
        CancellationToken ct = default,
        int? ownerSectionId = null,
        string? cursor = null)
        => handlers.SearchDeletedList.HandleAsync(
            new SearchDeletedDocumentListQuery(query, page, perPage, visibleBranchId, visibleUserId, ownerSectionId, cursor), ct);

    public static Task<PagedResult<DocumentResponse>> SearchAsync(
        this DocumentSearchHandlers handlers,
        string? query,
        string? status,
        string? applicant,
        string? court,
        string? lawyer,
        string? branch,
        string? administrativeBranch,
        string? executedEntity,
        string? publicEntityBranch,
        int page,
        int perPage,
        int? visibleBranchId = null,
        int? visibleUserId = null,
        CancellationToken ct = default,
        int? ownerSectionId = null)
        => handlers.Search.HandleAsync(
            new SearchDocumentsQuery(
                query, status, applicant, court, lawyer, branch, administrativeBranch,
                executedEntity, publicEntityBranch, page, perPage, visibleBranchId, visibleUserId,
                ownerSectionId), ct);

    public static Task<PagedResult<DocumentListItemDto>> SearchListAsync(
        this DocumentSearchHandlers handlers,
        string? query,
        string? status,
        string? applicant,
        string? court,
        string? lawyer,
        string? branch,
        string? administrativeBranch,
        string? executedEntity,
        string? publicEntityBranch,
        int page,
        int perPage,
        int? visibleBranchId = null,
        int? visibleUserId = null,
        CancellationToken ct = default,
        int? ownerSectionId = null,
        string? cursor = null)
        => handlers.SearchList.HandleAsync(
            new SearchDocumentListQuery(
                query, status, applicant, court, lawyer, branch, administrativeBranch,
                executedEntity, publicEntityBranch, page, perPage, visibleBranchId, visibleUserId,
                ownerSectionId, cursor), ct);

    public static Task<PagedResult<DocumentResponse>> SearchStruckOffAsync(
        this DocumentSearchHandlers handlers,
        string? query,
        int page,
        int perPage,
        int? visibleBranchId = null,
        int? visibleUserId = null,
        CancellationToken ct = default,
        int? ownerSectionId = null)
        => handlers.SearchStruckOff.HandleAsync(
            new SearchStruckOffDocumentsQuery(query, page, perPage, visibleBranchId, visibleUserId, ownerSectionId), ct);

    public static Task<PagedResult<DocumentListItemDto>> SearchStruckOffListAsync(
        this DocumentSearchHandlers handlers,
        string? query,
        int page,
        int perPage,
        int? visibleBranchId = null,
        int? visibleUserId = null,
        CancellationToken ct = default,
        int? ownerSectionId = null,
        string? cursor = null)
        => handlers.SearchStruckOffList.HandleAsync(
            new SearchStruckOffDocumentListQuery(query, page, perPage, visibleBranchId, visibleUserId, ownerSectionId, cursor), ct);

    public static Task<PagedResult<DocumentResponse>> SearchExecutedAsync(
        this DocumentSearchHandlers handlers,
        string? query,
        int page,
        int perPage,
        int? visibleBranchId = null,
        int? visibleUserId = null,
        CancellationToken ct = default,
        int? ownerSectionId = null)
        => handlers.SearchExecuted.HandleAsync(
            new SearchExecutedDocumentsQuery(query, page, perPage, visibleBranchId, visibleUserId, ownerSectionId), ct);

    public static Task<PagedResult<DocumentListItemDto>> SearchExecutedListAsync(
        this DocumentSearchHandlers handlers,
        string? query,
        int page,
        int perPage,
        int? visibleBranchId = null,
        int? visibleUserId = null,
        CancellationToken ct = default,
        int? ownerSectionId = null,
        string? cursor = null)
        => handlers.SearchExecutedList.HandleAsync(
            new SearchExecutedDocumentListQuery(query, page, perPage, visibleBranchId, visibleUserId, ownerSectionId, cursor), ct);

    public static Task<PagedResult<DocumentResponse>> SearchReferredToStartAsync(
        this DocumentSearchHandlers handlers,
        string? query,
        int page,
        int perPage,
        int? visibleBranchId = null,
        int? visibleUserId = null,
        CancellationToken ct = default,
        int? ownerSectionId = null)
        => handlers.SearchReferredToStart.HandleAsync(
            new SearchReferredToStartDocumentsQuery(query, page, perPage, visibleBranchId, visibleUserId, ownerSectionId), ct);

    public static Task<PagedResult<DocumentListItemDto>> SearchReferredToStartListAsync(
        this DocumentSearchHandlers handlers,
        string? query,
        int page,
        int perPage,
        int? visibleBranchId = null,
        int? visibleUserId = null,
        CancellationToken ct = default,
        int? ownerSectionId = null,
        string? cursor = null)
        => handlers.SearchReferredToStartList.HandleAsync(
            new SearchReferredToStartDocumentListQuery(query, page, perPage, visibleBranchId, visibleUserId, ownerSectionId, cursor), ct);

    public static Task<List<DocumentExportRow>> ExportAsync(
        this DocumentSearchHandlers handlers,
        string? query,
        string? status,
        string? applicant,
        string? court,
        string? lawyer,
        string? branch,
        string? administrativeBranch,
        string? executedEntity,
        string? publicEntityBranch,
        int? visibleBranchId = null,
        int? visibleUserId = null,
        CancellationToken ct = default,
        string? actorName = null,
        int? ownerSectionId = null)
        => handlers.Export.HandleAsync(
            new ExportDocumentsQuery(
                query, status, applicant, court, lawyer, branch, administrativeBranch,
                executedEntity, publicEntityBranch, visibleBranchId, visibleUserId, actorName,
                ownerSectionId), ct);

    public static Task<DocumentFilterOptions> GetFilterOptionsAsync(
        this DocumentSearchHandlers handlers,
        string? status,
        string? applicant,
        string? court,
        string? lawyer,
        string? branch,
        string? administrativeBranch,
        string? executedEntity,
        string? publicEntityBranch,
        int? visibleBranchId = null,
        int? visibleUserId = null,
        CancellationToken ct = default,
        int? ownerSectionId = null)
        => handlers.FilterOptions.HandleAsync(
            new GetDocumentFilterOptionsQuery(
                status, applicant, court, lawyer, branch, administrativeBranch,
                executedEntity, publicEntityBranch, visibleBranchId, visibleUserId, ownerSectionId), ct);
}
