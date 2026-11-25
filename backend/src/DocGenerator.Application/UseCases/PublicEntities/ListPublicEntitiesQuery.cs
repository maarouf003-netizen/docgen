using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>قائمة السجل لشاشة الإدارة/البحث — قراءة صرفة (بلا كتابة ولا معاملة).</summary>
public sealed record ListPublicEntitiesQuery(EntityRegistryListQuery Query)
    : IQuery<PagedResult<PublicEntityEntryDto>>;

internal sealed class ListPublicEntitiesQueryHandler
    : QueryHandlerBase<ListPublicEntitiesQuery, PagedResult<PublicEntityEntryDto>>
{
    private readonly IPublicEntityRepository _entities;

    public ListPublicEntitiesQueryHandler(
        IPublicEntityRepository entities,
        IEnumerable<IPipelineBehavior<ListPublicEntitiesQuery, PagedResult<PublicEntityEntryDto>>> behaviors)
        : base(behaviors)
        => _entities = entities;

    protected override async Task<PagedResult<PublicEntityEntryDto>> HandleCoreAsync(
        ListPublicEntitiesQuery query, CancellationToken ct)
    {
        var request = query.Query;
        var page = Math.Max(1, request.Page);
        var perPage = Math.Clamp(request.PerPage <= 0 ? 20 : request.PerPage, 1, 100);
        var qNorm = ArabicNameNormalizer.Normalize(request.Q);
        var governorate = PublicEntityService.NormalizeOptional(request.Governorate);
        var status = PublicEntityService.NormalizeOptional(request.Status);
        var branchName = PublicEntityService.NormalizeOptional(request.BranchName);

        var groups = await _entities.ListGroupsWithEntriesAsync(ct);
        var entries = groups
            .SelectMany(g => g.Entries.Select(e => (Group: g, Entry: e)))
            .Where(x => request.IncludePending || x.Entry.Status != EntityStatusCatalog.Pending)
            .Where(x => request.IncludeInactive || x.Entry.IsActive)
            .Where(x => governorate is null || x.Entry.IsParentEntity || x.Entry.Governorate == governorate)
            .Where(x => status is null || x.Entry.Status == status)
            .Where(x => branchName is null || x.Entry.IsParentEntity || x.Entry.BranchName == branchName)
            .Where(x => qNorm.Length == 0
                || ArabicNameNormalizer.Normalize(x.Group.CanonicalName).Contains(qNorm)
                || x.Entry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText).Contains(qNorm)))
            .OrderBy(x => x.Group.CanonicalName, StringComparer.Ordinal)
            .ThenBy(x => x.Entry.Governorate, StringComparer.Ordinal)
            .ThenBy(x => x.Entry.BranchName, StringComparer.Ordinal)
            // فاصل حتمي: مجموعتان بالاسم/المحافظة/الفرع نفسهما (أو حقول فارغة) تترك
            // حدود الصفحات غير حتمية بلا هذا الفاصل.
            .ThenBy(x => x.Entry.Id)
            .ToList();

        var result = new PagedResult<PublicEntityEntryDto>
        {
            Page = page,
            PerPage = perPage,
            TotalCount = entries.Count,
        };
        result.Items = entries
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(x => PublicEntityService.ToEntryDto(x.Group, x.Entry))
            .ToList();
        return result;
    }
}
