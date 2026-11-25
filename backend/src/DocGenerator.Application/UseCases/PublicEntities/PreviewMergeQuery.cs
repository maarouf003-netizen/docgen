using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>معاينة دمج عدة هويات أم في هوية ناجية — قراءة صرفة (بلا كتابة ولا معاملة).</summary>
public sealed record PreviewMergeQuery(MergePreviewRequest Request) : IQuery<MergePreviewResponse>;

internal sealed class PreviewMergeQueryHandler : QueryHandlerBase<PreviewMergeQuery, MergePreviewResponse>
{
    private readonly IPublicEntityRepository _entities;

    public PreviewMergeQueryHandler(
        IPublicEntityRepository entities,
        IEnumerable<IPipelineBehavior<PreviewMergeQuery, MergePreviewResponse>> behaviors)
        : base(behaviors)
        => _entities = entities;

    protected override async Task<MergePreviewResponse> HandleCoreAsync(PreviewMergeQuery query, CancellationToken ct)
    {
        var request = query.Request;
        var survivorGroup = await _entities.GetGroupAsync(request.SurvivorGroupId, ct)
            ?? throw new ArgumentException("الهوية الأم الناجية غير موجودة");
        if (!survivorGroup.IsActive)
            throw new ArgumentException("الهوية الأم الناجية غير نشطة");

        if (request.AbsorbedGroupIds.Count == 0)
            throw new ArgumentException("حدد هوية أم واحدة على الأقل للدمج");

        if (request.AbsorbedGroupIds.Contains(request.SurvivorGroupId))
            throw new ArgumentException("لا يمكن دمج هوية في نفسها");

        var survivorEntryEntities = await _entities.ListEntriesByGroupAsync(survivorGroup.Id, ct);
        var activeSurvivorEntries = survivorEntryEntities.Where(e => e.IsActive).ToList();
        if (activeSurvivorEntries.Count == 0)
            throw new ArgumentException("الهوية الأم الناجية بلا قيود نشطة");

        var warnings = new List<string>();
        var absorbedDtos = new List<AbsorbedGroupPreviewDto>();
        int totalAffected = 0;

        foreach (var ae in survivorEntryEntities.Where(e => e.NeedsReview))
            warnings.Add($"القيد «{ae.Governorate}/{ae.BranchName}» في «{survivorGroup.CanonicalName}» (الناجي) بانتظار المراجعة");

        foreach (var absorbedId in request.AbsorbedGroupIds.Distinct())
        {
            var absorbedGroup = await _entities.GetGroupAsync(absorbedId, ct)
                ?? throw new ArgumentException($"الهوية الأم #{absorbedId} غير موجودة");
            if (!absorbedGroup.IsActive)
                throw new ArgumentException($"الهوية الأم «{absorbedGroup.CanonicalName}» غير نشطة");

            var absorbedEntryEntities = await _entities.ListEntriesByGroupAsync(absorbedId, ct);

            foreach (var ae in absorbedEntryEntities.Where(e => e.NeedsReview))
                warnings.Add($"القيد «{ae.Governorate}/{ae.BranchName}» في «{absorbedGroup.CanonicalName}» بانتظار المراجعة");

            var entryDtos = new List<AbsorbedEntryPreviewDto>();
            int groupDocCount = 0;

            foreach (var ae in absorbedEntryEntities)
            {
                var matchedSurvivor = activeSurvivorEntries
                    .FirstOrDefault(se => se.Governorate == ae.Governorate && se.BranchName == ae.BranchName);

                var defaultEntry = activeSurvivorEntries.First();
                int mappedToId = matchedSurvivor?.Id ?? defaultEntry.Id;
                bool conflictsWithSurvivor = matchedSurvivor is null;

                var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(ae.Id, ct);
                int docCount = linkedDocs.Count;
                groupDocCount += docCount;

                entryDtos.Add(new AbsorbedEntryPreviewDto(
                    ae.Id, ae.Governorate, ae.BranchName,
                    docCount,
                    mappedToId, conflictsWithSurvivor));
            }

            totalAffected += groupDocCount;

            var aliases = absorbedEntryEntities
                .SelectMany(e => e.Aliases)
                .Select(a => a.AliasText)
                .Distinct()
                .ToList();

            absorbedDtos.Add(new AbsorbedGroupPreviewDto(
                absorbedGroup.Id, absorbedGroup.CanonicalName,
                entryDtos, groupDocCount, aliases));
        }

        return new MergePreviewResponse(survivorGroup.CanonicalName, absorbedDtos, totalAffected, warnings);
    }
}
