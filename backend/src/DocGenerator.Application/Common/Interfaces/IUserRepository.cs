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

    /// <summary>
    /// مندوبو الجهات (EntityManager) الذين نطاقهم قيد معين عبر PortalEntryId — لترحيل نطاقهم
    /// إلى القيد الناجي عند طيّ قيد في قيد (متتبَّعة للتعديل). لا يشمل مندوبي مستوى الهوية الأم.
    /// </summary>
    Task<List<User>> ListEntityManagersByEntryIdAsync(int entryId, CancellationToken ct = default);

    /// <summary>
    /// مرشحو استلام مراسلة: حسابات نشطة بدور محامٍ/رئيس قسم/مندوب جهة، بلا المستثنى،
    /// مع الفرع ونطاق البوابة لعرض المحافظة — يُصفَّى بالاسم ويُسقَف بالحد الممرر.
    /// </summary>
    Task<List<User>> SearchCorrespondenceTargetsAsync(int excludeUserId, string? q, int limit, CancellationToken ct = default);

    /// <summary>
    /// محامو الملف المؤهلون كمستلمين لمراسلة مربوطة به لمندوب الجهة: مالك الملف
    /// (CreatedById) + متابعو الاستئناف عليه + المحامون المسندون في إناباته (مصدرًا/منابًّا) —
    /// نفس دلالة FollowsDocumentAsync المستخدمة في بوابة الكتابة. نشطون بدور محامٍ، بلا
    /// المستثنى، يُصفَّون بالاسم ويُسقَفون بالحد الممرر.
    /// </summary>
    Task<List<User>> SearchDocumentLawyerTargetsAsync(
        int documentId, int? ownerUserId, int excludeUserId, string? q, int limit,
        CancellationToken ct = default);

    /// <summary>
    /// مناديب الجهة المؤهلون كمستلمين لمراسلة مربوطة بملف لمحامي/رئيس القسم: نشطون،
    /// مرتبطون بقيد من قيود الملف النهائية النشطة أو بهوية أم من هوياتها (عكس النطاق)،
    /// بلا المستثنى، يُصفَّون بالاسم ويُسقَفون بالحد الممرر.
    /// </summary>
    Task<List<User>> SearchScopeDelegateTargetsAsync(
        IReadOnlyCollection<int> entryIds, IReadOnlyCollection<int> groupIds,
        int excludeUserId, string? q, int limit, CancellationToken ct = default);

    /// <summary>هل المستخدم المحدد (نشط بدور محامٍ) هو مالك الملف أو أحد متابعيه؟</summary>
    Task<bool> IsDocumentLawyerTargetAsync(
        int documentId, int? ownerUserId, int userId, CancellationToken ct = default);

    /// <summary>هل المستخدم المحدد (نشط بدور مندوب جهة) ضمن نطاق القيود/الهويات المعطاة؟</summary>
    Task<bool> IsScopeDelegateTargetAsync(
        IReadOnlyCollection<int> entryIds, IReadOnlyCollection<int> groupIds,
        int userId, CancellationToken ct = default);
}
