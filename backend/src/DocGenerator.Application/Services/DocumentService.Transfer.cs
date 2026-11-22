using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentService : IDocumentService
{
    public async Task<DocumentResponse> TransferAsync(int documentId, int targetLawyerId, string? actorName, CancellationToken ct = default, int? actorUserId = null)
    {
        return await _tx.RunAsync(async token =>
        {
            // كل التحققات داخل المعاملة نفسها لتفادي أي TOCTOU: حالة الهدف (وجود/دور/تفعيل)
            // تُقرأ في نفس لقطة المعاملة التي يُنفَّذ فيها النقل الذرّي، فلو عُطّل المحامي
            // أو تغيّرت بياناته لحظياً أثناء النقل لن يُنقل الملف إليه.
            var target = await _users.GetByIdAsync(targetLawyerId, token);
            if (target is null || target.Role != UserRole.Lawyer)
                throw new ArgumentException("المحامي المستهدف غير موجود");
            if (!target.IsActive)
                throw new ArgumentException("المحامي المستهدف غير مفعل");

            var doc = await _documents.GetByIdAsync(documentId, token);
            if (doc is null)
                throw new KeyNotFoundException();

            if (doc.CreatedById == target.Id)
                throw new ArgumentException("لا يمكن نقل الملف إلى المحامي المختص به حاليًا");
            if (target.BranchId != doc.BranchId)
                throw new ArgumentException("لا يمكن نقل الملف إلى محامٍ من فرع آخر");

            var sourceOwnerId = doc.CreatedById;
            var transferred = await _documents.TransferOwnerAsync(
                documentId, doc.CreatedById, target.Id, target.FullName,
                doc.Lawyer ?? doc.CreatedBy?.FullName ?? string.Empty, token);
            if (transferred is null)
                throw new DocumentConflictException("تغيّر المحامي المختص للملف أثناء النقل — أعد المحاولة");

            // سجل تعاقب: الملف أُحيل إلى المحامي المستهدف بتاريخ النقل.
            await _documents.AddAssignmentAsync(documentId, AssignmentKindCatalog.Transfer,
                target.FullName, actorName, DateTime.UtcNow, token);

            // الملف المناب: يُزامَن المحامي المختص المخزّن على الإنابة مع المالك الجديد
            // في نفس المعاملة، فتعرض «تشعبات الملف» ونافذة الإتمام المتابع الحالي.
            await _delegationService.SyncTargetsTransferredAsync(
                new[] { documentId }, target.Id, actorName, actorUserId, token);

            await _audit.LogAsync(actorName, "transfer", documentId, doc.DocumentType,
                DocumentText.AuditWithActor($"نقل الملف إلى المحامي: {target.FullName}", doc), token);

            // H2: التنبيه يتبع المالك — داخل معاملة النقل كي لا يبقى تنبيه قديم بلا مالك.
            if (_executionCircuits is not null && actorUserId.HasValue)
                await _executionCircuits.SyncPendingAlertsAfterTransferAsync(
                    sourceOwnerId, target.Id, doc.BranchId ?? target.BranchId ?? 0, actorUserId.Value, token);

            return DocumentResponse.FromEntity(transferred, CurrentYear());
        }, ct);
    }

    public async Task<int> CountFilesByOwnerAsync(int ownerId, int? scopeBranchId, CancellationToken ct = default, int? ownerSectionId = null)
    {
        var owner = await _users.GetByIdAsync(ownerId, ct);
        if (owner is null || owner.Role != UserRole.Lawyer)
            throw new ArgumentException("المحامي غير موجود");
        if (scopeBranchId.HasValue && owner.BranchId != scopeBranchId)
            throw new ArgumentException("لا يمكن عرض ملفات محامٍ من فرع آخر");
        // المعاينة بالنطاق نفسه (§5.5): تطابق المنقول فعلًا.
        return await _documents.CountByOwnerInScopeAsync(ownerId, scopeBranchId, ownerSectionId, ct);
    }

    public async Task<int> TransferAllAsync(int sourceLawyerId, int targetLawyerId, int? scopeBranchId, string? actorName, CancellationToken ct = default, string? idempotencyKey = null, int? ownerSectionId = null, int? actorUserId = null)
    {
        // RF-011: حجز المفتاح أولًا (لا تحقق مسبق يستحق `400` هنا — التحققات داخل
        // المعاملة عمدًا ضد `TOCTOU`) — النطاق محامي المصدر (النية = نقل ملفاته) —
        // التكرار يُعيد العدد المخزن نفسه.
        var ticket = await IdempotencyGuard.BeginAsync(_idempotency, "documents.transfer-all",
            sourceLawyerId, idempotencyKey,
            IdempotencyGuard.Fingerprint(new { sourceLawyerId, targetLawyerId, scopeBranchId, ownerSectionId }), ct);

        int transferred;
        try
        {
            transferred = await TransferAllCoreAsync(
                sourceLawyerId, targetLawyerId, scopeBranchId, ownerSectionId, actorName, actorUserId, ct);
        }
        catch
        {
            await IdempotencyGuard.ReleaseAsync(_idempotency, ticket, ct);
            throw;
        }

        // اللقطة بشكل الاستجابة نفسه (`{"transferredCount":N}`) ليُعاد حرفيًا عند التكرار.
        await IdempotencyGuard.CompleteAsync(_idempotency, ticket,
            IdempotencyGuard.Snapshot(new { transferredCount = transferred }), ct: ct);
        return transferred;
    }

    /// <summary>نواة النقل الجماعي (تحققات + نقل + تدقيق) — تُستدعى بعد حجز مفتاح عدم التكرار.</summary>
    private async Task<int> TransferAllCoreAsync(
        int sourceLawyerId, int targetLawyerId, int? scopeBranchId, int? ownerSectionId, string? actorName,
        int? actorUserId, CancellationToken ct)
    {
        return await _tx.RunAsync(async token =>
        {
            // كل التحققات داخل المعاملة نفسها لتفادي أي TOCTOU: حالة المصدر والهدف
            // (وجود/دور/تفعيل/فرع) تُقرأ في نفس لقطة المعاملة التي يُنفَّذ فيها النقل،
            // ويُتحقق من تطابق عدد الملفات المتوقعة مع الصفوف المتأثرة فعلياً.
            var source = await _users.GetByIdAsync(sourceLawyerId, token);
            if (source is null || source.Role != UserRole.Lawyer)
                throw new ArgumentException("المحامي المطلوب نقل ملفاته غير موجود");
            if (source.Id == targetLawyerId)
                throw new ArgumentException("لا يمكن نقل الملفات إلى المحامي صاحبها");
            if (scopeBranchId.HasValue && source.BranchId != scopeBranchId)
                throw new ArgumentException("لا يمكن نقل ملفات محامٍ من فرع آخر");

            var target = await _users.GetByIdAsync(targetLawyerId, token);
            if (target is null || target.Role != UserRole.Lawyer)
                throw new ArgumentException("المحامي المستهدف غير موجود");
            if (!target.IsActive)
                throw new ArgumentException("المحامي المستهدف غير مفعل");
            if (source.BranchId != target.BranchId)
                throw new ArgumentException("لا يمكن نقل الملفات إلى محامٍ من فرع آخر");

            var files = await _documents.ListByOwnerInScopeAsync(sourceLawyerId, scopeBranchId, ownerSectionId, token);
            if (files.Count == 0)
                return 0;

            var moved = await _documents.TransferAllOwnerAsync(sourceLawyerId, target.Id, target.FullName, source.FullName, scopeBranchId, ownerSectionId, token);
            if (moved != files.Count)
                throw new DocumentConflictException("تغيّرت بيانات الملفات أثناء النقل الجماعي — أعد المحاولة");

            var now = ServerClock.Now(_clock, _timeZone);
            foreach (var file in files)
            {
                // سجل تعاقب لكل ملف أُحيل إلى المحامي المستهدف.
                await _documents.AddAssignmentAsync(file.Id, AssignmentKindCatalog.Transfer,
                    target.FullName, actorName, DateTime.UtcNow, token);
                await _audit.LogAsync(actorName, "transfer", file.Id, file.DocumentType,
                    $"تم إحالة هذا الملف إلى المحامي: {target.FullName} بتاريخ {now:d/M/yyyy} — المنفذ عليه: {DocumentText.ActorFullName(file)}", token);
            }

            // ملفات منابة ضمن الدفعة: يُزامَن المحامي المختص المخزّن على إناباتها
            // مع المالك الجديد (استعلام واحد + كتابة داخل نفس المعاملة).
            await _delegationService.SyncTargetsTransferredAsync(
                files.Select(f => f.Id).ToArray(), target.Id, actorName, actorUserId, token);

            // H2: التنبيه يتبع المالك — داخل معاملة النقل الجماعي كي لا يبقى تنبيه قديم بلا مالك.
            if (_executionCircuits is not null && actorUserId.HasValue)
                await _executionCircuits.SyncPendingAlertsAfterTransferAsync(
                    sourceLawyerId, target.Id, target.BranchId ?? scopeBranchId ?? 0, actorUserId.Value, token);

            return moved;
        }, ct);
    }
}
