using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentDelegationService : IDocumentDelegationService
{
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
}
