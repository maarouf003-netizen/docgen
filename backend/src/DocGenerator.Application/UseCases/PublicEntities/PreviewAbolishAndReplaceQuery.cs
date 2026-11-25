using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>معاينة «الحلول»: إلغاء عدة هويات أم واستبدالها بهوية جديدة — قراءة صرفة.</summary>
public sealed record PreviewAbolishAndReplaceQuery(AbolishReplacePreviewRequest Request)
    : IQuery<AbolishReplacePreviewResponse>;

internal sealed class PreviewAbolishAndReplaceQueryHandler
    : QueryHandlerBase<PreviewAbolishAndReplaceQuery, AbolishReplacePreviewResponse>
{
    private readonly IPublicEntityRepository _entities;
    private readonly IUserRepository _users;

    public PreviewAbolishAndReplaceQueryHandler(
        IPublicEntityRepository entities,
        IUserRepository users,
        IEnumerable<IPipelineBehavior<PreviewAbolishAndReplaceQuery, AbolishReplacePreviewResponse>> behaviors)
        : base(behaviors)
    {
        _entities = entities;
        _users = users;
    }

    protected override async Task<AbolishReplacePreviewResponse> HandleCoreAsync(
        PreviewAbolishAndReplaceQuery query, CancellationToken ct)
    {
        var request = query.Request;
        if (request.AbolishedGroupIds is null || request.AbolishedGroupIds.Count == 0)
            throw new ArgumentException("حدد هوية أم واحدة على الأقل للإلغاء");

        var names = new List<string>();
        var affectedDocs = 0;
        var branches = new HashSet<string>(StringComparer.Ordinal);
        var abolishedGroupIds = new HashSet<int>(request.AbolishedGroupIds);

        foreach (var id in request.AbolishedGroupIds.Distinct())
        {
            var group = await _entities.GetGroupAsync(id, ct)
                ?? throw new ArgumentException($"الهوية الأم #{id} غير موجودة");
            if (!group.IsActive)
                throw new ArgumentException($"الهوية الأم «{group.CanonicalName}» غير نشطة");
            if (group.Entries.Any(e => e.NeedsReview))
                throw new ArgumentException($"يجب إتمام مراجعة جميع قيود «{group.CanonicalName}» قبل الإلغاء");
            names.Add(group.CanonicalName);
            affectedDocs += await PublicEntityService.CountDocumentsForGroupAsync(_entities, id, ct);
            foreach (var b in await PublicEntityService.BranchNamesForGroupAsync(_entities, id, ct))
                branches.Add(b);
        }

        var delegates = await _users.ListEntityManagersByGroupIdsAsync(abolishedGroupIds, ct);
        return new AbolishReplacePreviewResponse(
            names, request.AbolishedGroupIds.Count,
            await PublicEntityService.CountActiveEntriesForGroupsAsync(_entities, abolishedGroupIds, ct),
            affectedDocs, delegates.Count, branches.OrderBy(x => x).ToList());
    }
}
