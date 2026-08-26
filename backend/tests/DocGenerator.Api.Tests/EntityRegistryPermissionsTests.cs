using DocGenerator.Api.Authorization;
using DocGenerator.Domain.Enums;
using Xunit;

namespace DocGenerator.Api.Tests;

/// <summary>مصفوفة صلاحيات سجل الجهات العامة (د3/د4/د10) — المرحلة 1.</summary>
public class EntityRegistryPermissionsTests
{
    [Theory]
    [InlineData(UserRole.Lawyer, false)]
    [InlineData(UserRole.Head, true)]
    [InlineData(UserRole.Manager, true)]
    [InlineData(UserRole.Admin, true)]
    [InlineData(UserRole.EntityManager, false)]
    public void CanManageEntityRegistry_MatchesDecisionD3(UserRole role, bool expected)
        => Assert.Equal(expected, RolePermissions.CanManageEntityRegistry(role));

    [Theory]
    [InlineData(UserRole.Lawyer, false)]
    [InlineData(UserRole.Head, true)]
    [InlineData(UserRole.Manager, true)]
    [InlineData(UserRole.Admin, true)]
    [InlineData(UserRole.EntityManager, false)]
    public void CanManageDelegates_MatchesDecisionD11(UserRole role, bool expected)
        => Assert.Equal(expected, RolePermissions.CanManageDelegates(role));

    [Theory]
    [InlineData(UserRole.Lawyer, false)]
    [InlineData(UserRole.Head, false)]
    [InlineData(UserRole.Manager, false)]
    [InlineData(UserRole.Admin, false)]
    [InlineData(UserRole.EntityManager, true)]
    public void CanUseDelegatePortal_IsEntityManagerOnly(UserRole role, bool expected)
        => Assert.Equal(expected, RolePermissions.CanUseDelegatePortal(role));
}
