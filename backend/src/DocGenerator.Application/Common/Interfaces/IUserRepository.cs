using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// استعلامات المستخدمين المنفّذة على مستوى قاعدة البيانات.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>كل الحسابات المطابقة للاسم المطبّع (قد يتكرر الاسم عبر فروع مختلفة).</summary>
    Task<List<User>> FindByUsernameAllAsync(string username, CancellationToken ct = default);

    /// <summary>محامو فرع (أو كل المحامين إن كان الفرع فارغاً) ببيانات الفرع.</summary>
    Task<List<User>> ListLawyersAsync(int? branchId, CancellationToken ct = default);

    /// <summary>كل المستخدمين بكامل البيانات (لإدارة المستخدمين عند المشرف).</summary>
    Task<List<User>> ListAllUsersAsync(CancellationToken ct = default);

    /// <summary>
    /// تحقق من تفرّد الاسم الثلاثي ضمن نطاق الفرع (المستخدمون بلا فرع يتفردون فيما بينهم).
    /// branchId يحدد النطاق؛ excludeUserId يستثني مستخدماً معيناً عند التحديث.
    /// </summary>
    Task<bool> UsernameExistsAsync(string username, int? branchId, int? excludeUserId, CancellationToken ct = default);

    /// <summary>حسابات مندوبي الجهات مع نطاقهم (الهوية/القيد) لشاشة إدارة المندوبين.</summary>
    Task<List<User>> ListEntityManagersAsync(CancellationToken ct = default);

    /// <summary>
    /// مندوبو الجهات (EntityManager) الذين نطاقهم (PortalGroupId أو PortalEntryId) يقع ضمن
    /// مجموعة هويات أم معينة — لترحيل/إعادة توجيه نطاقهم عند دمج أو إلغاء جهات (متتبَّعة للتعديل).
    /// </summary>
    Task<List<User>> ListEntityManagersByGroupIdsAsync(IReadOnlyCollection<int> groupIds, CancellationToken ct = default);
}
