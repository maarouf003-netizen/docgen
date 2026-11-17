using System.Security.Claims;
using DocGenerator.Application.Common;

namespace DocGenerator.Api.Authorization;

/// <summary>
/// سؤال قدرة دورّية واحدة من الكتالوج المركزي. الواجهة تفصل المتحكمات المُرحَّلة عن
/// <see cref="RolePermissions"/> نصيًا (حارس <c>AuthorizationMigrationGuardTests</c>)
/// وتبقي القاعدة في مكان واحد.
/// </summary>
public interface IUserCapabilities
{
    /// <summary>
    /// هل يملك الفاعل القدرة؟ دور غير معروف في الرمز يرمي
    /// <see cref="UnauthorizedAccessException"/> (يُترجم 403) بدل التدهور إلى منح/منع صامت.
    /// </summary>
    bool Has(ClaimsPrincipal user, UserCapability capability);
}

/// <inheritdoc/>
public sealed class UserCapabilities : IUserCapabilities
{
    public bool Has(ClaimsPrincipal user, UserCapability capability)
    {
        ArgumentNullException.ThrowIfNull(user);

        var role = user.GetRoleEnum();
        return capability switch
        {
            UserCapability.HasFullAccess => RolePermissions.HasFullAccess(role),
            UserCapability.IsHeadOrSubHead => RolePermissions.IsHeadOrSubHead(role),
            UserCapability.IsLawyer => RolePermissions.IsLawyer(role),
            UserCapability.IsHead => RolePermissions.IsHead(role),
            UserCapability.IsSubHead => RolePermissions.IsSubHead(role),
            UserCapability.IsAdmin => RolePermissions.IsAdmin(role),
            UserCapability.IsEntityManager => RolePermissions.IsEntityManager(role),
            UserCapability.CanViewPrintCounter => RolePermissions.CanViewPrintCounter(role),
            UserCapability.CanSearchByLawyer => RolePermissions.CanSearchByLawyer(role),
            UserCapability.CanSeeAdministrativeBranch => RolePermissions.CanSeeAdministrativeBranch(role),
            UserCapability.CanSeeAssignedLawyer => RolePermissions.CanSeeAssignedLawyer(role),
            UserCapability.CanViewDeletedDocuments => RolePermissions.CanViewDeletedDocuments(role),
            UserCapability.CanViewStrikeOffReminders => RolePermissions.CanViewStrikeOffReminders(role),
            UserCapability.CanEditDocuments => RolePermissions.CanEditDocuments(role),
            UserCapability.CanChangeDocumentStatus => RolePermissions.CanChangeDocumentStatus(role),
            UserCapability.CanDeleteDocuments => RolePermissions.CanDeleteDocuments(role),
            UserCapability.CanManageExecutionActions => RolePermissions.CanManageExecutionActions(role),
            UserCapability.CanRotate => RolePermissions.CanRotate(role),
            UserCapability.CanTransferDocuments => RolePermissions.CanTransferDocuments(role),
            UserCapability.CanManageEntityRegistry => RolePermissions.CanManageEntityRegistry(role),
            UserCapability.CanMergeEntities => RolePermissions.CanMergeEntities(role),
            UserCapability.CanManageDelegates => RolePermissions.CanManageDelegates(role),
            UserCapability.CanManageBranches => RolePermissions.CanManageBranches(role),
            UserCapability.CanViewAppSuggestions => RolePermissions.CanViewAppSuggestions(role),
            UserCapability.CanSuggestApp => RolePermissions.CanSuggestApp(role),
            UserCapability.CanManagePersonalReminders => RolePermissions.CanManagePersonalReminders(role),
            UserCapability.CanCreateReviewLetters => RolePermissions.CanCreateReviewLetters(role),
            UserCapability.CanReplyReviewLetters => RolePermissions.CanReplyReviewLetters(role),
            UserCapability.CanCreateCorrespondences => RolePermissions.CanCreateCorrespondences(role),
            UserCapability.CanManageDelegations => RolePermissions.CanManageDelegations(role),
            UserCapability.CanApproveDelegations => RolePermissions.CanApproveDelegations(role),
            UserCapability.CanManageAppeals => RolePermissions.CanManageAppeals(role),
            UserCapability.CanAssignAppeals => RolePermissions.CanAssignAppeals(role),
            UserCapability.CanManageExecutionCircuits => RolePermissions.CanManageExecutionCircuits(role),
            UserCapability.CanManageUsers => RolePermissions.CanManageUsers(role),
            UserCapability.CanManageBranchLawyers => RolePermissions.CanManageBranchLawyers(role),
            // إغلاق الفشل: قدرة جديدة بلا ربط صريح لا تُمنح افتراضيًا.
            _ => throw new ArgumentOutOfRangeException(
                nameof(capability), capability, "قدرة غير مربوطة بالكتالوج المركزي"),
        };
    }
}
