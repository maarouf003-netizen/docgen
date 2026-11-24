using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// قواعد الاستئناف المشتركة بين الخدمة ومراحل `P4-01` — حرّاس الصلاحية/الحالة
/// المشتركة بين التعديل والحذف، فلا تنحرف رسالة أو ترتيب بين المسارين.
/// </summary>
internal static class DocumentAppealRules
{
    /// <summary>شروط الاستئناف على الملف: مقيد (ليس تحت الرفع). الشطب لا يمنع الاستئناف.</summary>
    internal static void ValidateSourceForAppeal(Document source)
    {
        if (source.NeedsRegistration)
            throw new ArgumentException("لا يمكن تسطير استئناف على ملف بانتظار إعادة القيد — أدخل رقمه الجديد أولًا");
        if (source.IsDraft)
            throw new ArgumentException("لا يمكن الاستئناف على ملف تحت الرفع");
    }

    /// <summary>
    /// الصلاحية أولًا (403)، ثم حالة الاستئناف (400) — لا كشف لحالة استئناف الغير
    /// (`P2-02f`).
    /// </summary>
    internal static void EnsurePendingByCreator(DocumentAppeal appeal, int userId)
    {
        if (appeal.CreatedById != userId)
            throw new UnauthorizedAccessException("لا يمكنك تعديل أو حذف استئناف لم تسطّره");
        if (appeal.AssignedLawyerId is not null || appeal.Status != AppealStatusCatalog.Pending)
            throw new ArgumentException("لا يمكن تعديل أو حذف استئناف بعد إسناده");
    }

    /// <summary>
    /// تحميل استئناف لتنفيذ رئيس (قسم/شعبة) مع فحص الحالة والفرع والنطاق —
    /// منقول حرفيًا من خدمة الاستئناف (قرار §2.22 استثناء المحال لرئيس القسم).
    /// </summary>
    internal static async Task<DocumentAppeal> LoadForHeadActionAsync(
        int appealId, int callerUserId, IAppealRepository appeals, IUserRepository users, CancellationToken ct)
    {
        var appeal = await appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        if (appeal.Status != AppealStatusCatalog.Pending)
            throw new ArgumentException("لا يمكن التنفيذ على استئناف لم يبق منظورًا");
        var caller = await users.GetByIdAsync(callerUserId, ct)
            ?? throw new ArgumentException("المستخدم غير موجود");
        var doc = appeal.Document;
        if (doc.BranchId is null || doc.BranchId != caller.BranchId)
            throw new ArgumentException("لا يمكنك التنفيذ على هذا الاستئناف — ليس ضمن فرعك");
        if (caller.Role == UserRole.Head)
        {
            // رئيس القسم: ملفات دوائر القسم + المحال له استثناءً حتى الحسم (قرار §2.22).
            var sectionId = doc.ExecutionCircuit?.SectionId;
            if (sectionId.HasValue && appeal.ForwardState != AppealForwardCatalog.ForwardedToHead)
                throw new ArgumentException("الدائرة ليست ضمن نطاقك");
            return appeal;
        }
        if (caller.Role == UserRole.SubHead)
        {
            // رئيس الشعبة: ملفات دوائر شعبته غير المحالة (المحال صار لرئيس القسم).
            if (caller.SectionId is null)
                throw new ArgumentException("حسابك بلا شعبة — أعد الدخول");
            if (doc.ExecutionCircuitId is null || doc.ExecutionCircuit?.SectionId != caller.SectionId)
                throw new ArgumentException("الدائرة ليست ضمن نطاقك");
            if (appeal.ForwardState == AppealForwardCatalog.ForwardedToHead)
                throw new ArgumentException("الاستئناف محال لرئيس القسم — بانتظار إسناده");
            return appeal;
        }
        throw new ArgumentException("إسناد الاستئنافات للرؤساء فقط");
    }

    /// <summary>محامٍ مختص مستهدف: وجود/دور/تفعيل/فرع — برسائله الثلاث.</summary>
    internal static async Task<User> ResolveTargetLawyerAsync(
        int lawyerId, int? branchId, IUserRepository users, CancellationToken ct)
    {
        var lawyer = await users.GetByIdAsync(lawyerId, ct);
        if (lawyer is null || lawyer.Role != UserRole.Lawyer)
            throw new ArgumentException("المحامي المختص غير موجود");
        if (!lawyer.IsActive)
            throw new ArgumentException("المحامي المختص غير مفعل");
        if (branchId is null || lawyer.BranchId != branchId)
            throw new ArgumentException("المحامي المختص ليس ضمن فرع الملف");
        return lawyer;
    }

    /// <summary>
    /// تنبيه المحامي المسند إليه عبر `Outbox` (استبدال عائلة تنبيهات الاستئناف) —
    /// منقول حرفيًا من `DocumentAppealService.Alerts.cs`.
    /// </summary>
    internal static Task EnqueueAppealAssignedAsync(
        DocumentAppeal appeal, User lawyer, int actorUserId, DateTime occurredAt, int currentYear,
        IHeadAlertOutbox alertOutbox, CancellationToken ct)
    {
        var source = appeal.Document;
        if (source.BranchId is null)
            return Task.CompletedTask;

        return alertOutbox.EnqueueSystemAsync(
            new CreateSystemHeadAlertRequest(
                Kind: HeadAlertKindCatalog.AppealAssignedToLawyer,
                Message: HeadAlertMessages.AppealAssignedToLawyer(new(
                    DocumentAppealService.DocumentTitle(source),
                    DocumentAppealService.AppealDocumentNumberText(source, currentYear))),
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

    /// <summary>
    /// تنبيه صاحب الملف الأساس بنتيجة الحسم/الشطب — منقول حرفيًا من
    /// `DocumentAppealService.Alerts.cs` (بلا إشعار ذاتي عندما المتابع هو صاحب الملف).
    /// </summary>
    internal static Task EnqueueAppealFinalAsync(
        DocumentAppeal appeal, bool decided, int actorUserId, DateTime occurredAt,
        int currentYear, IHeadAlertOutbox alertOutbox, CancellationToken ct)
    {
        var source = appeal.Document;
        if (source.BranchId is null || source.CreatedById == appeal.AssignedLawyerId)
            return Task.CompletedTask; // لا إشعار ذاتيًا عندما المتابع هو نفسه محامي الملف الأساس.

        var outcomePart = decided && appeal.Outcome is not null
            ? $" — النتيجة: {AppealOutcomeCatalog.ToLabel(appeal.Outcome)}"
            : string.Empty;

        return alertOutbox.EnqueueSystemAsync(
            new CreateSystemHeadAlertRequest(
                Kind: HeadAlertKindCatalog.AppealFinalToBaseLawyer,
                Message: HeadAlertMessages.AppealFinalToBaseLawyer(new(
                    DocumentAppealService.DocumentTitle(source),
                    DocumentAppealService.AppealDocumentNumberText(source, currentYear),
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

    /// <summary>تنبيه رئيس القسم بإحالة الاستئناف — منقول حرفيًا من خدمة الاستئناف.</summary>
    internal static Task EnqueueAppealForwardedAsync(
        DocumentAppeal appeal, Document source, User caller, User head,
        DateTime occurredAt, int currentYear, IHeadAlertOutbox alertOutbox, CancellationToken ct)
    {
        return alertOutbox.EnqueueSystemAsync(
            new CreateSystemHeadAlertRequest(
                Kind: HeadAlertKindCatalog.AppealReferralToHead,
                Message: HeadAlertMessages.AppealReferralToHead(new(
                    DocumentAppealService.DocumentTitle(source),
                    DocumentAppealService.AppealDocumentNumberText(source, currentYear),
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

    /// <summary>تنبيه/أرشفة عند التراجع عن الإحالة — منقول حرفيًا.</summary>
    internal static async Task EnqueueAppealRecalledAsync(
        DocumentAppeal appeal, Document source, User caller, User? forwarder,
        DateTime occurredAt, int currentYear, IHeadAlertOutbox alertOutbox,
        IHeadAlertService alerts, CancellationToken ct)
    {
        if (forwarder is null || source.BranchId is null)
        {
            await alerts.ArchiveByAppealAsync(
                appeal.Id,
                HeadAlertKindCatalog.AppealKinds,
                "appeal_recalled_no_recipient",
                ct);
            return;
        }

        await alertOutbox.EnqueueSystemAsync(
            new CreateSystemHeadAlertRequest(
                Kind: HeadAlertKindCatalog.AppealReturnedToHead,
                Message: HeadAlertMessages.AppealReturnedToHead(new(
                    DocumentAppealService.DocumentTitle(source),
                    DocumentAppealService.AppealDocumentNumberText(source, currentYear))),
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
