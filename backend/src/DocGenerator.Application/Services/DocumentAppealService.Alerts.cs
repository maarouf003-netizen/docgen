using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentAppealService : IDocumentAppealService
{
    // ── تسجيل التنبيهات في صندوق الصادر داخل المعاملة ──────────────────────

    private Task EnqueueAppealForwardedAsync(
        DocumentAppeal appeal, Document source, User caller, User head,
        DateTime occurredAt, CancellationToken ct)
    {
        return _alertOutbox.EnqueueSystemAsync(
            new CreateSystemHeadAlertRequest(
                Kind: HeadAlertKindCatalog.AppealReferralToHead,
                Message: HeadAlertMessages.AppealReferralToHead(new(
                    DocumentTitle(source),
                    AppealDocumentNumber(source),
                    caller.FullName)),
                Audience: HeadAlertAudience.SingleHead,
                BranchId: source.BranchId!.Value,
                CreatedById: caller.Id,
                DocumentId: source.Id,
                AppealId: appeal.Id,
                RecipientUserId: head.Id),
            new HeadAlertSupersedePlan(
                DelegationId: null,
                AppealId: appeal.Id,
                ArchiveKinds: HeadAlertKindCatalog.AppealKinds,
                Reason: "appeal_forwarded"),
            occurredAt,
            ct);
    }

    private async Task EnqueueAppealRecalledAsync(
        DocumentAppeal appeal, Document source, User caller, User? forwarder,
        DateTime occurredAt, CancellationToken ct)
    {
        if (forwarder is null || source.BranchId is null)
        {
            await _alerts.ArchiveByAppealAsync(
                appeal.Id,
                HeadAlertKindCatalog.AppealKinds,
                "appeal_recalled_no_recipient",
                ct);
            return;
        }

        await _alertOutbox.EnqueueSystemAsync(
            new CreateSystemHeadAlertRequest(
                Kind: HeadAlertKindCatalog.AppealReturnedToHead,
                Message: HeadAlertMessages.AppealReturnedToHead(new(
                    DocumentTitle(source),
                    AppealDocumentNumber(source))),
                Audience: HeadAlertAudience.SingleHead,
                BranchId: source.BranchId.Value,
                CreatedById: caller.Id,
                DocumentId: source.Id,
                AppealId: appeal.Id,
                RecipientUserId: forwarder.Id),
            new HeadAlertSupersedePlan(
                DelegationId: null,
                AppealId: appeal.Id,
                ArchiveKinds: HeadAlertKindCatalog.AppealKinds,
                Reason: "appeal_recalled"),
            occurredAt,
            ct);
    }
}
