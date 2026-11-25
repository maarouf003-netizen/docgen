using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Suggestions;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>حزمة معالجات قراءات اقتراحات التطبيق (`P4-01cu`) على قاعدة اختبارية واحدة.</summary>
public sealed record AppSuggestionReadHandlers(
    IQueryHandler<ListMyAppSuggestionsQuery, List<AppSuggestionDto>> Mine,
    IQueryHandler<GetAppSuggestionQuery, AppSuggestionDto?> Single,
    IQueryHandler<ListAppSuggestionsForAdminQuery, PagedResult<AppSuggestionDto>> Admin,
    IQueryHandler<CountUnreadAppSuggestionsQuery, int> Unread);

public static class AppSuggestionReadHandlerFactory
{
    public static AppSuggestionReadHandlers Create(DocGeneratorDbContext db)
        => new(
            new ListMyAppSuggestionsQueryHandler(
                new AppSuggestionRepository(db),
                Array.Empty<IPipelineBehavior<ListMyAppSuggestionsQuery, List<AppSuggestionDto>>>()),
            new GetAppSuggestionQueryHandler(
                new AppSuggestionRepository(db),
                Array.Empty<IPipelineBehavior<GetAppSuggestionQuery, AppSuggestionDto?>>()),
            new ListAppSuggestionsForAdminQueryHandler(
                new AppSuggestionRepository(db),
                Array.Empty<IPipelineBehavior<ListAppSuggestionsForAdminQuery, PagedResult<AppSuggestionDto>>>()),
            new CountUnreadAppSuggestionsQueryHandler(
                new AppSuggestionRepository(db),
                Array.Empty<IPipelineBehavior<CountUnreadAppSuggestionsQuery, int>>()));
}
