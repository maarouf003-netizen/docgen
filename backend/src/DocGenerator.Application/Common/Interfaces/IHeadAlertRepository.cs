using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// استعلامات تنبيهات رئيس القسم على مستوى قاعدة البيانات:
/// قائمة مستلم، قائمة فرع، عدّاد غير المقروء، واستعلامات الاستهداف.
/// </summary>
public interface IHeadAlertRepository : IRepository<HeadAlert>
{
    /// <summary>تنبيهات المحامي المستلم، الأحدث أولاً، مع بيانات منشئ التنبيه والملف.</summary>
    Task<List<HeadAlert>> ListForRecipientAsync(int userId, CancellationToken ct = default);

    /// <summary>تنبيهات فرع معين (عرض رئيس القسم)، الأحدث أولاً.</summary>
    Task<List<HeadAlert>> ListByBranchAsync(int branchId, CancellationToken ct = default);

    /// <summary>عدد تنبيهات المحامي غير المقروءة.</summary>
    Task<int> CountUnreadAsync(int userId, CancellationToken ct = default);

    /// <summary>تنبيه مع مستلميه ومنشئه وملفه (لتعليم القراءة أو العرض).</summary>
    Task<HeadAlert?> GetByIdWithRecipientsAsync(int id, CancellationToken ct = default);

    /// <summary>محامو الفرع المفعلون (مستلمو التعميم).</summary>
    Task<List<User>> ListActiveLawyersAsync(int branchId, CancellationToken ct = default);

    /// <summary>رؤساء أقسام الفرع المفعلون (مستلمو تنبيهات النظام المرحلية، كمراحل الإنابة).</summary>
    Task<List<User>> ListActiveHeadsAsync(int branchId, CancellationToken ct = default);

    /// <summary>كل تنبيهات الإنابة المحددة (لتصفيتها عند اعتمادها/إتمامها أو حذفها).</summary>
    Task<List<HeadAlert>> ListByDelegationAsync(int delegationId, CancellationToken ct = default);

    /// <summary>أحدث تنبيه للإنابة (لتحديث رسالته عند تعديل الإنابة).</summary>
    Task<HeadAlert?> FindLatestByDelegationAsync(int delegationId, CancellationToken ct = default);

    /// <summary>كل تنبيهات الاستئناف المحدد (لتصفية تنبيه «اختيار المحامي» عند الإسناد).</summary>
    Task<List<HeadAlert>> ListByAppealAsync(int appealId, CancellationToken ct = default);

    /// <summary>
    /// أحدث تنبيه ردٍّ غير مقروء لكتاب مطالعة محدد لدى محاميه — لدمج الردود المتتالية
    /// في تنبيه واحد بدل تراكمها (متتبَّع لإمكانية تحديث رسالته).
    /// </summary>
    Task<HeadAlert?> FindLatestUnseenByReviewLetterAsync(
        int reviewLetterId, int recipientUserId, CancellationToken ct = default);
}
