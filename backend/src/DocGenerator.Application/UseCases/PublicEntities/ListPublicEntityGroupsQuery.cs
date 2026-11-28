using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>قائمة المجموعات (الهويات الأم) مع ترقيم وبحث — قراءة صرفة بلا كتابة.</summary>
public sealed record ListPublicEntityGroupsQuery(EntityGroupListQuery Query, EntityRegistryActor Actor)
    : IQuery<PagedResult<PublicEntityGroupDto>>;

internal sealed class ListPublicEntityGroupsQueryHandler
    : QueryHandlerBase<ListPublicEntityGroupsQuery, PagedResult<PublicEntityGroupDto>>
{
    private readonly IPublicEntityRepository _entities;
    private readonly IRepository<Branch> _branches;

    public ListPublicEntityGroupsQueryHandler(
        IPublicEntityRepository entities,
        IRepository<Branch> branches,
        IEnumerable<IPipelineBehavior<ListPublicEntityGroupsQuery, PagedResult<PublicEntityGroupDto>>> behaviors)
        : base(behaviors)
    {
        _entities = entities;
        _branches = branches;
    }

    protected override async Task<PagedResult<PublicEntityGroupDto>> HandleCoreAsync(
        ListPublicEntityGroupsQuery query, CancellationToken ct)
    {
        var request = query.Query;
        var actor = query.Actor;
        var (page, perPage) = PageRequest.Normalize(request.Page, request.PerPage);
        var qNorm = ArabicNameNormalizer.Normalize(request.Q);
        var governorate = PublicEntityService.NormalizeOptional(request.Governorate);
        var excludeIds = request.ExcludeIds is null
            ? new HashSet<int>()
            : new HashSet<int>(request.ExcludeIds.Distinct());
        // معرّفات يجب ضمان ظهورها في النتيجة مهما كانت ترتيبها/صفحتها
        // (تُستخدم لنافذة توحيد التسمية لضمان تواجد «الهوية الهدف» السابقة الاختيار).
        var includeIds = request.IncludeIds is null
            ? new HashSet<int>()
            : new HashSet<int>(request.IncludeIds.Distinct());

        // نطاق الرئيس (قسم/شعبة): محافظة فرعه فقط — بلا فرع/محافظة لا يُعرض شيء
        string? headGovernorate = null;
        bool isHead = actor.Role is UserRole.Head or UserRole.SubHead;
        if (isHead)
        {
            if (!actor.BranchId.HasValue)
                return new PagedResult<PublicEntityGroupDto> { Page = page, PerPage = perPage, TotalCount = 0, Items = new List<PublicEntityGroupDto>() };
            var branch = await _branches.GetByIdAsync(actor.BranchId.Value, ct);
            headGovernorate = PublicEntityService.NormalizeOptional(branch?.Governorate);
            if (headGovernorate is null)
                return new PagedResult<PublicEntityGroupDto> { Page = page, PerPage = perPage, TotalCount = 0, Items = new List<PublicEntityGroupDto>() };
        }

        var groups = await _entities.ListGroupsWithEntriesAsync(ct);

        var filtered = groups
            .Where(g => g.IsActive)
            .Where(g => !excludeIds.Contains(g.Id))
            .Where(g => !isHead || g.Entries.Any(e => e.IsActive && e.Governorate == headGovernorate))
            .Where(g => governorate is null || g.Entries.Any(e => e.IsActive && e.Governorate == governorate))
            .Where(g => qNorm.Length == 0
                || ArabicNameNormalizer.Normalize(g.CanonicalName).Contains(qNorm)
                || g.Entries.Any(e => e.IsActive && e.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText).Contains(qNorm))))
            .OrderBy(g => g.CanonicalName, StringComparer.Ordinal)
            // فاصل حتمي لحدود الصفحات الإزاحية عند تساوي الاسم القانوني.
            .ThenBy(g => g.Id)
            .ToList();

        var totalCount = filtered.Count;
        var pageItems = filtered
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToList();

        // ضمان تواجد الهويات المطلوبة (IncludeIds) في النتيجة حتى لو كانت خارج نطاق الصفحة
        // الحالية بسبب الفرز/الترقيم — بشرط أن تكون مرّت من نفس الفلاتر (نشطة، نطاق، بحث).
        if (includeIds.Count > 0)
        {
            var included = filtered.Where(g => includeIds.Contains(g.Id)).ToList();
            if (included.Count > 0)
            {
                var presentIds = new HashSet<int>(pageItems.Select(g => g.Id));
                pageItems = pageItems
                    .Concat(included.Where(g => !presentIds.Contains(g.Id)))
                    .OrderBy(g => g.CanonicalName, StringComparer.Ordinal)
                    .ToList();
            }
        }

        var pageGroupIds = pageItems.Select(g => g.Id).ToList();
        var linkedCounts = await _entities.CountLinkedDocumentsByGroupIdsAsync(pageGroupIds, ct);

        var dtos = pageItems.Select(g =>
        {
            var scoped = g.Entries.Where(e => e.IsActive);
            if (isHead) scoped = scoped.Where(e => e.Governorate == headGovernorate);
            var scopedList = scoped.ToList();
            return new PublicEntityGroupDto(
                g.Id,
                g.CanonicalName,
                g.EntityType,
                g.IsActive,
                scopedList.Count,
                scopedList.Select(e => e.Governorate).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList(),
                linkedCounts.TryGetValue(g.Id, out var count) ? count : 0);
        }).ToList();

        return new PagedResult<PublicEntityGroupDto>
        {
            Page = page,
            PerPage = perPage,
            TotalCount = totalCount,
            Items = dtos,
        };
    }
}
