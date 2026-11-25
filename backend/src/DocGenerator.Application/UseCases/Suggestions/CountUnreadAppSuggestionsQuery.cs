using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;

namespace DocGenerator.Application.UseCases.Suggestions;

/// <summary>عدد الاقتراحات غير المقروءة في صندوق المشرف/المدير.</summary>
public sealed record CountUnreadAppSuggestionsQuery : IQuery<int>;

internal sealed class CountUnreadAppSuggestionsQueryHandler
    : QueryHandlerBase<CountUnreadAppSuggestionsQuery, int>
{
    private readonly IAppSuggestionRepository _suggestions;

    public CountUnreadAppSuggestionsQueryHandler(
        IAppSuggestionRepository suggestions,
        IEnumerable<IPipelineBehavior<CountUnreadAppSuggestionsQuery, int>> behaviors)
        : base(behaviors)
        => _suggestions = suggestions;

    protected override Task<int> HandleCoreAsync(
        CountUnreadAppSuggestionsQuery query, CancellationToken ct)
        => _suggestions.CountUnreadAsync(ct);
}
