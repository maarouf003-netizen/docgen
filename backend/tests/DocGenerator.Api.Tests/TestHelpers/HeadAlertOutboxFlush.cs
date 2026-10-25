using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests.TestHelpers;

/// <summary>
/// معالجة حتمية لرسائل Outbox في اختبارات التكامل التي تعطّل المشغّل الخلفي:
/// تستهلك الرسائل pending بالترتيب داخل معاملات، بنفس منطق موزّع الإنتاج.
/// </summary>
internal static class HeadAlertOutboxFlush
{
    public static async Task AllAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var alerts = scope.ServiceProvider.GetRequiredService<IHeadAlertService>();
        var tx = scope.ServiceProvider.GetRequiredService<ITransactionRunner>();

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
