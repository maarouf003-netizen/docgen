using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Common.Security;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.ExecutionActions;

/// <summary>
/// حذف إجراء/ملاحظة تنفيذ من ملف — يملك معاملته وتدقيقه (`action`).
/// الغياب أو عدم تطابق الملف يعيد `false` بلا أثر جانبي (كالأصل).
/// </summary>
public sealed record DeleteExecutionActionCommand(
    int DocumentId,
    int ActionId,
    string? ActorName) : ICommand<bool>, ITransactionalRequest;

public sealed class DeleteExecutionActionCommandHandler
    : CommandHandlerBase<DeleteExecutionActionCommand, bool>
{
    private readonly IRepository<ExecutionAction> _actions;
    private readonly IDocumentRepository _documents;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public DeleteExecutionActionCommandHandler(
        IRepository<ExecutionAction> actions,
        IDocumentRepository documents,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<DeleteExecutionActionCommand, bool>> behaviors)
        : base(behaviors)
    {
        _actions = actions;
        _documents = documents;
        _uow = uow;
        _audit = audit;
    }

    protected override async Task<bool> HandleCoreAsync(
        DeleteExecutionActionCommand command, CancellationToken ct)
    {
        var action = await _actions.GetByIdAsync(command.ActionId, ct);
        if (action is null || action.DocumentId != command.DocumentId)
            return false;

        var doc = await _documents.GetByIdAsync(command.DocumentId, ct);
        if (doc is null)
            return false;

        _actions.Remove(action);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(command.ActorName, "action", command.DocumentId, doc.DocumentType,
            DocumentText.AuditWithActor($"حذف {DocumentText.TypeLabel(action.Type)}: {HtmlInputSanitizer.ToPlainText(action.Text)}", doc), ct);
        return true;
    }
}
