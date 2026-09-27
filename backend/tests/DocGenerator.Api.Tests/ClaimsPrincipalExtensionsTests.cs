using System.Security.Claims;
using DocGenerator.Api;

namespace DocGenerator.Api.Tests;

public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetUserId_ValidSub_ReturnsId()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "7") }, authenticationType: "test"));

        Assert.Equal(7, user.GetUserId());
    }

    [Fact]
    public void GetUserId_MissingSub_ThrowsUnauthorizedAccess()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test"));

        // رفض صريح بدل الهوية الصفرية الصامتة التي قد تُخلط بمستخدم حقيقي.
        Assert.Throws<UnauthorizedAccessException>(() => user.GetUserId());
    }

    [Fact]
    public void GetUserId_NonNumericSub_ThrowsUnauthorizedAccess()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "not-a-number") }, authenticationType: "test"));

        Assert.Throws<UnauthorizedAccessException>(() => user.GetUserId());
    }
}
