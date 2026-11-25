using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>معاينة توحيد التسمية N←1 (محاكاة بركة الناجين بلا أي كتابة).</summary>
public sealed record PreviewUnifyQuery(UnifyNamesPreviewRequest Request) : IQuery<UnifyNamesPreviewResponse>;

internal sealed class PreviewUnifyQueryHandler : QueryHandlerBase<PreviewUnifyQuery, UnifyNamesPreviewResponse>
{
    private readonly IPublicEntityRepository _entities;

    public PreviewUnifyQueryHandler(
        IPublicEntityRepository entities,
        IEnumerable<IPipelineBehavior<PreviewUnifyQuery, UnifyNamesPreviewResponse>> behaviors)
        : base(behaviors)
        => _entities = entities;

    protected override async Task<UnifyNamesPreviewResponse> HandleCoreAsync(PreviewUnifyQuery query, CancellationToken ct)
    {
        var request = query.Request;
        var targetGroup = await _entities.GetGroupAsync(request.TargetGroupId, ct)
            ?? throw new ArgumentException("الهوية الأم الهدف غير موجودة");
        if (!targetGroup.IsActive)
            throw new ArgumentException("الهوية الأم الهدف غير نشطة");

        if (request.AbsorbedGroupIds.Count == 0)
            throw new ArgumentException("حدد هوية أم واحدة على الأقل للتوحيد");

        if (request.AbsorbedGroupIds.Contains(request.TargetGroupId))
            throw new ArgumentException("لا يمكن توحيد هوية مع نفسها");

        var targetEntries = await _entities.ListEntriesByGroupAsync(targetGroup.Id, ct);
        var activeTarget = targetEntries.Where(e => e.IsActive).ToList();
        // بركة الناجين: محاكاة مطابقة لحلقة التنفيذ في `UnifyPublicEntityNamesCommand` — تبدأ بنسخ القيود
        // النشطة للهدف ويُلحق بها كل قيد يُنقل؛ المطابقة خام (Ordinal) على (المحافظة/الفرع).
        var survivorPool = new List<PublicEntity>(activeTarget);

        var warnings = new List<string>();
        var absorbedDtos = new List<AbsorbedGroupUnifyPreviewDto>();
        var foldEntries = new List<(int GroupId, string GroupName, PublicEntity Entry)>();
        int totalToMove = 0;

        foreach (var ae in targetEntries.Where(e => e.NeedsReview))
            warnings.Add($"القيد «{ae.Governorate}/{ae.BranchName}» في «{targetGroup.CanonicalName}» (الهدف) بانتظار المراجعة");

        foreach (var absorbedId in request.AbsorbedGroupIds.Distinct())
        {
            var absorbedGroup = await _entities.GetGroupAsync(absorbedId, ct)
                ?? throw new ArgumentException($"الهوية الأم #{absorbedId} غير موجودة");
            if (!absorbedGroup.IsActive)
                throw new ArgumentException($"الهوية الأم «{absorbedGroup.CanonicalName}» غير نشطة");

            var absorbedEntries = await _entities.ListEntriesByGroupAsync(absorbedId, ct);
            foreach (var ae in absorbedEntries.Where(e => e.NeedsReview))
                warnings.Add($"القيد «{ae.Governorate}/{ae.BranchName}» في «{absorbedGroup.CanonicalName}» بانتظار المراجعة");

            if (!string.Equals(absorbedGroup.EntityType, targetGroup.EntityType, StringComparison.OrdinalIgnoreCase))
                warnings.Add($"تنبيه: نوع الجهة مختلف — «{absorbedGroup.CanonicalName}» ({absorbedGroup.EntityType}) و«{targetGroup.CanonicalName}» ({targetGroup.EntityType})");

            var activeAbsorbed = absorbedEntries.Where(e => e.IsActive).ToList();

            foreach (var ae in activeAbsorbed)
            {
                var survivor = survivorPool.FirstOrDefault(se => se.Governorate == ae.Governorate && se.BranchName == ae.BranchName);
                if (survivor is not null)
                {
                    // الطي لا يُعدّ نقلًا: يُبطل القيد المطابق ويُرحّل روابطه إلى الناجي.
                    foldEntries.Add((absorbedGroup.Id, absorbedGroup.CanonicalName, ae));
                }
                else
                {
                    totalToMove++;
                    survivorPool.Add(ae);
                }
            }

            var govs = activeAbsorbed.Select(e => e.Governorate).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
            absorbedDtos.Add(new AbsorbedGroupUnifyPreviewDto(absorbedGroup.Id, absorbedGroup.CanonicalName, activeAbsorbed.Count, govs));
        }

        // عدّ ملفات القيود المزمع طيّها دفعة واحدة ثم نبني تفاصيل الطي بالترتيب.
        var linkedCounts = await _entities.CountLinkedDocumentsByEntryIdsAsync(foldEntries.Select(f => f.Entry.Id).ToList(), ct);
        var folds = foldEntries.Select(f => new EntryFoldPreviewDto(
            f.GroupId,
            f.GroupName,
            f.Entry.Governorate,
            f.Entry.BranchName,
            linkedCounts.TryGetValue(f.Entry.Id, out var count) ? count : 0)).ToList();

        return new UnifyNamesPreviewResponse(targetGroup.CanonicalName, absorbedDtos, totalToMove, folds.Count, folds, warnings);
    }
}
