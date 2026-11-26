using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class UserManagementService
{
    private static string NormalizeUsername(string username)
    {
        var normalized = ArabicNameNormalizer.Normalize(UsernameInput.Normalize(username));
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("الاسم الثلاثي مطلوب");
        return normalized;
    }

    private static void ValidateUsername(string username)
    {
        if (username.Length > 50)
            throw new ArgumentException("الاسم الثلاثي أطول من المسموح (50 حرفاً)");
    }

    private static string DuplicateUsernameMessage(int? branchId) => branchId is null
        ? "يوجد مستخدم بنفس الاسم الثلاثي، يرجى اختيار اسم مختلف"
        : "يوجد مستخدم بنفس الاسم الثلاثي في نفس الفرع، يرجى اختيار اسم مختلف";

    /// <summary>M-02: القاعدة المعيارية — ما يُفحَص هو ما يُخزَّن. تُقصّ المسافات
    /// الطرفية قبل التحقق، ويجب تخزين القيمة المقصوصة نفسها في كل مسار.</summary>
    private static string NormalizePassword(string? password, string? username = null)
    {
        var candidate = password?.Trim() ?? string.Empty;
        PasswordPolicy.EnsureValid(candidate, username);
        return candidate;
    }

    /// <summary>
    /// نسخة التحرير (إنشاء/تحديث): الكيان المتتبَّع بلا `Include` للشعبة —
    /// يُحسم الاسم باستعلام صريح عند الحاجة (لا يُفترض تحميله).
    /// </summary>
    private async Task<UserListItemDto> ToUserDtoAsync(User user, CancellationToken ct)
    {
        var sectionName = user.Section?.Name;
        if (sectionName is null && user.SectionId.HasValue)
            sectionName = (await _sections.GetByIdAsync(user.SectionId.Value, ct))?.Name;
        return new UserListItemDto(
            user.Id,
            user.Username,
            user.FullName,
            user.Role.ToString().ToLowerInvariant(),
            user.BranchId,
            user.Branch?.Name,
            user.IsActive,
            user.SectionId,
            sectionName,
            user.Version);
    }
}
