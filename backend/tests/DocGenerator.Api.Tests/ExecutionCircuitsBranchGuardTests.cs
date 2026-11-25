using System.Security.Claims;
using DocGenerator.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Tests;

/// <summary>
/// حارس الفرع في متحكم الدوائر (S6.b): غياب claim الفرع فشل تفويض (403)
/// لا طلب مشوَّه (400). يُختبر بهوية مطالبات مصنوعة لأن تسجيل الدخول نفسه
/// يشترط الفرع، فيستحيل تجسيد الصف الشاذ عبر HTTP.
/// </summary>
public sealed class ExecutionCircuitsBranchGuardTests
{
    private static ExecutionCircuitsController ControllerWith(string role)
    {
        // المعالجات غير مبلوغة: الحارس (غياب claim الفرع) يسبق أي استعلام.
        var controller = new ExecutionCircuitsController(
            null!, null!, null!, null!, null!,
            null!, null!, ApiTestClock.Provider, TimeZoneInfo.Utc,
            new DocGenerator.Api.Authorization.UserCapabilities(), null!, null!, null!, null!, null!, null!);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "7"),
            new Claim(ClaimTypes.Name, "branchless"),
            new Claim(ClaimTypes.Role, role),
        }, "test"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };
        return controller;
    }

    [Fact]
    public async Task Mine_HeadWithoutBranch_Forbids()
    {
        var result = await ControllerWith("head").Mine(CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ForLawyer_LawyerWithoutBranch_Forbids()
    {
        var result = await ControllerWith("lawyer").ForLawyer(CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ForDelegation_LawyerWithoutBranch_Forbids()
    {
        var result = await ControllerWith("lawyer").ForDelegation(CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }
}
