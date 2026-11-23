using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.ExecutionActions;

/// <summary>
/// إلغاء تذكير إجراء/ملاحظة — يملك معاملته وتدقيقه (`action`). الرسالة والترتيب
/// هما نفس مسار `PUT .../reminder` بالحقلين الفارغين (نقطة كتابة واحدة مشتركة).
/// </summary>
public sealed record ClearActionReminderCommand(
    int DocumentId,
    int ActionId,
    string? ActorName) : ICommand<bool>, ITransactionalRequest;

public sealed class ClearActionReminderCommandHandler
    : CommandHandlerBase<ClearActionReminderCommand, bool>
{
    private readonly IRepository<ExecutionAction> _actions;
    private readonly IDocumentRepository _documents;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public ClearActionReminderCommandHandler(
        IRepository<ExecutionAction> actions,
        IDocumentRepository documents,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<ClearActionReminderCommand, bool>> behaviors)
        : base(behaviors)
    {
        _actions = actions;
        _documents = documents;
        _uow = uow;
        _audit = audit;
    }

    protected override async Task<bool> HandleCoreAsync(
        ClearActionReminderCommand command, CancellationToken ct)
    {
        var action = await _actions.GetByIdAsync(command.ActionId, ct);
        if (action is null || action.DocumentId != command.DocumentId)
            return false;

        var doc = await _documents.GetByIdAsync(command.DocumentId, ct);
        if (doc is null)
            return false;

        await ExecutionActionService.ClearReminderWriteAsync(
            action, doc, command.ActorName, _actions, _uow, _audit, ct);
        return true;
    }
}
