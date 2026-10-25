using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests.TestHelpers;

internal static class HeadAlertTestSeed
{
    public static async Task<int> SeedSingleHeadAsync(
        ApiFactory factory,
        int branchId,
        int createdById,
        int recipientUserId,
        string message,
        CancellationToken ct = default)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
        var alerts = scope.ServiceProvider.GetRequiredService<IHeadAlertService>();
        var tx = scope.ServiceProvider.GetRequiredService<ITransactionRunner>();

        await tx.RunAsync(async token =>
        {
            await alerts.CreateSystemAsync(
                new CreateSystemHeadAlertRequest(
                    HeadAlertKindCatalog.AppealReferralToHead,
                    message,
                    HeadAlertAudience.SingleHead,
                    branchId,
                    createdById,
                    RecipientUserId: recipientUserId),
                DateTime.UtcNow,
                supersedePlan: null,
                targetUserIdOverride: null,
                token);
        }, ct);

        return await db.HeadAlerts
            .AsNoTracking()
            .Where(a => a.BranchId == branchId
                && a.Kind == HeadAlertKindCatalog.AppealReferralToHead
                && a.Message == message)
            .OrderByDescending(a => a.Id)
            .Select(a => a.Id)
            .FirstAsync(ct);
    }
}
