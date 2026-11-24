using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// مساعدات كتابة حسابات المحامين المشتركة بين الخدمة وأوامر `P4-01bg` — تطبيع
/// بيانات الدخول ورسائل التفرد وبناء الـDTO — بلا فتح معاملة.
/// </summary>
internal interface IUserManagementLawyerWriteRules
{
    string NormalizeUsername(string username);
    void ValidateUsername(string username);
    string NormalizePassword(string? password, string? username = null);
    string DuplicateUsernameMessage(int? branchId);
    LawyerListItemDto ToLawyerDto(User user);
}
