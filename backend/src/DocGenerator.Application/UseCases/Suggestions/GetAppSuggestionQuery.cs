using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Suggestions;

/// <summary>
/// اقتراح واحد بالمعرف: `SenderId` فارغ = عرض المشرف (أي اقتراح مع اسم المرسل)،
/// وله قيمة = عرض المالك (اقتراحه فقط بلا اسم). الغائب أو غير المملوك = `null`
/// دون كشف الوجود — بنفس عقد `List`.
/// </summary>
public sealed record GetAppSuggestionQuery(
    int Id,
    int? SenderId) : IQuery<AppSuggestionDto?>;

internal sealed class GetAppSuggestionQueryHandler
    : QueryHandlerBase<GetAppSuggestionQuery, AppSuggestionDto?>
{
    private readonly IAppSuggestionRepository _suggestions;

    public GetAppSuggestionQueryHandler(
        IAppSuggestionRepository suggestions,
        IEnumerable<IPipelineBehavior<GetAppSuggestionQuery, AppSuggestionDto?>> behaviors)
        : base(behaviors)
        => _suggestions = suggestions;

    protected override async Task<AppSuggestionDto?> HandleCoreAsync(
        GetAppSuggestionQuery query, CancellationToken ct)
    {
        var suggestion = await _suggestions.GetByIdWithSenderAsync(query.Id, ct);
        if (suggestion is null)
            return null;
        if (query.SenderId.HasValue)
        {
            if (suggestion.SenderId != query.SenderId.Value)
                return null;
            return AppSuggestionService.ToDto(suggestion, includeSender: false);
        }
        return AppSuggestionService.ToDto(suggestion, includeSender: true);
    }
}
