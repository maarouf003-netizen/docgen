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
    // ── الإحالة لرئيس القسم والتراجع عنها (شعبة → قسم، اتجاه واحد) ──────────

    /// <summary>
    /// إحالة استئناف من رئيس الشعبة لرئيس قسم فرعه (§6.2): استثناء لمرة واحدة
    /// — القسم يسند رغم الدائرة، ويبقى الاستثناء حتى الحسم (قرار §2.22).
    /// </summary>
    public async Task<AppealDto?> ForwardAsync(
        int appealId,
        ForwardAppealRequest request,
        int callerUserId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        if (appeal.Status != AppealStatusCatalog.Pending)
            throw new ArgumentException("لا يمكن إحالة استئناف لم يبق منظورًا");
        if (appeal.AssignedLawyerId is not null)
            throw new ArgumentException("لا يمكن إحالة استئناف أُسند سلفًا — التراجع قبل الإسناد فقط");
        if (appeal.ForwardState == AppealForwardCatalog.ForwardedToHead)
            throw new ArgumentException("الاستئناف محال سلفًا لرئيس القسم");
        if (request.Version.HasValue && appeal.Version != request.Version.Value)
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الاستئناف وحاول مجددًا");

        var caller = await _users.GetByIdAsync(callerUserId, ct)
            ?? throw new ArgumentException("المستخدم غير موجود");
        if (caller.Role != UserRole.SubHead)
            throw new ArgumentException("الإحالة لرئيس الشعبة فقط");
        var doc = appeal.Document;
        if (doc.BranchId is null || doc.BranchId != caller.BranchId)
            throw new ArgumentException("لا يمكنك إحالة هذا الاستئناف — ليس ضمن فرعك");
        if (doc.ExecutionCircuitId is null || doc.ExecutionCircuit?.SectionId != caller.SectionId || caller.SectionId is null)
            throw new ArgumentException("الدائرة ليست ضمن نطاقك");

        var head = await _users.FindActiveHeadAsync(UserRole.Head, doc.BranchId, null, ct)
            ?? throw new ArgumentException("لا يوجد رئيس قسم مفعّل في الفرع — تعذّرت الإحالة");
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason?.Length > 1000)
            throw new ArgumentException("طول سبب الإحالة يتجاوز الحد الأقصى (1000 حرف)");

        var now = DateTime.UtcNow;
        try
        {
            await _tx.RunAsync(async token =>
            {
                appeal.ForwardedById = caller.Id;
                appeal.ForwardedAt = now;
                appeal.ForwardState = AppealForwardCatalog.ForwardedToHead;
                appeal.ForwardReason = reason;
                appeal.UpdatedAt = now;
                appeal.Version++;
                _appeals.Update(appeal);
                await EnqueueAppealForwardedAsync(appeal, doc, caller, head, now, token);
                await _uow.SaveChangesAsync(token);
                await _audit.LogAsync(actorName, "forward_appeal",
                    appeal.DocumentId, appeal.Document.DocumentType,
                    $"يرجى التفضل بإسناد الاستئناف (ملف {appeal.DocumentId} — محال من {caller.FullName})"
                    + (reason is null ? string.Empty : $" — السبب: {reason}"), token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors?.IsConcurrencyViolation(ex) == true)
        {
            // إحالتان متزامنتان حقيقيتان — 409 ودية بدل 500 خام.
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الاستئناف وحاول مجددًا", ex);
        }

        return ToDto(appeal, ServerClock.CurrentYear(_clock, _timeZone));
    }

    /// <summary>
    /// التراجع عن الإحالة قبل الإسناد (§6.5): استرجاع المحيل أو إعادة المستلم —
    /// يُصفَّر الاستثناء وتُعاد البدائل للمحيل. بعد الإسناد: نقل عادي.
    /// </summary>
    public async Task<AppealDto?> RecallForwardAsync(
        int appealId,
        int callerUserId,
        string? actorName,
        CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        if (appeal.Status != AppealStatusCatalog.Pending)
            throw new ArgumentException("لا يمكن التراجع عن إحالة استئناف لم يبق منظورًا");
        if (appeal.AssignedLawyerId is not null)
            throw new ArgumentException("تعذّر التراجع بعد الإسناد — انقل الاستئناف نقلًا عاديًا");
        if (appeal.ForwardState != AppealForwardCatalog.ForwardedToHead || appeal.ForwardedById is null)
            throw new ArgumentException("لا توجد إحالة مفتوحة لهذا الاستئناف");

        var caller = await _users.GetByIdAsync(callerUserId, ct)
            ?? throw new ArgumentException("المستخدم غير موجود");
        var doc = appeal.Document;
        var isForwarder = appeal.ForwardedById == caller.Id;
        var isDivisionHead = caller.Role == UserRole.Head
            && doc.BranchId is not null && doc.BranchId == caller.BranchId;
        if (!isForwarder && !isDivisionHead)
            throw new ArgumentException("التراجع للمحيل أو لرئيس قسم الفرع فقط");
        var forwarderId = appeal.ForwardedById.Value;
        var forwarder = await _users.GetByIdAsync(forwarderId, ct);
        var now = DateTime.UtcNow;

        try
        {
            await _tx.RunAsync(async token =>
            {
                appeal.ForwardState = AppealForwardCatalog.Owned;
                appeal.ForwardedById = null;
                appeal.ForwardedAt = null;
                appeal.ForwardReason = null;
                appeal.UpdatedAt = now;
                appeal.Version++;
                _appeals.Update(appeal);
                await EnqueueAppealRecalledAsync(appeal, doc, caller, forwarder, now, token);
                await _uow.SaveChangesAsync(token);
                await _audit.LogAsync(actorName, "recall_forward",
                    appeal.DocumentId, appeal.Document.DocumentType,
                    isForwarder
                        ? $"استرجع المحيل إحالته للاستئناف (رقم {appeal.Id})"
                        : $"أعاد رئيس القسم الإحالة (رقم {appeal.Id}) لمحيلها", token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors?.IsConcurrencyViolation(ex) == true)
        {
            // تراجعان متزامنان حقيقيان — 409 ودية بدل 500 خام.
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الاستئناف وحاول مجددًا", ex);
        }

        return ToDto(appeal, ServerClock.CurrentYear(_clock, _timeZone));
    }
}
