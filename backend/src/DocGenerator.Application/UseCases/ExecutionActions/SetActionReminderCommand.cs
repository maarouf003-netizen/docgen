using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.ExecutionActions;

/// <summary>
/// إنشاء/تعديل تذكير إجراء (مدة/لون) عبر مسار مستقل عن تعديل الإجراء — يملك معاملته
/// وتدقيقه (`action`). الحقلان الفارغان = إلغاء عبر نقطة الكتابة المشتركة.
/// </summary>
public sealed record SetActionReminderCommand(
    int DocumentId,
    int ActionId,
    SetActionReminderRequest Request,
    string? ActorName) : ICommand<ExecutionActionDto>, ITransactionalRequest;

public sealed class SetActionReminderCommandHandler
    : CommandHandlerBase<SetActionReminderCommand, ExecutionActionDto>
{
    private readonly IRepository<ExecutionAction> _actions;
    private readonly IDocumentRepository _documents;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public SetActionReminderCommandHandler(
        IRepository<ExecutionAction> actions,
        IDocumentRepository documents,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<SetActionReminderCommand, ExecutionActionDto>> behaviors)
        : base(behaviors)
    {
        _actions = actions;
        _documents = documents;
        _uow = uow;
        _audit = audit;
    }

    protected override async Task<ExecutionActionDto> HandleCoreAsync(
        SetActionReminderCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var action = await _actions.GetByIdAsync(command.ActionId, ct);
        if (action is null || action.DocumentId != command.DocumentId)
            throw new KeyNotFoundException();

        OptimisticConcurrencyGuard.EnsureCurrent(action, request.Version, "الإجراء");

        var doc = await _documents.GetByIdAsync(command.DocumentId, ct);
        if (doc is null)
            throw new KeyNotFoundException();

        var (reminderDuration, reminderColor) = ExecutionActionService.NormalizeReminder(
            request.ReminderDuration, request.ReminderColor);

        if (reminderDuration is null && reminderColor is null)
        {
            await ExecutionActionService.ClearReminderWriteAsync(
                action, doc, command.ActorName, _actions, _uow, _audit, ct);
            return ExecutionActionService.ToDto(action, command.ActorName);
        }

        action.ReminderDuration = reminderDuration;
        action.ReminderColor = reminderColor;
        action.ReminderBaseDate ??= ActionDateParser.TryParse(action.ActionDate);

        _actions.Update(action);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(command.ActorName, "action", command.DocumentId, doc.DocumentType,
            DocumentText.AuditWithActor(
                $"ضبط تذكير {DocumentText.TypeLabel(action.Type)}: {HtmlInputSanitizer.ToPlainText(action.Text)} ({reminderDuration ?? "—"} — {reminderColor ?? "—"})",
                doc), ct);

        return ExecutionActionService.ToDto(action, command.ActorName);
    }
}
