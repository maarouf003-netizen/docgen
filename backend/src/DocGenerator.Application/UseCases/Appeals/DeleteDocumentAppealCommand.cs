using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Appeals;

/// <summary>
/// حذف استئناف قبل الإسناد — يملك حرّاس الصلاحية/الحالة، وأرشفة تنبيهاته، ومعاملته،
/// وتدقيق `delete_appeal`.
/// <para>
/// **حدود المعاملة:** الحرّاس قبل المعاملة كما الأصل، ثم معاملة واحدة: أرشفة التنبيهات
/// ← إزالة الصف ← حفظ ← تدقيق. بلا وسم `ITransactionalRequest`.
/// </para>
/// </summary>
public sealed record DeleteDocumentAppealCommand(
    int AppealId,
    int UserId,
    string? ActorName) : ICommand<bool>;

public sealed class DeleteDocumentAppealCommandHandler
    : CommandHandlerBase<DeleteDocumentAppealCommand, bool>
{
    private readonly IAppealRepository _appeals;
    private readonly IHeadAlertService _alerts;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public DeleteDocumentAppealCommandHandler(
        IAppealRepository appeals,
        IHeadAlertService alerts,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<DeleteDocumentAppealCommand, bool>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _alerts = alerts;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<bool> HandleCoreAsync(
        DeleteDocumentAppealCommand command, CancellationToken ct)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(command.AppealId, ct)
            ?? throw new KeyNotFoundException("الاستئناف غير موجود");

        DocumentAppealRules.EnsurePendingByCreator(appeal, command.UserId);
        var source = appeal.Document;

        await _tx.RunAsync(async token =>
        {
            await _alerts.ArchiveByAppealAsync(
                appeal.Id,
                HeadAlertKindCatalog.AppealKinds,
                "appeal_deleted",
                token);
            _appeals.Remove(appeal);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "delete_appeal",
                source.Id, source.DocumentType,
                $"حذف الاستئناف (رقم {appeal.Id}) من الملف (رقم {source.Id})", token);
        }, ct);

        return true;
    }
}
