using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.Suggestions;

/// <summary>تسطير اقتراح تطوير — يملك معاملته وتدقيقه.</summary>
public sealed record CreateAppSuggestionCommand(
    string Message,
    int SenderId,
    string? ActorName) : ICommand<AppSuggestionDto>, ITransactionalRequest;

public sealed class CreateAppSuggestionCommandHandler
    : CommandHandlerBase<CreateAppSuggestionCommand, AppSuggestionDto>
{
    private readonly IAppSuggestionRepository _suggestions;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public CreateAppSuggestionCommandHandler(
        IAppSuggestionRepository suggestions,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<CreateAppSuggestionCommand, AppSuggestionDto>> behaviors)
        : base(behaviors)
    {
        _suggestions = suggestions;
        _uow = uow;
        _audit = audit;
    }

    protected override async Task<AppSuggestionDto> HandleCoreAsync(
        CreateAppSuggestionCommand command, CancellationToken ct)
    {
        var trimmed = command.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("نص الاقتراح مطلوب");
        if (trimmed.Length > AppSuggestionService.MessageMaxLength)
            throw new ArgumentException(
                $"نص الاقتراح أطول من الحد المسموح ({AppSuggestionService.MessageMaxLength} حرف)");

        var suggestion = new AppSuggestion
        {
            SenderId = command.SenderId,
            Message = trimmed,
            CreatedAt = DateTime.UtcNow,
        };

        await _suggestions.AddAsync(suggestion, ct);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(command.ActorName, "app-suggestion.create",
            details: $"أُضيف اقتراح تطوير: {trimmed[..Math.Min(trimmed.Length, 80)]}", ct: ct);

        return new AppSuggestionDto(
            suggestion.Id,
            suggestion.Message,
            suggestion.CreatedAt,
            suggestion.IsRead,
            null);
    }
}
