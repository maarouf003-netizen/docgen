using System.Security.Claims;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Api.Authorization.Documents;

/// <summary>نتيجة تقييم وصول إلى مورد — تُترجم في المتحكم إلى 404/403/متابعة.</summary>
public enum DocumentAccessOutcome
{
    Allowed,
    NotFound,
    Forbidden,
}

/// <summary>
/// نطاق قوائم المستندات المشتق من الرمز (قرار §2.21): البديل الوحيد لتكرار
/// <c>HasFullAccess ? null : ...</c> في كل نقطة قائمة.
/// </summary>
/// <param name="VisibleBranchId">`null` = بلا فلترة فرع (وصول كامل).</param>
/// <param name="VisibleUserId">`null` = بلا فلترة مالك (رئيس/مدير/مشرف).</param>
/// <param name="OwnerSectionId">شعبة رئيس الشعبة؛ `null` لغيره.</param>
public sealed record DocumentListScope(
    int? VisibleBranchId,
    int? VisibleUserId,
    int? OwnerSectionId);

/// <summary>
/// واجهة التفويض الموردي لمتحكم المستندات (`P2-01a`): نقطة واحدة تسأل
/// <c>IAuthorizationService</c> عن مورد/عملية، وتشتق نطاق القوائم، وتقرأ القدرات
/// غير الموردية (تعقيم عدّاد الطباعة/أعمدة التصدير).
///
/// <para>
/// الفصل عن الكتالوج المركزي مقصود: القاعدة الدورّية تبقى في
/// <c>Application.Common.RolePermissions</c>، وهذه الواجهة **تسأل** عنها فقط،
/// فلا يظهر في المتحكم أي `Role ==` أو `RolePermissions.*`.
/// </para>
/// </summary>
public interface IDocumentAuthorization
{
    /// <summary>
    /// يحلّ المورد من إسقاط F-01 ثم يقيّم المتطلب: المفقود = <see cref="DocumentAccessOutcome.NotFound"/>
    /// قبل أي تقييم صلاحية (حفظًا لعقد `ADR-0007`)، والموجود خارج النطاق/القدرة = `Forbidden`.
    /// </summary>
    Task<DocumentAccessOutcome> AuthorizeAsync(
        ClaimsPrincipal user, int documentId, DocumentAction action, CancellationToken ct = default);

    /// <summary>تقييم مورد محمَّل مسبقًا (تفاصيل/محذوف) بلا قراءة إضافية.</summary>
    Task<DocumentAccessOutcome> AuthorizeAsync(
        ClaimsPrincipal user, DocumentResponse document, DocumentAction action, CancellationToken ct = default);

    /// <summary>قدرة دورّية غير موردية (عرض/تصدير/تعقيم) — تفويض إلى الكتالوج المركزي.</summary>
    bool Has(ClaimsPrincipal user, UserCapability capability);

    /// <summary>نطاق القوائم؛ `null` = رئيس شعبة بلا شعبة في الرمز (يُرفض 403).</summary>
    DocumentListScope? ResolveListScope(ClaimsPrincipal user);
}
