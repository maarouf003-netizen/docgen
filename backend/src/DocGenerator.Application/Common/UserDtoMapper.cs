using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common;

/// <summary>
/// إسقاط المستخدم إلى عقد العرض الموحّد <see cref="UserDto"/> — مشترك بين قراءة
/// «من أنا» والدخول الناجح، فلا يُكرَّر التطابق الحرفي في موضعين.
/// </summary>
internal static class UserDtoMapper
{
    public static UserDto ToDto(User user) => new(
        user.Id,
        user.Username,
        user.FullName,
        user.Role.ToString().ToLowerInvariant(),
        user.BranchId,
        user.Branch?.Name,
        user.SectionId,
        user.Section?.Name);
}
