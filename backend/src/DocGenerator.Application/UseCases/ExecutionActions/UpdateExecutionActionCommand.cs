using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.ExecutionActions;

/// <summary>
/// تعديل إجراء/ملاحظة تنفيذ — يملك معاملته وتدقيقه (`action`)، ولا يمسّ أي حقل تذكير
/// (حالة التذكير تُدار من `SetReminderAsync` و«نقل التذكير» حصرًا).
/// </summary>
public sealed record UpdateExecutionActionCommand(
    int DocumentId,
    int ActionId,
    UpdateExecutionActionRequest Request,
    string? ActorName) : ICommand<ExecutionActionDto?>, ITransactionalRequest;

public sealed class UpdateExecutionActionCommandHandler
    : CommandHandlerBase<UpdateExecutionActionCommand, ExecutionActionDto?>
{
    private readonly IRepository<ExecutionAction> _actions;
    private readonly IDocumentRepository _documents;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public UpdateExecutionActionCommandHandler(
        IRepository<ExecutionAction> actions,
        IDocumentRepository documents,
        IUnitOfWork uow,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<UpdateExecutionActionCommand, ExecutionActionDto?>> behaviors)
        : base(behaviors)
    {
        _actions = actions;
        _documents = documents;
        _uow = uow;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<ExecutionActionDto?> HandleCoreAsync(
        UpdateExecutionActionCommand command, CancellationToken ct)
    {
        var request = command.Request;
        // تعديل الإجراء (نوع/نص/تاريخ) لا يكتب أي حقل تذكير: حالة التذكير تُدار من
        // `SetReminderAsync` و«نقل التذكير» حصرًا، فلا يتغيّر `ReminderBaseDate` هنا أبدًا.
        var (type, text, actionDate) = ExecutionActionService.NormalizeAction(
            request.Type, request.Text, request.ActionDate, _clock, _timeZone);

        var action = await _actions.GetByIdAsync(command.ActionId, ct);
        if (action is null || action.DocumentId != command.DocumentId)
            throw new KeyNotFoundException();

        OptimisticConcurrencyGuard.EnsureCurrent(action, request.Version, "الإجراء");

        var doc = await _documents.GetByIdAsync(command.DocumentId, ct);
        if (doc is null)
            throw new KeyNotFoundException();

        action.Type = type;
        action.Text = text;
        action.ActionDate = actionDate;
        // التاريخ المحلول يتبع نص الإجراء المُدخل (لا يمسّه أي مسار تذكير).
        action.ActionDateParsed = ActionDateParser.TryParse(actionDate);

        _actions.Update(action);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(command.ActorName, "action", command.DocumentId, doc.DocumentType,
            DocumentText.AuditWithActor($"عدّل {DocumentText.TypeLabel(type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), ct);
        return ExecutionActionService.ToDto(action, command.ActorName);
    }
}
