using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// عضوية المنتدى من حقيقة القاعدة (لا من ادعاءات الرمز): الخمسة الداخلية فقط —
/// المندوب والمعطَّل والغائب مرفضون بنيويًا (فوق حارس البوابة وسمات التفويض).
/// رئيس الشعبة بلا شعبة جلسة مكسورة (`null` تعني خللًا لا ملك القسم هنا).
/// نقطة واحدة تستهلكها الخدمة ومعالج حذف الرسالة ومعالج التثبيت — فلا تنحرف الرسائل.
/// </summary>
internal static class ForumMembership
{
    internal static async Task<User> RequireAsync(
        IUserRepository users, int actorUserId, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(actorUserId, ct)
            ?? throw new UnauthorizedAccessException("الحساب غير موجود");
        if (!user.IsActive || user.Role == UserRole.EntityManager)
            throw new UnauthorizedAccessException("لا تملك صلاحية المنتدى");
        if (user.Role == UserRole.SubHead && user.SectionId is null)
            throw new UnauthorizedAccessException("حساب رئيس الشعبة بلا شعبة — أعد الدخول");
        return user;
    }
}
