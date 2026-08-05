using System.Security.Claims;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Api;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
        return int.TryParse(sub, out var id) ? id : 0;
    }

    public static string GetRole(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Role)?.ToLowerInvariant() ?? string.Empty;

    /// <summary>
    /// يحوّل دور التوكن النصي (أحرف صغيرة) إلى التعداد المقابل؛
    /// القيمة الافتراضية Lawyer لمنع التصريح عن غير قصد عند قيمة غير معروفة.
    /// </summary>
    public static UserRole GetRoleEnum(this ClaimsPrincipal user)
        => Enum.TryParse<UserRole>(user.GetRole(), ignoreCase: true, out var role)
            ? role
            : UserRole.Lawyer;

    public static int? GetBranchId(this ClaimsPrincipal user)
    {
        var b = user.FindFirstValue("branch_id");
        return int.TryParse(b, out var id) ? id : null;
    }
}
