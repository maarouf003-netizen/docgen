using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>سجل تغييرات الجهات — مصدره PublicEntityChangeEvent فقط (د5 §7)؛ قراءة صرفة.</summary>
public sealed record ListPublicEntityChangeEventsQuery(
    EntityChangeEventQuery Query,
    EntityRegistryActor Actor) : IQuery<PagedResult<EntityChangeEventDto>>;

internal sealed class ListPublicEntityChangeEventsQueryHandler
    : QueryHandlerBase<ListPublicEntityChangeEventsQuery, PagedResult<EntityChangeEventDto>>
{
    private readonly IPublicEntityRepository _entities;
    private readonly IRepository<Branch> _branches;

    public ListPublicEntityChangeEventsQueryHandler(
        IPublicEntityRepository entities,
        IRepository<Branch> branches,
        IEnumerable<IPipelineBehavior<ListPublicEntityChangeEventsQuery, PagedResult<EntityChangeEventDto>>> behaviors)
        : base(behaviors)
    {
        _entities = entities;
        _branches = branches;
    }

    protected override async Task<PagedResult<EntityChangeEventDto>> HandleCoreAsync(
        ListPublicEntityChangeEventsQuery query, CancellationToken ct)
    {
        var request = query.Query;
        var actor = query.Actor;
        var filtered = await PublicEntityService.GetFilteredChangeEventsAsync(_entities, _branches, request, actor, ct);
        var (page, perPage) = PageRequest.Normalize(request.Page, request.PerPage);
        var total = filtered.Count;
        var items = filtered.Skip((page - 1) * perPage).Take(perPage).Select(PublicEntityService.ToChangeEventDto).ToList();
        return new PagedResult<EntityChangeEventDto> { Items = items, Page = page, PerPage = perPage, TotalCount = total };
    }
}
