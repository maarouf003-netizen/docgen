using System.Reflection;
using System.Security.Claims;
using DocGenerator.Api.Authorization;
using DocGenerator.Api.Authorization.Documents;
using DocGenerator.Application.Common;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Api.Tests;

/// <summary>
/// P2-01a — جسر القدرات بين سياسة المتحكم والكتالوج المركزي: كل قدرة × كل دور
/// تُقارن بنتيجة <see cref="RolePermissions"/> نفسها، فإضافة قدرة إلى الواجهة بلا
/// ربط (أو تغيير دور في الكتالوج) تُفشل الاختبار بدل أن تنحرف السياسة بصمت.
/// </summary>
[Trait("Category", "Unit")]
public sealed class UserCapabilitiesTests
{
    private static readonly UserCapabilities Capabilities = new();

    public static TheoryData<UserCapability, UserRole> Cells
    {
        get
        {
            var data = new TheoryData<UserCapability, UserRole>();
            foreach (var capability in Enum.GetValues<UserCapability>())
            {
                foreach (var role in Enum.GetValues<UserRole>())
                {
                    data.Add(capability, role);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void Capability_DelegatesToCentralCatalog(UserCapability capability, UserRole role)
    {
        var catalogMethod = CatalogMethod(capability);

        var expected = (bool)catalogMethod.Invoke(null, [role])!;
        var actual = Capabilities.Has(Principal(role), capability);

        Assert.True(
            actual == expected,
            $"انحراف عن الكتالوج المركزي: {capability} × {role} — المتوقع {expected} والسياسة أعادت {actual}.");
    }

    [Fact]
    public void EveryCapabilityName_IsACatalogMethod()
    {
        Assert.Equal(
            Enum.GetValues<UserCapability>().Select(capability => capability.ToString()).Order(),
            Enum.GetValues<UserCapability>()
                .Select(CatalogMethod)
                .Select(method => method.Name)
                .Order());
    }

    /// <summary>فشل مغلق: رمز بلا دور لا يُمنح أي قدرة ولا يُسقط إلى دور افتراضي.</summary>
    [Fact]
    public void MissingRoleClaim_FailsClosed()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.Throws<UnauthorizedAccessException>(
            () => Capabilities.Has(anonymous, UserCapability.HasFullAccess));
    }

    /// <summary>فاعل اختبار موحّد: معرّف 7، فرع 3، شعبة 2 (افتراضيًا).</summary>
    internal static ClaimsPrincipal Principal(
        UserRole role, int userId = 7, int? branchId = 3, int? sectionId = 2)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role.ToString().ToLowerInvariant()),
        };

        if (branchId is not null)
        {
            claims.Add(new Claim("branch_id", branchId.Value.ToString()));
        }

        if (sectionId is not null)
        {
            claims.Add(new Claim("section_id", sectionId.Value.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static MethodInfo CatalogMethod(UserCapability capability)
    {
        var method = typeof(RolePermissions).GetMethod(
            capability.ToString(), BindingFlags.Public | BindingFlags.Static);

        Assert.True(
            method is not null,
            $"القدرة {capability} غير موجودة في RolePermissions — أضِفها إلى الكتالوج ومصفوفة P2-02g أولًا.");

        return method!;
    }
}
