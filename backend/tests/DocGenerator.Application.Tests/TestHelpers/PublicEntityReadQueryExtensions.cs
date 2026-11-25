using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.PublicEntities;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسور اختبارية لمعالجات قراءات سجل الجهات (`P4-01cj`): تحافظ على توقيعات
/// الخدمة القديمة فوق معالجات الاستعلام — منطق الاختبارات كما هو.
/// </summary>
public static class PublicEntityReadQueryExtensions
{
    public static Task<PagedResult<PublicEntityEntryDto>> ListAsync(
        this IQueryHandler<ListPublicEntitiesQuery, PagedResult<PublicEntityEntryDto>> handler,
        EntityRegistryListQuery query,
        CancellationToken ct = default)
        => handler.HandleAsync(new ListPublicEntitiesQuery(query), ct);

    public static Task<List<PublicEntityEntryDto>> ListNeedsReviewAsync(
        this IQueryHandler<ListPublicEntitiesNeedingReviewQuery, List<PublicEntityEntryDto>> handler,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new ListPublicEntitiesNeedingReviewQuery(actor), ct);

    public static Task<int> CountNeedsReviewAsync(
        this IQueryHandler<CountPublicEntitiesNeedingReviewQuery, int> handler,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new CountPublicEntitiesNeedingReviewQuery(actor), ct);

    public static Task<PagedResult<EntityChangeEventDto>> ListChangeEventsAsync(
        this IQueryHandler<ListPublicEntityChangeEventsQuery, PagedResult<EntityChangeEventDto>> handler,
        EntityChangeEventQuery query,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new ListPublicEntityChangeEventsQuery(query, actor), ct);

    public static Task<Stream> ExportChangeEventsAsync(
        this IQueryHandler<ExportPublicEntityChangeEventsQuery, Stream> handler,
        EntityChangeEventQuery query,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new ExportPublicEntityChangeEventsQuery(query, actor), ct);

    public static Task<ImportPreviewResponse> PreviewImportAsync(
        this IQueryHandler<PreviewPublicEntityImportQuery, ImportPreviewResponse> handler,
        CancellationToken ct = default)
        => handler.HandleAsync(new PreviewPublicEntityImportQuery(), ct);

    public static Task<PagedResult<PublicEntityGroupDto>> ListGroupsAsync(
        this IQueryHandler<ListPublicEntityGroupsQuery, PagedResult<PublicEntityGroupDto>> handler,
        EntityGroupListQuery query,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new ListPublicEntityGroupsQuery(query, actor), ct);

    public static Task<SimilarToResponse> FindSimilarToGroupAsync(
        this IQueryHandler<FindSimilarPublicEntityGroupsQuery, SimilarToResponse> handler,
        int groupId,
        double threshold,
        int maxResults,
        CancellationToken ct = default)
        => handler.HandleAsync(new FindSimilarPublicEntityGroupsQuery(groupId, threshold, maxResults), ct);

    public static Task<IReadOnlyList<PublicEntityEntryDto>> ListEntriesByGroupAsync(
        this IQueryHandler<ListPublicEntityGroupEntriesQuery, IReadOnlyList<PublicEntityEntryDto>> handler,
        int groupId,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new ListPublicEntityGroupEntriesQuery(groupId, actor), ct);

    public static Task<PagedResult<ParentEditSuggestionDto>> ListParentEditSuggestionsAsync(
        this IQueryHandler<ListParentEditSuggestionsQuery, PagedResult<ParentEditSuggestionDto>> handler,
        ParentEditSuggestionListQuery query,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new ListParentEditSuggestionsQuery(query, actor), ct);
}
