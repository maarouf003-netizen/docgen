using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Suggestions;

/// <summary>
/// تعليم اقتراح تطوير مقروءًا — إخفاء أثر إداري يُوثَّق بالفاعل في المعاملة نفسها.
/// إعادة التعليم idempotent: `true` بلا كتابة إن كان مقروءًا سلفًا.
/// </summary>
public sealed record MarkAppSuggestionReadCommand(
    int SuggestionId,
    string? ActorName) : ICommand<bool>, ITransactionalRequest;

public sealed class MarkAppSuggestionReadCommandHandler
    : CommandHandlerBase<MarkAppSuggestionReadCommand, bool>
{
    private readonly IAppSuggestionRepository _suggestions;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public MarkAppSuggestionReadCommandHandler(
        IAppSuggestionRepository suggestions,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<MarkAppSuggestionReadCommand, bool>> behaviors)
        : base(behaviors)
    {
        _suggestions = suggestions;
        _uow = uow;
        _audit = audit;
    }

    protected override async Task<bool> HandleCoreAsync(
        MarkAppSuggestionReadCommand command, CancellationToken ct)
    {
        var suggestion = await _suggestions.GetByIdAsync(command.SuggestionId, ct);
        if (suggestion is null)
            return false;
        if (suggestion.IsRead)
            return true;

        suggestion.IsRead = true;
        _suggestions.Update(suggestion);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(command.ActorName, "app-suggestion.read",
            details: $"علّم اقتراح التطوير (رقم {suggestion.Id}) مقروءًا", ct: ct);
        return true;
    }
}
