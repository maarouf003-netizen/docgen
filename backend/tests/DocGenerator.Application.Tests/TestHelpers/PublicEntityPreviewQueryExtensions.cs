using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.PublicEntities;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسور اختبارية لمعالجات معاينات سجل الجهات (`P4-01cg`): تحافظ على توقيعات
/// <c>IPublicEntityService</c> القديمة فوق معالجات الاستعلام — منطق الاختبارات كما هو.
/// </summary>
public static class PublicEntityPreviewQueryExtensions
{
    public static Task<MergePreviewResponse> PreviewMergeAsync(
        this IQueryHandler<PreviewMergeQuery, MergePreviewResponse> handler,
        MergePreviewRequest request,
        CancellationToken ct = default)
        => handler.HandleAsync(new PreviewMergeQuery(request), ct);

    public static Task<UnifyNamesPreviewResponse> PreviewUnifyAsync(
        this IQueryHandler<PreviewUnifyQuery, UnifyNamesPreviewResponse> handler,
        UnifyNamesPreviewRequest request,
        CancellationToken ct = default)
        => handler.HandleAsync(new PreviewUnifyQuery(request), ct);

    public static Task<RenameGroupPreviewResponse> PreviewRenameGroupAsync(
        this IQueryHandler<PreviewRenameGroupQuery, RenameGroupPreviewResponse> handler,
        RenameGroupPreviewRequest request,
        CancellationToken ct = default)
        => handler.HandleAsync(new PreviewRenameGroupQuery(request), ct);

    public static Task<AbolishReplacePreviewResponse> PreviewAbolishAndReplaceAsync(
        this IQueryHandler<PreviewAbolishAndReplaceQuery, AbolishReplacePreviewResponse> handler,
        AbolishReplacePreviewRequest request,
        CancellationToken ct = default)
        => handler.HandleAsync(new PreviewAbolishAndReplaceQuery(request), ct);

    public static Task<BranchActionPreviewResponse> PreviewBranchActionAsync(
        this IQueryHandler<PreviewBranchActionQuery, BranchActionPreviewResponse> handler,
        int groupId,
        PreviewBranchActionRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new PreviewBranchActionQuery(groupId, request, actor), ct);
}
