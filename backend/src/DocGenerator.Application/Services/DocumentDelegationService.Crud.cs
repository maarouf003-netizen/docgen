using System.Globalization;
using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentDelegationService : IDocumentDelegationService
{
    public async Task<DelegationDto> CreateAsync(
        int sourceDocumentId,
        UpsertDelegationRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var source = await _documents.GetByIdAsync(sourceDocumentId, ct)
            ?? throw new KeyNotFoundException("الملف المنيب غير موجود");

        ValidateSourceForDelegation(source);
        // P2-02e: فشل الصلاحية يجب أن يظهر 403 لا 400 — الرسالة تبقى كما هي للعميل.
        if (source.CreatedById != userId)
            throw new UnauthorizedAccessException("لا يمكنك تسطير إنابة على ملف لا تملكه");
        if (source.NeedsRegistration)
            throw new ArgumentException("لا يمكن تسطير إنابة على ملف بانتظار إعادة القيد — أدخل رقمه الجديد أولًا");

        var (court, circuitId, fields) = await ValidateAndBuildAsync(request, source.BranchId, ct);

        // الاستهداف (§7.1): مالك الدائرة المنابة (رئيس شعبته وإلا قسمه)،
        // وخارجيةً رئيس قسم الفرع المناب، وبلا دائرة رئيس قسم فرع المنيب.
        var (alertBranch, ownerId) = await ResolvePendingAlertTargetAsync(
            fields.IsExternal, fields.ExternalBranchId, source.BranchId, circuitId, ct);
        var pendingApprovalMessage = HeadAlertMessages.DelegationPendingApproval(new(
            source.CreatedBy?.FullName ?? string.Empty, SourceLabel(source), court));

        DocumentDelegation delegation = null!;
        await _tx.RunAsync(async token =>
        {
            // B2: الفحص وبناء اللقطة والحفظ داخل المعاملة نفسها — لا فحص حجب خارجها.
            // إعادة قراءة طازجة بلا تتبع: قراءات السياق المتتبَّع تُرجع المثيلات نفسها
            // (identity resolution) ولا ترى ما التزمته معاملة منافسة بعد القراءة الأولى.
            var fresh = await _documents.GetByIdWithDelegationsNoTrackingAsync(source.Id, token)
                ?? throw new ArgumentException("الملف المنيب غير موجود");
            await PruneStaleReservationsAsync(fresh, token);
            await ValidateAssetsNotBlockedAsync(fresh, request.AssetIds, excludeDelegationId: null, token);

            delegation = new DocumentDelegation
            {
                SourceDocumentId = source.Id,
                DelegatedCourt = court,
                // `PB-001`: الدائرة المنابة المعيارية (تضبط مع الخام).
                DelegatedCourtNorm = string.IsNullOrWhiteSpace(court)
                    ? null
                    : ArabicNameNormalizer.Normalize(court),
                DelegatedCircuitId = circuitId,
                IsExternal = fields.IsExternal,
                ExternalBranchId = fields.ExternalBranchId,
                DelegationDate = fields.DelegationDate,
                DelegationText = Normalize(request.DelegationText),
                DepositBookNumber = Normalize(request.DepositBookNumber),
                DepositBookDate = fields.DepositBookDate,
                Status = DelegationStatusCatalog.PendingHead,
                CreatedById = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            ApplyDelegationAssets(delegation, fresh, request.AssetIds);

            await _delegations.AddAsync(delegation, token);
            await _uow.SaveChangesAsync(token);

            // B3: حجز ما استهلكته لقطات الإنابة الجديدة من المسبح بعد الحواجب — اللغة
            // المشتركة مع المسح (التوائم قابلة للتبادل ولا تحمل اللقطات معرفاتها):
            // يُحجز المستهلَك لا المطلوب حرفيًا، وإلا اختلف المرجعان على التوأم المحجوب.
            var blocked = ComputeBlockedIds(fresh, excludeDelegationId: null);
            var pool = fresh.Assets.OrderBy(a => a.Id).Where(a => !blocked.Contains(a.Id)).ToList();
            var ownConsumed = ConsumeSnapshots(pool, delegation.Assets.OrderBy(s => s.Id), new HashSet<int>(blocked));
            foreach (var assetId in ownConsumed)
                await _reservations.AddAsync(new DelegationAssetReservation
                {
                    DelegationId = delegation.Id,
                    SourceDocumentId = source.Id,
                    AssetId = assetId,
                }, token);

            await _alertOutbox.EnqueueSystemAsync(
                new CreateSystemHeadAlertRequest(
                    HeadAlertKindCatalog.DelegationPendingApproval,
                    pendingApprovalMessage,
                    HeadAlertAudience.SingleHead,
                    alertBranch,
                    userId,
                    DocumentId: source.Id,
                    DelegationId: delegation.Id,
                    RecipientUserId: ownerId),
                supersedePlan: null,
                occurredAt: DateTime.UtcNow,
                ct: token);
            try
            {
                await _uow.SaveChangesAsync(token);
            }
            catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
            {
                // الخاسر في السباق: نفس رسالة الحارس (400 بالمجال) لا 500.
                throw new ArgumentException(BlockedAssetsMessage(fresh, request.AssetIds), ex);
            }

            await _audit.LogAsync(actorName, "create_delegation",
                source.Id, source.DocumentType,
                $"سطّر إنابة على الملف (رقم {source.Id}) إلى {court}", token);
        }, ct);

        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        return ToDto(delegation, source, currentYear);
    }

    public async Task<DelegationDto?> UpdateAsync(
        int delegationId,
        UpsertDelegationRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var delegation = await _delegations.GetByIdWithDetailsAsync(delegationId, ct)
            ?? throw new KeyNotFoundException("الإنابة غير موجودة");

        EnsurePendingByOwner(delegation, userId);
        var source = delegation.SourceDocument;

        var (court, circuitId, fields) = await ValidateAndBuildAsync(request, source.BranchId, ct);

        // التصحيح قد يغيّر مالك الاعتماد (دائرة/فرع)، فتُبنى هوية المستلم الجديدة
        // مسبقًا، ويُستبدل أي تنبيه سابق للإنابة عند معالجة Outbox.
        var (alertBranch, ownerId) = await ResolvePendingAlertTargetAsync(
            fields.IsExternal, fields.ExternalBranchId, source.BranchId, circuitId, ct);
        var pendingApprovalMessage = HeadAlertMessages.DelegationPendingApproval(new(
            source.CreatedBy?.FullName ?? string.Empty, SourceLabel(source), AlertCourtAfterDaira(court)));

        await _tx.RunAsync(async token =>
        {
            // B2+D1: الفحص أولًا على قراءة طازجة بلا تتبع، ثم أي إسناد على الكيان —
            // داخل المعاملة نفسها. الرفض لا يترك حالة Modified عالقة.
            var fresh = await _documents.GetByIdWithDelegationsNoTrackingAsync(source.Id, token)
                ?? throw new ArgumentException("الملف المنيب غير موجود");
            await PruneStaleReservationsAsync(fresh, token);
            await ValidateAssetsNotBlockedAsync(fresh, request.AssetIds, excludeDelegationId: delegation.Id, token);

            delegation.DelegatedCourt = court;
            delegation.DelegatedCourtNorm = string.IsNullOrWhiteSpace(court)
                ? null
                : ArabicNameNormalizer.Normalize(court);
            delegation.DelegatedCircuitId = circuitId;
            delegation.IsExternal = fields.IsExternal;
            delegation.ExternalBranchId = fields.ExternalBranchId;
            delegation.DelegationDate = fields.DelegationDate;
            delegation.DelegationText = Normalize(request.DelegationText);
            delegation.DepositBookNumber = Normalize(request.DepositBookNumber);
            delegation.DepositBookDate = fields.DepositBookDate;
            delegation.UpdatedAt = DateTime.UtcNow;
            // أي كتابة ترفع الرمز — وإلا سحقت كتابة متزامنة قديمة بصمت (TOCTOU).
            delegation.Version++;
            // تصحيح المحامي يُبطل قراري الرئيس السابقين على المحتوى القديم (التوجيه
            // لشعبة والرفض للدائرة الخطأ) — تُعاد المراجعة من جديد على المحتوى الجديد.
            delegation.RedirectedToSectionId = null;
            delegation.RejectReason = null;
            ApplyDelegationAssets(delegation, fresh, request.AssetIds);

            // B3: مواءمة الحجوزات مع اللقطات الجديدة (حذف المباشر أولًا فلا تعارض ذاتي) —
            // تُحجز المستهلَكة من المسبح بعد الحواجب (لغة المسح المشتركة)، لا المطلوبة حرفيًا.
            await _reservations.DeleteByDelegationAsync(delegation.Id, token);
            var blocked = ComputeBlockedIds(fresh, excludeDelegationId: delegation.Id);
            var pool = fresh.Assets.OrderBy(a => a.Id).Where(a => !blocked.Contains(a.Id)).ToList();
            var ownConsumed = ConsumeSnapshots(pool, delegation.Assets.OrderBy(s => s.Id), new HashSet<int>(blocked));
            foreach (var assetId in ownConsumed)
                await _reservations.AddAsync(new DelegationAssetReservation
                {
                    DelegationId = delegation.Id,
                    SourceDocumentId = source.Id,
                    AssetId = assetId,
                }, token);

            _delegations.Update(delegation);

            await _alertOutbox.EnqueueSystemAsync(
                new CreateSystemHeadAlertRequest(
                    HeadAlertKindCatalog.DelegationPendingApproval,
                    pendingApprovalMessage,
                    HeadAlertAudience.SingleHead,
                    alertBranch,
                    userId,
                    DocumentId: source.Id,
                    DelegationId: delegation.Id,
                    RecipientUserId: ownerId),
                new HeadAlertSupersedePlan(
                    delegation.Id,
                    AppealId: null,
                    HeadAlertKindCatalog.DelegationKinds,
                    "delegation_updated"),
                occurredAt: DateTime.UtcNow,
                ct: token);
            try
            {
                await _uow.SaveChangesAsync(token);
            }
            catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
            {
                throw new ArgumentException(BlockedAssetsMessage(fresh, request.AssetIds), ex);
            }
            await _audit.LogAsync(actorName, "update_delegation",
                source.Id, source.DocumentType,
                $"عدّل إنابة (رقم {delegation.Id}) إلى {court}", token);
        }, ct);

        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        return ToDto(delegation, source, currentYear);
    }

    public async Task<bool> DeleteAsync(int delegationId, int userId, string? actorName, CancellationToken ct = default)
    {
        var delegation = await _delegations.GetByIdWithDetailsAsync(delegationId, ct)
            ?? throw new KeyNotFoundException("الإنابة غير موجودة");

        EnsurePendingByOwner(delegation, userId);
        var source = delegation.SourceDocument;

        await _tx.RunAsync(async token =>
        {
            await _alerts.ArchiveByDelegationAsync(
                delegation.Id,
                HeadAlertKindCatalog.DelegationKinds,
                "delegation_deleted",
                token);
            _delegations.Remove(delegation);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "delete_delegation",
                source.Id, source.DocumentType,
                $"حذف إنابة (رقم {delegation.Id}) إلى {delegation.DelegatedCourt}", token);
        }, ct);

        return true;
    }

    public async Task SyncTargetsTransferredAsync(IReadOnlyCollection<int> targetDocumentIds,
        int newLawyerId, string? actorName, int? actorUserId = null, CancellationToken ct = default)
    {
        if (targetDocumentIds.Count == 0)
            return;

        var delegations = await _delegations.ListByTargetDocumentIdsAsync(targetDocumentIds, ct);
        if (delegations.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var changed = new List<DocumentDelegation>();
        foreach (var delegation in delegations)
        {
            // الإنابة المنفذة سجل تاريخي مكتمل: نقل ملكية ملفها لاحقًا لا يعيد
            // كتابة «من أتمّها». يُترك الحقل كما هو ويُعرض المالك التاريخي.
            if (delegation.Status == DelegationStatusCatalog.Executed)
                continue;
            // النقل غير المتسلسل قد يكرر نفس الإنابة عبر معرف ملف واحد فقط عمليًا،
            // لكن الحارس يمنع كتابة/تدقيقًا بلا تغيير ويبقي العملية idempotent.
            if (delegation.AssignedLawyerId == newLawyerId)
                continue;
            delegation.AssignedLawyerId = newLawyerId;
            delegation.UpdatedAt = now;
            // رمز التزامن: التسجيل/الإتمام المتزامن مع النقل يُحسم بـ409 بدل كتابة عمياء.
            delegation.Version++;
            _delegations.Update(delegation);
            changed.Add(delegation);
        }

        if (changed.Count == 0)
            return;

        foreach (var delegation in changed)
        {
            var target = delegation.TargetDocument;
            // الأرشفة والإشعار وحدة واحدة لا تُجزَّأ: بلا فاعل/فرع معروف لا يُنشأ تنبيه
            // للمالك الجديد، فلا تُؤرشف أيضًا تنبيهات المحامي السابق كي لا يبقى الملف
            // بلا أي تنبيه (الفحص قبل الأرشفة — الترتيب المعكوس كان يؤرشف بلا إشعار).
            if (actorUserId is null || target?.BranchId is null)
                continue;

            // تنبيه المحامي السابق (إسناد/نقل سابق) يتبع الملف: يُؤرشف مع النقل،
            // ولا تُمَس تنبيهات الإنابة الأخرى (مثل «بانتظار الإتمام» الخاص بالرئيس).
            await _alerts.ArchiveByDelegationAsync(
                delegation.Id, DelegationTransferAlertKinds, "delegation_transferred", ct);

            // تنبيه المالك الجديد: الجمهور «منشئ الملف» يُحلّ إلى مالك الملف المناب
            // الحالي وقت معالجة الصادر (بعد اكتمال النقل).
            await _alertOutbox.EnqueueSystemAsync(
                new CreateSystemHeadAlertRequest(
                    HeadAlertKindCatalog.DelegationTransferredToLawyer,
                    HeadAlertMessages.DelegationTransferredToLawyer(new(SourceLabel(delegation.SourceDocument))),
                    HeadAlertAudience.DocumentCreator,
                    target.BranchId.Value,
                    actorUserId.Value,
                    DocumentId: target.Id,
                    DelegationId: delegation.Id),
                supersedePlan: null,
                occurredAt: DateTime.UtcNow,
                ct: ct);
        }

        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(actorName, "sync_delegation_assigned_lawyer",
            details: $"حُدِّث المحامي المختص لـ{changed.Count} إنابة بعد نقل ملكية الملف المناب", ct: ct);
    }

    /// <summary>
    /// أنواع تنبيهات الإنابة التي تتبع الملكية عند النقل: إسناد أول محامٍ وتنبيه نقل
    /// سابق. تُؤرشف وحدها دون بقية عائلة الإنابة (تنبيهات الرئيس/الإتمام) حتى لا يُفقد تنبيه قائم.
    /// </summary>
    private static readonly IReadOnlySet<string> DelegationTransferAlertKinds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            HeadAlertKindCatalog.DelegationAssignedToLawyer,
            HeadAlertKindCatalog.DelegationTransferredToLawyer,
        };

    public async Task<List<DelegationDto>> ListForDocumentAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct)
            ?? throw new ArgumentException("الملف غير موجود");

        // الملف المناب: يُعرض «تشعباته» من إنابته (المصدر)، والملف المنيب: إناباته الصادرة.
        var delegations = doc.SourceDelegationId is not null
            ? await ListOfTargetAsync(doc.Id, ct)
            : await _delegations.ListBySourceAsync(doc.Id, ct);

        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        // ملاحظة الإضافات المحلية تُقرأ من صف الإنابة نفسه (سجل دائم) لا من تنبيهات المرآة
        // النشطة — فأرشفة التنبيه أو تنظيفه لا تمحو أثر الإضافة من «تشعبات الملف».
        return delegations
            .Select(d => ToDto(d, d.SourceDocument, currentYear))
            .ToList();
    }

    public async Task<List<DelegationDto>> ListPendingForHeadAsync(
        int branchId, int? ownerSectionId = null, bool rejectedOnly = false, CancellationToken ct = default)
    {
        var delegations = await _delegations.ListPendingByBranchAsync(branchId, ownerSectionId, rejectedOnly, ct);
        return delegations.Select(d => ToDto(d, d.SourceDocument, ServerClock.CurrentYear(_clock, _timeZone))).ToList();
    }

    public Task<int> CountPendingForHeadAsync(
        int branchId, int? ownerSectionId = null, bool rejectedOnly = false, CancellationToken ct = default)
        => _delegations.CountPendingByBranchAsync(branchId, ownerSectionId, rejectedOnly, ct);
}
