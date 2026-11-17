namespace DocGenerator.Api.Authorization;

/// <summary>
/// أسماء القدرات الدورّية التي تستهلكها المتحكمات المُرحَّلة إلى سياسات `P2-01` — **مطابقة حرفيًا** لأسماء
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
    IsLawyer,
    IsHead,
    IsSubHead,
    IsAdmin,
    IsEntityManager,
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
    CanManageEntityRegistry,
    CanMergeEntities,
    CanManageDelegates,
    CanManageBranches,
    CanViewAppSuggestions,
    CanSuggestApp,
    CanManagePersonalReminders,
    CanCreateReviewLetters,
    CanReplyReviewLetters,
    CanCreateCorrespondences,
    CanManageDelegations,
    CanApproveDelegations,
    CanManageAppeals,
    CanAssignAppeals,
    CanManageExecutionCircuits,
    CanManageUsers,
    CanManageBranchLawyers,
}
