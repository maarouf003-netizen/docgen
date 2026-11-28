using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>قائمة اقتراحات تعديل الجهة الأم — قراءة صرفة (نطاق الرئيس: اقتراحاته فقط).</summary>
public sealed record ListParentEditSuggestionsQuery(
    ParentEditSuggestionListQuery Query,
    EntityRegistryActor Actor) : IQuery<PagedResult<ParentEditSuggestionDto>>;

internal sealed class ListParentEditSuggestionsQueryHandler
    : QueryHandlerBase<ListParentEditSuggestionsQuery, PagedResult<ParentEditSuggestionDto>>
{
    private readonly IRepository<ParentEditSuggestion> _suggestions;

    public ListParentEditSuggestionsQueryHandler(
        IRepository<ParentEditSuggestion> suggestions,
        IEnumerable<IPipelineBehavior<ListParentEditSuggestionsQuery, PagedResult<ParentEditSuggestionDto>>> behaviors)
        : base(behaviors)
        => _suggestions = suggestions;

    protected override async Task<PagedResult<ParentEditSuggestionDto>> HandleCoreAsync(
        ListParentEditSuggestionsQuery query, CancellationToken ct)
    {
        var request = query.Query;
        var actor = query.Actor;
        var all = await _suggestions.ListAsync(ct);
        var status = PublicEntityService.NormalizeOptional(request.Status);
        IEnumerable<ParentEditSuggestion> filtered = all
            .Where(s => status is null || s.Status == status)
            .Where(s => request.GroupId is null || s.GroupId == request.GroupId);

        // نطاق الرئيس: اقتراحاته هو فقط (لحالة المعلّق في نافذة فروع جهة محافظته).
        if (actor.Role is UserRole.Head or UserRole.SubHead)
            filtered = filtered.Where(s => s.CreatedById == actor.UserId);

        var ordered = filtered
            .OrderByDescending(s => s.CreatedAtUtc)
            // فاصل حتمي لحدود الصفحات الإزاحية عند تساوي زمن الإنشاء.
            .ThenByDescending(s => s.Id)
            .ToList();
        var (page, perPage) = PageRequest.Normalize(request.Page, request.PerPage);
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * perPage).Take(perPage).Select(PublicEntityService.ToParentEditSuggestionDto).ToList();
        return new PagedResult<ParentEditSuggestionDto> { Items = items, Page = page, PerPage = perPage, TotalCount = total };
    }
}
