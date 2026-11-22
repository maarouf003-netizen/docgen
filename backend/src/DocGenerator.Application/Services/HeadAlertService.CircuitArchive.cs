using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class HeadAlertService : IHeadAlertService
{
    public async Task<int> ApplyCircuitRenamedAsync(
        int circuitId, string message, DateTime occurredAt, CancellationToken ct = default)
    {
        if (!_transactions.HasActiveTransaction)
            throw new InvalidOperationException("معالجة إعادة التسمية تتطلب معاملة نشطة");

        var text = HeadAlertTextLimits.EnsureWithinSafety(message);
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("نص إعادة التسمية مطلوب", nameof(message));

        var pending = await _alerts.ListActivePendingByCircuitAndKindAsync(
            circuitId, HeadAlertKindCatalog.CircuitPendingDocuments, ct);

        var active = 0;
        foreach (var alert in pending)
        {
            // الرسالة الأقدم من آخر تغيير محتوى لا تمحو الأحدث؛ لكن الصف نشط
            // فتُعدّ الرسالة معالَجة، لا «لا تنبيهات نشطة» (الخطة §6.4).
            active++;
            var lastChanged = alert.LastContentChangedAt;
            if (occurredAt < lastChanged)
                continue;

            if (!string.Equals(alert.Message, text, StringComparison.Ordinal))
            {
                alert.Message = text;
                alert.LastContentChangedAt = lastChanged > occurredAt ? lastChanged : occurredAt;
                foreach (var recipient in alert.Recipients)
                {
                    recipient.IsRead = false;
                    recipient.ReadAt = null;
                }
            }
        }

        await _uow.SaveChangesAsync(ct);
        return active;
    }

    public Task<int> ArchiveByDelegationAsync(
        int delegationId, IReadOnlySet<string> archiveKinds, string reason, CancellationToken ct = default)
    {
        ValidateArchiveArguments(archiveKinds, reason);

        // معاملة محيطة: إن وُجدت معاملة نشطة فلا تُفتح معاملة متداخلة.
        if (_transactions.HasActiveTransaction)
            return ArchiveByDelegationCoreAsync(delegationId, archiveKinds, null, reason, ct);

        return _tx.RunAsync(
            token => ArchiveByDelegationCoreAsync(delegationId, archiveKinds, null, reason, token),
            ct);
    }

    public Task<int> ArchiveByAppealAsync(
        int appealId, IReadOnlySet<string> archiveKinds, string reason, CancellationToken ct = default)
    {
        ValidateArchiveArguments(archiveKinds, reason);

        if (_transactions.HasActiveTransaction)
            return ArchiveByAppealCoreAsync(appealId, archiveKinds, null, reason, ct);

        return _tx.RunAsync(
            token => ArchiveByAppealCoreAsync(appealId, archiveKinds, null, reason, token),
            ct);
    }

    /// <summary>
    /// الأرشفة الأساسية — بلا <c>ITransactionRunner</c> (تعمل داخل معاملة المتصل).
    /// </summary>
    internal async Task<int> ArchiveByDelegationCoreAsync(
        int delegationId,
        IReadOnlySet<string> archiveKinds,
        int? excludeAlertId,
        string reason,
        CancellationToken ct)
    {
        var archived = await _alerts.ArchiveByScopeAndKindsAsync(
            delegationId, HeadAlertScopeKind.Delegation, archiveKinds, excludeAlertId, ct);

        if (archived > 0)
            await _audit.LogAsync(null, "archive_alert",
                details: $"أُرشفت {archived} تنبيه إنابة ({reason})", ct: ct);

        return archived;
    }

    /// <summary>الأرشفة الأساسية للاستئناف — بلا <c>ITransactionRunner</c>.</summary>
    internal async Task<int> ArchiveByAppealCoreAsync(
        int appealId,
        IReadOnlySet<string> archiveKinds,
        int? excludeAlertId,
        string reason,
        CancellationToken ct)
    {
        var archived = await _alerts.ArchiveByScopeAndKindsAsync(
            appealId, HeadAlertScopeKind.Appeal, archiveKinds, excludeAlertId, ct);

        if (archived > 0)
            await _audit.LogAsync(null, "archive_alert",
                details: $"أُرشفت {archived} تنبيه استئناف ({reason})", ct: ct);

        return archived;
    }

    private static void ValidateArchiveArguments(IReadOnlySet<string> archiveKinds, string reason)
    {
        if (archiveKinds is null || archiveKinds.Count == 0)
            throw new ArgumentException("مجموعة أنواع الأرشفة إلزامية", nameof(archiveKinds));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("سبب الأرشفة إلزامي", nameof(reason));
    }

    private async Task ApplySupersedePlanAsync(
        HeadAlert current,
        HeadAlertSupersedePlan plan,
        CancellationToken ct)
    {
        if (plan.DelegationId is not null)
        {
            await ArchiveByDelegationCoreAsync(
                plan.DelegationId.Value, plan.ArchiveKinds, current.Id, plan.Reason, ct);
        }
        else if (plan.AppealId is not null)
        {
            await ArchiveByAppealCoreAsync(
                plan.AppealId.Value, plan.ArchiveKinds, current.Id, plan.Reason, ct);
        }
    }
}
