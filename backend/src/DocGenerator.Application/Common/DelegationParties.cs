using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common;

/// <summary>
/// أطراف الإنابة — قرار نقي (بلا قاعدة بيانات) على كيان مجلوب مسبقًا.
///
/// <para>كان الفحص في خدمة الإنابات (`IDocumentDelegationService`، الطريقة المحذوفة)،
/// أي أن عقد <c>IHeadAlertService.ListByDelegationAsync</c> لم يكن آمنًا بذاته بل بحرس
/// المتحكم (مراجعة PR-4 `ح‑1`). نُقل هنا ليتخذه المستهلك في طبقة الخدمة بلا استدعاء
/// خدمة أخرى وبلا استعلام إضافي.</para>
///
/// <para>الأطراف الثلاثة هي السلوك القائم بلا توسعة: مالك الملف المنيب
/// (<c>SourceDocument.CreatedById</c>)، ومالك الملف المناب (<c>TargetDocument.CreatedById</c>)،
/// والمحامي المختص الحالي (<c>AssignedLawyerId</c>).</para>
/// </summary>
public static class DelegationParties
{
    /// <summary>هل المستخدم طرفٌ في الإنابة المجلوبة (لا يفحص وجودها — يفحصه المستدعي)؟</summary>
    public static bool IsParty(DocumentDelegation delegation, int userId)
    {
        ArgumentNullException.ThrowIfNull(delegation);

        if (delegation.SourceDocument?.CreatedById == userId)
            return true;
        if (delegation.TargetDocument?.CreatedById == userId)
            return true;
        return delegation.AssignedLawyerId == userId;
    }
}
