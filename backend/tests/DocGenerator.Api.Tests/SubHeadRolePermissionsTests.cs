using DocGenerator.Api.Authorization;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Api.Tests;

/// <summary>
/// مصفوفة صلاحيات رئيس الشعبة (المرحلة 1ج — قرار §2.1/§2.21): يرث الخمس عشرة
/// الخاصة برئيس القسم، ولا يرث المجموعة السلبية أبدًا.
/// </summary>
public sealed class SubHeadRolePermissionsTests
{
    [Theory]
    [InlineData(nameof(RolePermissions.CanViewDeletedDocuments))]
    [InlineData(nameof(RolePermissions.CanViewCounters))]
    [InlineData(nameof(RolePermissions.CanTransferDocuments))]
    [InlineData(nameof(RolePermissions.CanManageBranchLawyers))]
    [InlineData(nameof(RolePermissions.CanCreateAlerts))]
    [InlineData(nameof(RolePermissions.CanApproveDelegations))]
    [InlineData(nameof(RolePermissions.CanAssignAppeals))]
    [InlineData(nameof(RolePermissions.CanCreateCorrespondences))]
    [InlineData(nameof(RolePermissions.CanSeeAssignedLawyer))]
    [InlineData(nameof(RolePermissions.CanSearchByLawyer))]
    [InlineData(nameof(RolePermissions.CanManageEntityRegistry))]
    [InlineData(nameof(RolePermissions.CanManageDelegates))]
    [InlineData(nameof(RolePermissions.CanSuggestApp))]
    [InlineData(nameof(RolePermissions.CanReplyReviewLetters))]
    [InlineData(nameof(RolePermissions.CanManageExecutionCircuits))]
    public void HeadPermissions_AlsoHoldForSubHead(string method)
    {
        var fn = typeof(RolePermissions).GetMethod(method)!;
        Assert.True((bool)fn.Invoke(null, [UserRole.SubHead])!);
        // ورئيس القسم لا يفقد شيئًا (انحدار).
        Assert.True((bool)fn.Invoke(null, [UserRole.Head])!);
    }

    [Theory]
    [InlineData(nameof(RolePermissions.HasFullAccess))]
    [InlineData(nameof(RolePermissions.CanManageUsers))]
    [InlineData(nameof(RolePermissions.CanManageBranches))]
    [InlineData(nameof(RolePermissions.CanSeeAdministrativeBranch))]
    [InlineData(nameof(RolePermissions.IsReadOnlyOnDocuments))]
    public void NegativeSet_NeverHoldsForSubHead(string method)
    {
        var fn = typeof(RolePermissions).GetMethod(method)!;
        Assert.False((bool)fn.Invoke(null, [UserRole.SubHead])!);
    }

    [Theory]
    [InlineData(UserRole.Head, true)]
    [InlineData(UserRole.SubHead, true)]
    [InlineData(UserRole.Lawyer, false)]
    [InlineData(UserRole.Manager, false)]
    [InlineData(UserRole.Admin, false)]
    [InlineData(UserRole.EntityManager, false)]
    public void IsHeadOrSubHead_MatchesDecisionMatrix(UserRole role, bool expected)
        => Assert.Equal(expected, RolePermissions.IsHeadOrSubHead(role));

    [Theory]
    [InlineData(UserRole.Admin, true)]
    [InlineData(UserRole.Manager, true)]
    [InlineData(UserRole.Head, false)]
    [InlineData(UserRole.SubHead, false)]
    [InlineData(UserRole.Lawyer, false)]
    [InlineData(UserRole.EntityManager, false)]
    public void CanManageBranches_ManagerAndAdminOnly(UserRole role, bool expected)
        => Assert.Equal(expected, RolePermissions.CanManageBranches(role));
}
