using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.ExecutionCircuits;

/// <summary>
/// خطاف تنظيف تنبيه إعادة القيد عند صفر المعلقات (على نمط DeleteByDelegationAsync) —
/// معاملة واحدة وتصفية الكاش.
/// </summary>
public sealed record DeletePendingRegistrationAlertsCommand(
    int LawyerId) : ICommand<bool>;

internal sealed class DeletePendingRegistrationAlertsCommandHandler
    : CommandHandlerBase<DeletePendingRegistrationAlertsCommand, bool>
{
    private readonly IDocumentRepository _documents;
    private readonly IUserRepository _users;
    private readonly IHeadAlertRepository _alerts;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IReferenceDataCache? _cache;

    public DeletePendingRegistrationAlertsCommandHandler(
        IDocumentRepository documents,
        IUserRepository users,
        IHeadAlertRepository alerts,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IEnumerable<IPipelineBehavior<DeletePendingRegistrationAlertsCommand, bool>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _documents = documents;
        _users = users;
        _alerts = alerts;
        _uow = uow;
        _tx = tx;
        _cache = cache;
    }

    protected override async Task<bool> HandleCoreAsync(
        DeletePendingRegistrationAlertsCommand command, CancellationToken ct)
    {
        var removed = false;
        await _tx.RunAsync(async token =>
        {
            removed = await ExecutionCircuitRules.CleanupPendingAlertsAsync(
                command.LawyerId, _documents, _users, _alerts, token);
            if (removed)
                await _uow.SaveChangesAsync(token);
            _cache?.Invalidate(ReferenceDataGroups.Circuits);
        }, ct);
        return removed;
    }
}
