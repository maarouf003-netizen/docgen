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

    [Theory]
    [InlineData("head", null, true)]
    [InlineData("subhead", "4", true)]
    [InlineData("lawyer", null, false)]
    [InlineData("manager", null, false)]
    [InlineData("admin", null, false)]
    [InlineData("entitymanager", null, false)]
    public void IsHeadOrSubHead_MatchesRoleClaim(string role, string? sectionId, bool expected)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (sectionId is not null)
            claims.Add(new Claim("section_id", sectionId));
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));

        Assert.Equal(expected, user.IsHeadOrSubHead());
    }

    [Theory]
    [InlineData("4", 4)]
    [InlineData(null, null)]
    [InlineData("not-a-number", null)]
    public void GetSectionId_ParsesOrDefaultsNull(string? raw, int? expected)
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.Role, "subhead") };
        if (raw is not null)
            claims.Add(new Claim("section_id", raw));
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));

        Assert.Equal(expected, user.GetSectionId());
    }
}
