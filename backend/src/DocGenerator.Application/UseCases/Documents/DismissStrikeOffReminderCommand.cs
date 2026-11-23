using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>
/// إلغاء تذكير الشطب لملف يملكه المحامي — أول use-case يملك معاملته بنفسه
/// (عبر `ITransactionalRequest`)، فينتقل عبء المعاملة من الخدمة إلى المعالج.
/// </summary>
public sealed record DismissStrikeOffReminderCommand(
    int DocumentId,
    int OwnerId,
    string? ActorName) : ICommand<bool>, ITransactionalRequest;

public sealed class DismissStrikeOffReminderCommandHandler
    : CommandHandlerBase<DismissStrikeOffReminderCommand, bool>
{
    private readonly IDocumentRepository _documents;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public DismissStrikeOffReminderCommandHandler(
        IDocumentRepository documents,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<DismissStrikeOffReminderCommand, bool>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _uow = uow;
        _audit = audit;
    }

    protected override async Task<bool> HandleCoreAsync(
        DismissStrikeOffReminderCommand command, CancellationToken ct)
    {
        var doc = await _documents.GetByIdAsync(command.DocumentId, ct);
        // المالك فقط (النطاق مفروض في المتحكم بالدور، والتحقق هنا دفاعًا مزدوجًا).
        if (doc is null || doc.CreatedById != command.OwnerId)
            return false;

        doc.StrikeOffReminderDismissedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(command.ActorName, "strike_off_reminder_dismiss", doc.Id, doc.DocumentType,
            $"ألغى تذكير الشطب للملف (رقم {doc.Id})", ct);
        return true;
    }
}
