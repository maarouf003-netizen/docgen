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

    /// <summary>
    /// هل يقع حساب المحامي ضمن نطاق شعبة الفاعل؟ (رئيس الشعبة فقط — W2/B3).
    /// </summary>
    Task<bool> IsLawyerInSectionScopeAsync(
        User lawyer, int actorUserId, int? actorBranchId, int? actorSectionId, CancellationToken ct);
}
