using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Suggestions;

/// <summary>سجل «اقتراحاتي» — اقتراحات المرسل نفسه فقط (بلا اسم المرسل).</summary>
public sealed record ListMyAppSuggestionsQuery(
    int SenderId) : IQuery<List<AppSuggestionDto>>;

internal sealed class ListMyAppSuggestionsQueryHandler
    : QueryHandlerBase<ListMyAppSuggestionsQuery, List<AppSuggestionDto>>
{
    private readonly IAppSuggestionRepository _suggestions;

    public ListMyAppSuggestionsQueryHandler(
        IAppSuggestionRepository suggestions,
        IEnumerable<IPipelineBehavior<ListMyAppSuggestionsQuery, List<AppSuggestionDto>>> behaviors)
        : base(behaviors)
        => _suggestions = suggestions;

    protected override async Task<List<AppSuggestionDto>> HandleCoreAsync(
        ListMyAppSuggestionsQuery query, CancellationToken ct)
    {
        var items = await _suggestions.ListBySenderAsync(query.SenderId, ct);
        return items.Select(s => AppSuggestionService.ToDto(s, includeSender: false)).ToList();
    }
}
