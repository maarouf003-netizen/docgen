using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// معالجة حتمية لرسائل Outbox في اختبارات الخدمات التي لا تشغّل المضيف الخلفي:
/// تستهلك الرسائل pending بالترتيب داخل معاملات، بنفس منطق موزّع الإنتاج.
/// </summary>
internal static class HeadAlertOutboxFlush
{
    public static async Task AllAsync(
        DocGeneratorDbContext db,
        IHeadAlertService alerts,
        ITransactionRunner tx,
        CancellationToken ct = default)
    {
        while (true)
        {
            var message = await db.HeadAlertOutboxMessages
                .Where(m => m.Status == HeadAlertOutboxStatus.Pending)
                .OrderBy(m => m.Id)
                .FirstOrDefaultAsync(ct);
            if (message is null)
                return;

            var payload = HeadAlertOutboxPayloadSerializer.Deserialize(message.Payload);
            await tx.RunAsync(async token =>
            {
                if (message.Operation == HeadAlertOutboxOperation.Circuit_Renamed)
                {
                    await alerts.ApplyCircuitRenamedAsync(
                        payload.CircuitId ?? throw new InvalidOperationException("circuit id missing"),
                        payload.Message ?? string.Empty,
                        payload.OccurredAt,
                        token);
                }
                else
                {
                    var request = payload.Request
                        ?? throw new InvalidOperationException("request missing");
                    var plan = payload.SupersedePlan is null
                        ? null
                        : new HeadAlertSupersedePlan(
                            payload.SupersedePlan.DelegationId,
                            payload.SupersedePlan.AppealId,
                            new HashSet<string>(payload.SupersedePlan.ArchiveKinds, StringComparer.Ordinal),
                            payload.SupersedePlan.Reason);

                    await alerts.CreateSystemAsync(
                        request,
                        payload.OccurredAt,
                        plan,
                        payload.TargetUserId,
                        token);
                }

                db.HeadAlertOutboxMessages.Remove(message);
                await db.SaveChangesAsync(token);
            }, ct);
        }
    }
}
