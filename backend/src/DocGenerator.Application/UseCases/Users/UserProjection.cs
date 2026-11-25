using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.Users;

/// <summary>
/// إسقاطات الحسابات المشتركة (`P4-01cs`): نقطة واحدة لبناء `LawyerListItemDto`
/// و`UserListItemDto` يستهلكها معالجا القراءة ومساعدات الكتابة معًا، فلا تنحرف
/// الحقول بين مسارات القراءة والإنشاء/التعديل.
/// </summary>
internal static class UserProjection
{
    internal static LawyerListItemDto ToLawyerDto(User user) => new(
        user.Id,
        user.Username,
        user.FullName,
        user.IsActive,
        user.BranchId,
        user.Branch?.Name,
        user.CreatedById,
        user.Version);

    internal static UserListItemDto ToUserDto(User user) => new(
        user.Id,
        user.Username,
        user.FullName,
        user.Role.ToString().ToLowerInvariant(),
        user.BranchId,
        user.Branch?.Name,
        user.IsActive,
        user.SectionId,
        user.Section?.Name,
        user.Version);
}
