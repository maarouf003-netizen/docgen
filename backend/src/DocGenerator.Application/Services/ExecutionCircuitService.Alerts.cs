using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;

namespace DocGenerator.Application.Services;

public sealed partial class ExecutionCircuitService : IExecutionCircuitService
{
    public async Task SyncPendingAlertsAfterTransferAsync(int sourceOwnerId, int targetOwnerId, int branchId, int actorUserId, CancellationToken ct = default)
    {
        // التنبيه يتبع المالك — تُنشأ تنبيهات المالك الجديد لدوائر معلقاته الحالية،
        // وتُصفَّى تنبيهات القديم التي لم يعد لها معلقات. تُنفَّذ داخل معاملة النقل
        // إن وُجدت (ذرّية مع تغيير الملكية)، وإلا تُفتح معاملة مستقلة.
        if (_tx is ITransactionContext { HasActiveTransaction: true })
        {
            await SyncPendingAlertsAfterTransferCoreAsync(
                sourceOwnerId, targetOwnerId, branchId, actorUserId, ct);
            return;
        }

        await _tx.RunAsync(token => SyncPendingAlertsAfterTransferCoreAsync(
            sourceOwnerId, targetOwnerId, branchId, actorUserId, token), ct);
    }

    private async Task SyncPendingAlertsAfterTransferCoreAsync(
        int sourceOwnerId, int targetOwnerId, int branchId, int actorUserId, CancellationToken token)
    {
        var targetPending = await _documents.ListPendingForLawyerAsync(targetOwnerId, null, token);
        foreach (var g in targetPending
                     .Where(d => d.ExecutionCircuitId.HasValue)
                     .GroupBy(d => d.ExecutionCircuitId!.Value))
        {
            var circuitName = g.Select(d => d.ExecutionCircuit?.Name)
                .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                ?? g.Select(d => d.Court)
                    .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
            if (string.IsNullOrWhiteSpace(circuitName))
                continue;
            await ExecutionCircuitRules.EnqueuePendingAlertAsync(
                _alertOutbox,
                targetOwnerId,
                branchId,
                actorUserId,
                g.Key,
                circuitName!,
                DateTime.UtcNow,
                token);
        }
        await ExecutionCircuitRules.CleanupPendingAlertsAsync(
            sourceOwnerId, _documents, _users, _alerts, token);
        await _uow.SaveChangesAsync(token);
        _cache?.Invalidate(ReferenceDataGroups.Circuits);
    }
}
