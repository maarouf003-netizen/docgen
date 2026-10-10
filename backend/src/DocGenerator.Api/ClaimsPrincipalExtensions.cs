using System.Security.Claims;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Api;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// معرّف المستخدم من الرمز — إغلاق صريح للفشل: رمز بلا معرّف أو بمعرّف غير رقمي يرمي
    /// `UnauthorizedAccessException` (يُترجم إلى 403 في `GlobalExceptionHandler`) بدل التدهور
    /// الصامت إلى `0` الذي قد يُخلط بمستخدم حقيقي في الاستعلامات اللاحقة.
    /// عمليًا غير بالغ (وسيط المصادقة يرفض `sub` غير الرقمي قبل المتحكمات)، لكنه حارس حدّي.
    /// </summary>
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
        if (int.TryParse(sub, out var id))
            return id;
        throw new UnauthorizedAccessException("هوية المستخدم في الرمز غير صالحة — سجّل الدخول مجددًا");
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

    /// <summary>
    /// رئيس قسم أو شعبة (قرار §2.21) — للاستخدام في اشتقاق النطاق بدل فحص
    /// `Role == UserRole.Head` المتناثر. التوسيع الفعلي للنطاق يتم مرحليًا
    /// مع الفلترة (§5) لا هنا.
    /// </summary>
    public static bool IsHeadOrSubHead(this ClaimsPrincipal user)
        => user.GetRoleEnum() is UserRole.Head or UserRole.SubHead;

    /// <summary>
    /// شعبة الحساب من الرمز (`section_id`)؛ `null` لغير رئيس الشعبة أو للرمز
    /// القديم قبل تفعيل الشعب (يُعامل كملك القسم).
    /// </summary>
    public static int? GetSectionId(this ClaimsPrincipal user)
    {
        var s = user.FindFirstValue("section_id");
        return int.TryParse(s, out var id) ? id : null;
    }
}
