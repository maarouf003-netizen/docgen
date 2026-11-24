using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// مساعدات كتابة المستخدمين (إنشاء/تحديث) المشتركة بين الخدمة وأوامر `P4-01bh`:
/// تحقق الدور/الفرع/الشعبة، حارس المشرف، والخلف الإجباري — بلا فتح معاملة.
/// </summary>
internal interface IUserManagementUserWriteRules
{
    string NormalizeUsername(string username);
    void ValidateUsername(string username);
    string NormalizePassword(string? password, string? username = null);
    string DuplicateUsernameMessage(int? branchId);
    UserRole ParseRole(string? role);
    Task GuardManagerAdminBoundaryAsync(int? actorUserId, bool requestedIsAdmin, bool targetIsAdmin, CancellationToken ct);
    Task<int?> ResolveBranchAsync(int? branchId, UserRole role, CancellationToken ct);
    Task<int?> ResolveSectionAsync(UserRole role, int? branchId, int? sectionId, CancellationToken ct);
    Task GuardSingleActiveHeadAsync(UserRole role, int? branchId, int? sectionId, int? excludeUserId, CancellationToken ct);
    Task<User> ResolveSuccessorAsync(User user, int successorId, CancellationToken ct);
    Task ApplySuccessionAsync(User predecessor, User successor, string? actorName, CancellationToken ct);
    Task<UserListItemDto> ToUserDtoAsync(User user, CancellationToken ct);
}
