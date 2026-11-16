using System.Security.Claims;
using DocGenerator.Application.Common;

namespace DocGenerator.Api.Authorization.Documents;

/// <summary>
/// سؤال قدرة دورّية واحدة من الكتالوج المركزي. الواجهة تفصل المتحكم عن
/// <see cref="RolePermissions"/> نصيًا (حارس <c>DocumentsControllerAuthorizationGuardTests</c>)
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
            // إغلاق الفشل: قدرة جديدة بلا ربط صريح لا تُمنح افتراضيًا.
            _ => throw new ArgumentOutOfRangeException(
                nameof(capability), capability, "قدرة غير مربوطة بالكتالوج المركزي"),
        };
    }
}
