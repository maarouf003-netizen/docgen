using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Suggestions;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري لمعالجات قراءات اقتراحات التطبيق (`P4-01cu`): يحافظ على توقيعات
/// خدمة الاقتراحات القديمة فوق سجل المعالجات — منطق الاختبارات كما هو.
/// </summary>
public static class AppSuggestionReadQueryExtensions
{
    public static Task<List<AppSuggestionDto>> ListMineAsync(
        this AppSuggestionReadHandlers handlers,
        int senderId,
        CancellationToken ct = default)
        => handlers.Mine.HandleAsync(new ListMyAppSuggestionsQuery(senderId), ct);

    public static Task<AppSuggestionDto?> GetByIdAsync(
        this AppSuggestionReadHandlers handlers,
        int id,
        int? senderId,
        CancellationToken ct = default)
        => handlers.Single.HandleAsync(new GetAppSuggestionQuery(id, senderId), ct);

    public static Task<PagedResult<AppSuggestionDto>> ListForAdminAsync(
        this AppSuggestionReadHandlers handlers,
        int page,
        int perPage,
        CancellationToken ct = default)
        => handlers.Admin.HandleAsync(new ListAppSuggestionsForAdminQuery(page, perPage), ct);

    public static Task<int> CountUnreadAsync(
        this AppSuggestionReadHandlers handlers,
        CancellationToken ct = default)
        => handlers.Unread.HandleAsync(new CountUnreadAppSuggestionsQuery(), ct);
}
