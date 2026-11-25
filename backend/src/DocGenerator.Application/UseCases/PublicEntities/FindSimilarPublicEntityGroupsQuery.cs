using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>أقرب المشابهات لجهة محددة — قراءة صرفة (بلا كتابة ولا معاملة).</summary>
public sealed record FindSimilarPublicEntityGroupsQuery(int GroupId, double Threshold, int MaxResults)
    : IQuery<SimilarToResponse>;

internal sealed class FindSimilarPublicEntityGroupsQueryHandler
    : QueryHandlerBase<FindSimilarPublicEntityGroupsQuery, SimilarToResponse>
{
    private readonly IPublicEntityRepository _entities;

    public FindSimilarPublicEntityGroupsQueryHandler(
        IPublicEntityRepository entities,
        IEnumerable<IPipelineBehavior<FindSimilarPublicEntityGroupsQuery, SimilarToResponse>> behaviors)
        : base(behaviors)
        => _entities = entities;

    protected override async Task<SimilarToResponse> HandleCoreAsync(
        FindSimilarPublicEntityGroupsQuery query, CancellationToken ct)
    {
        var groupId = query.GroupId;
        var threshold = query.Threshold;
        var maxResults = query.MaxResults;
        var target = await _entities.GetGroupAsync(groupId, ct)
            ?? throw new ArgumentException("المجموعة غير موجودة");
        if (!target.IsActive)
            throw new ArgumentException("المجموعة غير نشطة");

        var t = threshold <= 0 ? ArabicNameSimilarity.DefaultSimilarToThreshold : Math.Clamp(threshold, 0, 1);
        var max = maxResults <= 0 ? ArabicNameSimilarity.DefaultMaxSimilarResults : Math.Min(maxResults, 50);
        var groups = await _entities.ListGroupsWithEntriesAsync(ct);

        var ranked = groups
            .Where(g => g.IsActive && g.Id != groupId)
            .Select(g => (Group: g, Sim: ArabicNameSimilarity.Similarity(target.CanonicalName, g.CanonicalName)))
            .Where(x => x.Sim >= t)
            .OrderByDescending(x => x.Sim)
            .ThenBy(x => x.Group.CanonicalName, StringComparer.Ordinal)
            .Take(max)
            .ToList();

        var ids = ranked.Select(r => r.Group.Id).ToList();
        var linkedCounts = await _entities.CountLinkedDocumentsByGroupIdsAsync(ids, ct);

        var items = ranked.Select(r => new SimilarToItemDto(
            r.Group.Id,
            r.Group.CanonicalName,
            r.Group.EntityType,
            r.Group.Entries.Count(e => e.IsActive),
            linkedCounts.TryGetValue(r.Group.Id, out var c) ? c : 0,
            Math.Round(r.Sim, 3))).ToList();

        return new SimilarToResponse(target.Id, target.CanonicalName, items, t);
    }
}
