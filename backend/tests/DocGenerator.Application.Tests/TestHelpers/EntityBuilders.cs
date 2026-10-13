using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// بناة الكيانات المشتركة (R1-4): نسخ حرفية للمساعدات المحلية المتطابقة —
/// بلا أي تغيير دلالي (نفس الحقول، نفس كلمة المرور، نفس غياب التطبيع —
/// أسماء الاختبارات إنجليزية أصلًا فلا تطبيع عربي لازم).
/// مستثنى عمدًا: `NewUser` الخاص بالمنتدى (يدعم `SectionId` وكلمة مرور
/// صورية `"x"` غير صالحة للدخول — آلية مختلفة، يبقى محليًا).
/// </summary>
public static class EntityBuilders
{
    /// <summary>مستخدم جديد بكلمة `123456` (تجزئة حقيقية صالحة للدخول).</summary>
    public static User NewUser(string username, string fullName, UserRole role, int? branchId)
        => new()
        {
            Username = username,
            FullName = fullName,
            Role = role,
            BranchId = branchId,
            PasswordHash = new PasswordHasher().Hash("123456"),
        };
}
