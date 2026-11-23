using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.ExecutionActions;

/// <summary>
/// إضافة إجراء/ملاحظة تنفيذ — يملك معاملته وتدقيقه (`action`)، ويحسب تاريخ قاعدة
/// التذكير مرة واحدة عند الإنشاء كما الأصل.
/// </summary>
public sealed record AddExecutionActionCommand(
    int DocumentId,
    AddExecutionActionRequest Request,
    int UserId,
    string? ActorName) : ICommand<ExecutionActionDto>, ITransactionalRequest;

public sealed class AddExecutionActionCommandHandler
    : CommandHandlerBase<AddExecutionActionCommand, ExecutionActionDto>
{
    private readonly IRepository<ExecutionAction> _actions;
    private readonly IDocumentRepository _documents;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public AddExecutionActionCommandHandler(
        IRepository<ExecutionAction> actions,
        IDocumentRepository documents,
        IUnitOfWork uow,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<AddExecutionActionCommand, ExecutionActionDto>> behaviors)
        : base(behaviors)
    {
        _actions = actions;
        _documents = documents;
        _uow = uow;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<ExecutionActionDto> HandleCoreAsync(
        AddExecutionActionCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var doc = await _documents.GetByIdAsync(command.DocumentId, ct);
        if (doc is null)
            throw new KeyNotFoundException();

        var (type, text, actionDate) = ExecutionActionService.NormalizeAction(
            request.Type, request.Text, request.ActionDate, _clock, _timeZone);
        var (reminderDuration, reminderColor) = ExecutionActionService.NormalizeReminder(
            request.ReminderDuration, request.ReminderColor);

        var action = new ExecutionAction
        {
            DocumentId = command.DocumentId,
            Type = type,
            Text = text,
            ActionDate = actionDate,
            // تاريخ الإجراء محلولًا للاستعلامات النطاقية (تذكيرات الشطب) — يُكتب هنا فقط.
            ActionDateParsed = ActionDateParser.TryParse(actionDate),
            ReminderDuration = reminderDuration,
            ReminderColor = reminderColor,
            // تاريخ أساس التذكير يُثبَّت مرة واحدة عند الإنشاء من تاريخ الإجراء المحلول —
            // ولا يُشتق لاحقًا من نص الإجراء ولا يُكتب فيه أي مسار تذكير.
            ReminderBaseDate = ActionReminderCalculator.BaseDateForNewReminder(reminderDuration, reminderColor, actionDate),
            CreatedById = command.UserId,
            CreatedAt = DateTime.UtcNow,
        };

        await _actions.AddAsync(action, ct);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(command.ActorName, "action", doc.Id, doc.DocumentType,
            DocumentText.AuditWithActor($"أضاف {DocumentText.TypeLabel(type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), ct);
        return ExecutionActionService.ToDto(action, command.ActorName);
    }
}
