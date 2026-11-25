using System.Net;
using System.Security.Claims;
using DocGenerator.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Tests;

/// <summary>
/// حارس عدّاد طلبات الإنابة لرئيس بلا فرع: رفض صريح بدل نطاق مفتوح. يُختبر بهوية
/// مطالبات مصنوعة، لأن قيد القاعدة يجعل تجسيد الصف الشاذ عبر EF مستحيلًا.
/// (تغيير سلوكي صفري هنا — الحارس قائم؛ النقل للوحدة لاستحالة التكامل.)
/// </summary>
public sealed class DelegationPendingCountBranchGuardTests
{
    [Fact]
    public async Task PendingCount_HeadWithoutBranch_Forbidden()
    {
        // رئيس يمرّ بفحص الدور (`CanApproveDelegations`) فيبلغ حارس الفرع —
        // مسار العدّاد لا يلمس الملف ولا معالجات القراءة، فتُمرَّر `null!` صراحة.
        var controller = new DelegationsController(
            null!, new DocGenerator.Api.Authorization.UserCapabilities(),
            null!, null!, null!,
            null!, null!, null!, null!, null!, null!, null!, null!, null!);
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

        var result = await controller.PendingCount(CancellationToken.None);

        // D11 الموحّد (2026-11-17): العدّاد قراءة ⇒ 403 لا 400، مع رمز العقد الموحّد.
        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var json = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(forbidden.Value));
        Assert.Equal("branch_required", json.RootElement.GetProperty("code").GetString());
        Assert.Contains("دون فرع", json.RootElement.GetProperty("message").GetString());
    }
}
