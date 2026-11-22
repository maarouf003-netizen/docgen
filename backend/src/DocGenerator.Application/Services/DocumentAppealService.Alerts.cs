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

    private async Task EnqueueAppealPendingAsync(
        DocumentAppeal appeal, Document source, int actorUserId, DateTime occurredAt, CancellationToken ct)
    {
        var branchId = source.BranchId;
        if (branchId is null)
            return;

        // الإشعار الأولي لمالك واحد حسب دائرة الملف (§6.1): رئيس شعبته،
        // وإلا رئيس القسم — لا بث لفرع (قرار §2 + §17/5).
        var sectionId = source.ExecutionCircuit?.SectionId;
        var owner = sectionId.HasValue
            ? await _users.FindActiveHeadAsync(UserRole.SubHead, branchId, sectionId, ct)
                ?? await _users.FindActiveHeadAsync(UserRole.Head, branchId, null, ct)
            : await _users.FindActiveHeadAsync(UserRole.Head, branchId, null, ct);
        if (owner is null)
            return;

        await _alertOutbox.EnqueueSystemAsync(
            new CreateSystemHeadAlertRequest(
                Kind: HeadAlertKindCatalog.AppealPendingToOwner,
                Message: HeadAlertMessages.AppealPendingToOwner(new(
                    DocumentTitle(source),
                    AppealDocumentNumber(source),
                    source.FileType ?? "—",
                    AppealCourtBare(source.Court))),
                Audience: HeadAlertAudience.SingleHead,
                BranchId: branchId.Value,
                CreatedById: actorUserId,
                DocumentId: source.Id,
                AppealId: appeal.Id,
                RecipientUserId: owner.Id),
            supersedePlan: null,
            occurredAt,
            ct);
    }

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

    private Task EnqueueAppealAssignedAsync(
        DocumentAppeal appeal, User lawyer, int actorUserId,
        DateTime occurredAt, CancellationToken ct)
    {
        var source = appeal.Document;
        if (source.BranchId is null)
            return Task.CompletedTask;

        return _alertOutbox.EnqueueSystemAsync(
            new CreateSystemHeadAlertRequest(
                Kind: HeadAlertKindCatalog.AppealAssignedToLawyer,
                Message: HeadAlertMessages.AppealAssignedToLawyer(new(
                    DocumentTitle(source),
                    AppealDocumentNumber(source))),
                Audience: HeadAlertAudience.SingleLawyer,
                BranchId: source.BranchId.Value,
                CreatedById: actorUserId,
                DocumentId: source.Id,
                AppealId: appeal.Id,
                TargetLawyerId: lawyer.Id),
            new HeadAlertSupersedePlan(
                DelegationId: null,
                AppealId: appeal.Id,
                ArchiveKinds: HeadAlertKindCatalog.AppealKinds,
                Reason: "appeal_assigned"),
            occurredAt,
            ct);
    }

    private Task EnqueueAppealFinalAsync(
        DocumentAppeal appeal, bool decided, int actorUserId,
        DateTime occurredAt, CancellationToken ct)
    {
        var source = appeal.Document;
        if (source.BranchId is null || source.CreatedById == appeal.AssignedLawyerId)
            return Task.CompletedTask; // لا إشعار ذاتيًا عندما المتابع هو نفسه محامي الملف الأساس.

        var outcomePart = decided && appeal.Outcome is not null
            ? $" — النتيجة: {AppealOutcomeCatalog.ToLabel(appeal.Outcome)}"
            : string.Empty;

        return _alertOutbox.EnqueueSystemAsync(
            new CreateSystemHeadAlertRequest(
                Kind: HeadAlertKindCatalog.AppealFinalToBaseLawyer,
                Message: HeadAlertMessages.AppealFinalToBaseLawyer(new(
                    DocumentTitle(source),
                    AppealDocumentNumber(source),
                    decided ? "محسومًا" : "مشطوبًا",
                    outcomePart)),
                Audience: HeadAlertAudience.DocumentCreator,
                BranchId: source.BranchId.Value,
                CreatedById: actorUserId,
                DocumentId: source.Id,
                AppealId: appeal.Id),
            supersedePlan: null,
            occurredAt,
            ct);
    }
}
