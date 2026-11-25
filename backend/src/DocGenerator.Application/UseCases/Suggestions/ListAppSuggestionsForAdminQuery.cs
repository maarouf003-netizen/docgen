using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Suggestions;

/// <summary>صندوق المشرف/المدير مرقّمًا (الصفحة والحجم مقوّمان).</summary>
public sealed record ListAppSuggestionsForAdminQuery(
    int Page,
    int PerPage) : IQuery<PagedResult<AppSuggestionDto>>;

internal sealed class ListAppSuggestionsForAdminQueryHandler
    : QueryHandlerBase<ListAppSuggestionsForAdminQuery, PagedResult<AppSuggestionDto>>
{
    private readonly IAppSuggestionRepository _suggestions;

    public ListAppSuggestionsForAdminQueryHandler(
        IAppSuggestionRepository suggestions,
        IEnumerable<IPipelineBehavior<ListAppSuggestionsForAdminQuery, PagedResult<AppSuggestionDto>>> behaviors)
        : base(behaviors)
        => _suggestions = suggestions;

    protected override async Task<PagedResult<AppSuggestionDto>> HandleCoreAsync(
        ListAppSuggestionsForAdminQuery query, CancellationToken ct)
    {
        var page = query.Page;
        var perPage = query.PerPage;
        if (page < 1) page = 1;
        if (perPage < 1 || perPage > AppSuggestionService.MaxPerPage)
            perPage = AppSuggestionService.DefaultPerPage;
        var paged = await _suggestions.PagedForAdminAsync(page, perPage, ct);
        return new PagedResult<AppSuggestionDto>
        {
            Items = paged.Items.Select(s => AppSuggestionService.ToDto(s, includeSender: true)).ToList(),
            Page = paged.Page,
            PerPage = paged.PerPage,
            TotalCount = paged.TotalCount,
        };
    }
}
