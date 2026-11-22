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
    // ── الإسناد والنقل (رئيس القسم والشعبة بنطاقه) ──────────────────────────

    public async Task<AppealDto?> AssignAsync(
        int appealId,
        AssignAppealRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await LoadForHeadActionAsync(appealId, userId, ct);
        if (appeal.AssignedLawyerId is not null)
            throw new ArgumentException("لا يمكن إسناد استئناف أُسند سلفًا");
        if (request.Version.HasValue && appeal.Version != request.Version.Value)
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الاستئناف وحاول مجددًا");

        var lawyer = await ResolveTargetLawyerAsync(request.AssignedLawyerId, appeal.Document.BranchId, ct);

        appeal.AssignedLawyerId = lawyer.Id;
        appeal.AssignedAt = DateTime.UtcNow;
        appeal.UpdatedAt = DateTime.UtcNow;
        appeal.Version++;

        // التعارض الحقيقي (سباق بعد الفحص) يرميه الرمز (`IsConcurrencyToken`) —
        // المعالج العام يترجمه `409` برسالة ودية (RF-010).
        await _tx.RunAsync(async token =>
        {
            _appeals.Update(appeal);
            await EnqueueAppealAssignedAsync(appeal, lawyer, userId, DateTime.UtcNow, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "assign_appeal",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"أسند الاستئناف (رقم {appeal.Id}) إلى المحامي {lawyer.FullName}", token);
        }, ct);

        return ToDto(appeal, ServerClock.CurrentYear(_clock, _timeZone));
    }

    public async Task<AppealDto?> TransferAsync(
        int appealId,
        TransferAppealRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await LoadForHeadActionAsync(appealId, userId, ct);
        if (request.TargetLawyerId == appeal.AssignedLawyerId)
            throw new ArgumentException("الاستئناف مسند لهذا المحامي سلفًا");
        if (request.Version.HasValue && appeal.Version != request.Version.Value)
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الاستئناف وحاول مجددًا");

        var lawyer = await ResolveTargetLawyerAsync(request.TargetLawyerId, appeal.Document.BranchId, ct);

        appeal.AssignedLawyerId = lawyer.Id;
        appeal.AssignedAt = DateTime.UtcNow;
        appeal.UpdatedAt = DateTime.UtcNow;
        appeal.Version++;

        await _tx.RunAsync(async token =>
        {
            _appeals.Update(appeal);
            await EnqueueAppealAssignedAsync(appeal, lawyer, userId, DateTime.UtcNow, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "transfer_appeal",
                appeal.DocumentId, appeal.Document.DocumentType,
                $"نقل الاستئناف (رقم {appeal.Id}) إلى المحامي {lawyer.FullName}", token);
        }, ct);

        return ToDto(appeal, ServerClock.CurrentYear(_clock, _timeZone));
    }

    public async Task<int> TransferAllAsync(
        TransferAllAppealsRequest request,
        int? headBranchId,
        int? ownerSectionId,
        string? actorName,
        CancellationToken ct = default)
    {
        if (headBranchId is null)
            throw new ArgumentException("رئيس القسم دون فرع لا يمكنه نقل الاستئنافات");
        if (request.SourceLawyerId == request.TargetLawyerId)
            throw new ArgumentException("لا يمكن نقل الاستئنافات إلى المحامي نفسه");

        var target = await ResolveTargetLawyerAsync(request.TargetLawyerId, headBranchId.Value, ct);

        // النقل الجملة للمنظورة ضمن نطاق المنفِّذ فقط (§5.5) — القراءة داخل المعاملة
        // (F5: منع الكتابة فوق تغيير متزامن)، وإعادة فحص `Pending` قبل كل كتابة،
        // والرمز `IsConcurrencyToken` على `Version` يترجم السباق `409` ودية عبر
        // المعالج العام — مرآة المسارين المفردين (`AssignAsync`/`TransferAsync`).
        var moved = 0;
        await _tx.RunAsync(async token =>
        {
            // D5: العدّاد يُصفَّر داخل المعاملة — فلو أُعيد تنفيذ الـlambda (إعادة
            // محاولة مستقبلية عند تفعيل `EnableRetryOnFailure`) لا يزدوج العدّ.
            moved = 0;
            var pendingOnly = await _appeals.ListPendingByAssigneeInScopeAsync(
                request.SourceLawyerId, headBranchId.Value, ownerSectionId, token);
            foreach (var appeal in pendingOnly)
            {
                if (appeal.Status != AppealStatusCatalog.Pending)
                    continue;
                appeal.AssignedLawyerId = target.Id;
                appeal.AssignedAt = DateTime.UtcNow;
                appeal.UpdatedAt = DateTime.UtcNow;
                appeal.Version++;
                _appeals.Update(appeal);
                moved++;
            }
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "transfer_all_appeals",
                details: $"نقل {moved} استئنافًا منظورًا من المحامي (رقم {request.SourceLawyerId}) إلى المحامي {target.FullName}", ct: token);
        }, ct);

        return moved;
    }

    public async Task<int> CountByAssigneeForHeadAsync(int assigneeId, int? headBranchId, int? ownerSectionId, CancellationToken ct = default)
    {
        if (headBranchId is null)
            throw new ArgumentException("رئيس القسم دون فرع لا يمكنه الاطلاع على الاستئنافات");
        // المعاينة بالنطاق نفسه (§5.5): عدّ قاعدي يطابق المنقول فعلًا.
        return await _appeals.CountPendingByAssigneeInScopeAsync(
            assigneeId, headBranchId.Value, ownerSectionId, ct);
    }

    private async Task<DocumentAppeal> LoadForHeadActionAsync(int appealId, int callerUserId, CancellationToken ct)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        if (appeal.Status != AppealStatusCatalog.Pending)
            throw new ArgumentException("لا يمكن التنفيذ على استئناف لم يبق منظورًا");
        var caller = await _users.GetByIdAsync(callerUserId, ct)
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

    private async Task<User> ResolveTargetLawyerAsync(int lawyerId, int? branchId, CancellationToken ct)
    {
        var lawyer = await _users.GetByIdAsync(lawyerId, ct);
        if (lawyer is null || lawyer.Role != UserRole.Lawyer)
            throw new ArgumentException("المحامي المختص غير موجود");
        if (!lawyer.IsActive)
            throw new ArgumentException("المحامي المختص غير مفعل");
        if (branchId is null || lawyer.BranchId != branchId)
            throw new ArgumentException("المحامي المختص ليس ضمن فرع الملف");
        return lawyer;
    }
}
