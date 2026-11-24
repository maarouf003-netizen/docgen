using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>سحب اقتراح تعديل الجهة الأم — لمنشئه رئيس القسم/الشعبة فقط.</summary>
public sealed record WithdrawParentEditSuggestionCommand(
    int SuggestionId,
    EntityRegistryActor Actor) : ICommand<ParentEditSuggestionDto?>;

internal sealed class WithdrawParentEditSuggestionCommandHandler
    : CommandHandlerBase<WithdrawParentEditSuggestionCommand, ParentEditSuggestionDto?>
{
    private readonly IPublicEntityWriteRules _rules;
    private readonly IRepository<ParentEditSuggestion> _suggestions;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public WithdrawParentEditSuggestionCommandHandler(
        IPublicEntityWriteRules rules,
        IRepository<ParentEditSuggestion> suggestions,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<WithdrawParentEditSuggestionCommand, ParentEditSuggestionDto?>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _suggestions = suggestions;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<ParentEditSuggestionDto?> HandleCoreAsync(
        WithdrawParentEditSuggestionCommand command, CancellationToken ct)
    {
        var actor = command.Actor;
        if (actor.Role is not (UserRole.Head or UserRole.SubHead))
            throw new UnauthorizedAccessException("سحب اقتراح الجهة الأم متاح لمنشئه رئيس القسم أو الشعبة فقط");

        var suggestion = await _suggestions.GetByIdAsync(command.SuggestionId, ct);
        if (suggestion is null)
            return null;
        if (suggestion.CreatedById != actor.UserId)
            throw new UnauthorizedAccessException("لا يمكنك سحب اقتراح منشأ من رئيس آخر");
        if (suggestion.Status != ParentEditSuggestionStatusCatalog.Pending)
            throw new ArgumentException("الاقتراح لم يعد معلّقًا فلا يُسحب");

        suggestion.Status = ParentEditSuggestionStatusCatalog.Withdrawn;

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actor.Name, "withdraw_parent_edit_suggestion",
                details: $"سحب اقتراحه لتعديل الجهة الأم «{suggestion.Group?.CanonicalName ?? suggestion.Entry?.Group?.CanonicalName}»",
                ct: token);
        }, ct);

        return _rules.ToParentEditSuggestionDto(suggestion);
    }
}
