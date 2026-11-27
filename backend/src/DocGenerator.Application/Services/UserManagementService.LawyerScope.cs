using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

public sealed partial class UserManagementService
{
    /// <summary>
    /// نطاق رئيس الشعبة على حسابات المحامين (W2/B3): تُدار حسابات شعبة الرئيس فقط،
    /// لأن عزل الشعبة على المستندات (`DocumentAccessHandler`) يصبح بلا معنى إذا استطاع
    /// رئيس شعبة إعادة تعيين كلمة مرور محامي شعبة أخرى ثم الدخول بهويته.
    /// <para>
    /// العلاقة تُشتق من مصدرين فقط: محامٍ أنشأه الرئيس نفسه (قرار §2.10)، أو محامٍ يملك
    /// مستندًا في دائرة تنفيذ تابعة لشعبة الرئيس (نفس اشتقاق `ListLawyersQuery` في الوضع
    /// `mine`). القدامى بلا مُنشئ (`CreatedById == null`) مرئيون في القوائم للتوافق، لكنهم
    /// **خارج نطاق الكتابة** عمدًا: لا مرجع شعبة يثبت انتماءهم، فتوسيع الكتابة لهم يفتح
    /// الاستيلاء على أي حساب قديم في الفرع.
    /// </para>
    /// </summary>
    private async Task<bool> IsLawyerInSectionScopeAsync(
        User lawyer,
        int actorUserId,
        int? actorBranchId,
        int? actorSectionId,
        CancellationToken ct)
    {
        if (lawyer.BranchId != actorBranchId)
            return false;
        if (lawyer.CreatedById == actorUserId)
            return true;
        if (actorSectionId is null)
            return false;

        var circuits = await _circuits.ListAsync(ct);
        var sectionCircuitIds = circuits
            .Where(c => c.BranchId == actorBranchId && c.SectionId == actorSectionId)
            .Select(c => c.Id)
            .ToList();
        if (sectionCircuitIds.Count == 0)
            return false;

        var ownerIds = await _documents.ListOwnerIdsByCircuitIdsAsync(sectionCircuitIds, ct);
        return ownerIds.Contains(lawyer.Id);
    }
}
