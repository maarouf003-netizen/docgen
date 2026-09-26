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
    /// يحوّل دور التوكن النصي (أحرف صغيرة) إلى التعداد المقابل — إغلاق صريح
    /// للفشل: رمز بلا دور أو بدور غير معروف يرمي `UnauthorizedAccessException`
    /// (يُترجم إلى 403 في `GlobalExceptionHandler`) بدل التدهور الصامت إلى دور
    /// افتراضي قد يمنح صلاحيات غير مستحقة.
    /// </summary>
    public static UserRole GetRoleEnum(this ClaimsPrincipal user)
    {
        if (Enum.TryParse<UserRole>(user.GetRole(), ignoreCase: true, out var role))
            return role;
        throw new UnauthorizedAccessException("دور المستخدم في الرمز غير معروف — سجّل الدخول مجددًا");
    }

    public static int? GetBranchId(this ClaimsPrincipal user)
    {
        var b = user.FindFirstValue("branch_id");
        return int.TryParse(b, out var id) ? id : null;
    }
}
