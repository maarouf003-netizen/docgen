using System.Net;
using System.Security.Claims;
using DocGenerator.Api.Controllers;
using DocGenerator.Application.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Tests;

/// <summary>
/// حارسا النقل لرئيس بلا فرع (`TransferAll` وعدّاد المالك): منع صريح بلا نطاق.
/// يُختبران بهوية مطالبات مصنوعة، لأن قيد القاعدة يجعل تجسيد الصف الشاذ عبر EF
/// مستحيلًا. (تغيير سلوكي صفري — الحارسان قائمان؛ النقل للوحدة لاستحالة التكامل.)
/// خدمات المتحكم السبع غير مستخدمة في هذين المسارين (الحارس يسبقها) فتُمرَّر
/// `null!` صراحة مع هذا التوثيق.
/// </summary>
public sealed class TransferBranchGuardTests
{
    private static DocumentsController BranchlessHeadController()
    {
        var controller = new DocumentsController(null!, null!, null!, null!, null!, null!, null!, null!);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "7"),
            new Claim(ClaimTypes.Name, "headless"),
            new Claim(ClaimTypes.Role, "head"),
        }, "test"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };
        return controller;
    }

    [Fact]
    public async Task TransferAll_HeadWithoutBranch_Forbidden()
    {
        // الرئيس يمرّ بفحص الدور (`CanTransferDocuments`) فيبلغ حارس الفرع.
        var result = await BranchlessHeadController()
            .TransferAll(new TransferAllRequest(1, 2), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task CountFilesByOwner_HeadWithoutBranch_Forbidden()
    {
        var result = await BranchlessHeadController()
            .CountFilesByOwner(1, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }
}
