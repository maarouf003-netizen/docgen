namespace DocGenerator.Api.Authorization.Documents;

/// <summary>
/// أسماء القدرات الدورّية التي يستهلكها متحكم المستندات — **مطابقة حرفيًا** لأسماء
/// الدوال في <c>DocGenerator.Application.Common.RolePermissions</c>.
///
/// <para>
/// المطابقة مقصودة ومحمية باختبار (<c>UserCapabilitiesTests</c>): يمنع أن تنحرف طبقة
/// السياسة عن الكتالوج المركزي عند إضافة قدرة أو تغيير دور. لا تُضف قدرة هنا قبل
/// إضافتها إلى الكتالوج وإلى مصفوفة <c>RolePermissionsMatrixTests</c>.
/// </para>
/// </summary>
public enum UserCapability
{
    HasFullAccess,
    IsHeadOrSubHead,
    CanViewPrintCounter,
    CanSearchByLawyer,
    CanSeeAdministrativeBranch,
    CanSeeAssignedLawyer,
    CanViewDeletedDocuments,
    CanViewStrikeOffReminders,
    CanEditDocuments,
    CanChangeDocumentStatus,
    CanDeleteDocuments,
    CanManageExecutionActions,
    CanRotate,
    CanTransferDocuments,
}
