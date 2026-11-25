using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Users;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري لمعالجات قراءات الهيكل التنظيمي (`P4-01cs`): يحافظ على توقيعات
/// خدمة إدارة المستخدمين القديمة فوق سجل المعالجات — منطق الاختبارات كما هو.
/// </summary>
public static class UserReadQueryExtensions
{
    public static Task<List<LawyerListItemDto>> ListLawyersAsync(
        this UserReadHandlers handlers,
        int? branchId,
        CancellationToken ct = default)
        => handlers.Lawyers.HandleAsync(
            new ListLawyersQuery(branchId, "branch", 0, null, false, true), ct);

    public static Task<List<LawyerListItemDto>> ListLawyersAsync(
        this UserReadHandlers handlers,
        int? branchId,
        string? mode,
        int callerUserId,
        int? callerSectionId,
        bool callerIsHeadOrSubHead,
        bool includeInactive,
        CancellationToken ct = default)
        => handlers.Lawyers.HandleAsync(
            new ListLawyersQuery(
                branchId, mode, callerUserId, callerSectionId, callerIsHeadOrSubHead, includeInactive), ct);

    public static Task<List<UserListItemDto>> ListUsersAsync(
        this UserReadHandlers handlers,
        CancellationToken ct = default)
        => handlers.Users.HandleAsync(new ListUsersQuery(), ct);

    public static Task<List<HeadSuccessionDto>> ListSuccessionAsync(
        this UserReadHandlers handlers,
        int? branchId,
        CancellationToken ct = default)
        => handlers.Succession.HandleAsync(new ListSuccessionQuery(branchId), ct);
}
