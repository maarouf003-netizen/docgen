using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// استعلامات المستخدمين المنفّذة على مستوى قاعدة البيانات.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken ct = default);

    /// <summary>محامو فرع (أو كل المحامين إن كان الفرع فارغاً) ببيانات الفرع.</summary>
    Task<List<User>> ListLawyersAsync(int? branchId, CancellationToken ct = default);

    /// <summary>كل المستخدمين بكامل البيانات (لإدارة المستخدمين عند المشرف).</summary>
    Task<List<User>> ListAllUsersAsync(CancellationToken ct = default);

    /// <summary>تحقق من تفرّد اسم المستخدم (يستثني مستخدماً معيناً عند التحديث).</summary>
    Task<bool> UsernameExistsAsync(string username, int? excludeUserId, CancellationToken ct = default);
}
