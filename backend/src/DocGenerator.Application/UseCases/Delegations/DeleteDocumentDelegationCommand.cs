using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Delegations;

/// <summary>
/// حذف إنابة معلّقة — يملك حرّاس الملكية/الحالة، وأرشفة تنبيهاتها، ومعاملته، وتدقيق
/// `delete_delegation`.
/// <para>
/// **حدود المعاملة:** الحرّاس قبل المعاملة كما الأصل، ثم معاملة واحدة: أرشفة تنبيهات
/// الإنابة ← إزالة الصف ← حفظ ← تدقيق. بلا وسم `ITransactionalRequest`.
/// </para>
/// </summary>
public sealed record DeleteDocumentDelegationCommand(
    int DelegationId,
    int UserId,
    string? ActorName) : ICommand<bool>;

public sealed class DeleteDocumentDelegationCommandHandler
    : CommandHandlerBase<DeleteDocumentDelegationCommand, bool>
{
    private readonly IDelegationRepository _delegations;
    private readonly IHeadAlertService _alerts;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public DeleteDocumentDelegationCommandHandler(
        IDelegationRepository delegations,
        IHeadAlertService alerts,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<DeleteDocumentDelegationCommand, bool>> behaviors)
        : base(behaviors)
    {
        _delegations = delegations;
        _alerts = alerts;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<bool> HandleCoreAsync(
        DeleteDocumentDelegationCommand command, CancellationToken ct)
    {
        var delegation = await _delegations.GetByIdWithDetailsAsync(command.DelegationId, ct)
            ?? throw new KeyNotFoundException("الإنابة غير موجودة");

        DocumentDelegationRules.EnsurePendingByOwner(delegation, command.UserId);
        var source = delegation.SourceDocument;

        await _tx.RunAsync(async token =>
        {
            await _alerts.ArchiveByDelegationAsync(
                delegation.Id,
                HeadAlertKindCatalog.DelegationKinds,
                "delegation_deleted",
                token);
            _delegations.Remove(delegation);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "delete_delegation",
                source.Id, source.DocumentType,
                $"حذف إنابة (رقم {delegation.Id}) إلى {delegation.DelegatedCourt}", token);
        }, ct);

        return true;
    }
}
