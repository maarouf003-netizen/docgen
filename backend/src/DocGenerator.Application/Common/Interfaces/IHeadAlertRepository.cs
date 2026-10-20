using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// استعلامات تنبيهات الرؤساء على مستوى قاعدة البيانات:
/// قوائم المستلم، عدّاد غير المقروء، واستعلامات الاستهداف.
/// </summary>
public interface IHeadAlertRepository : IRepository<HeadAlert>
{
    /// <summary>تنبيهات المحامي المستلم، الأحدث أولاً، مع بيانات منشئ التنبيه والملف.</summary>
    Task<List<HeadAlert>> ListForRecipientAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// تنبيهات مستلم في فرع (§8: قراءة بالمستلم لا بالفرع) — لرؤساء النطاق.
    /// </summary>
    Task<List<HeadAlert>> ListByRecipientInBranchAsync(int userId, int branchId, CancellationToken ct = default);

    /// <summary>
    /// ما أصدره الرئيس في فرعه (صندوق الصادر — تبويب «أرسلتها»): لا يدخل نطاق
    /// المستلمين، فلا يؤثر في العدّادات ولا يُرحَّل عند التعاقب.
    /// </summary>
    Task<List<HeadAlert>> ListSentAsync(int userId, int branchId, CancellationToken ct = default);

    /// <summary>
    /// كل التنبيهات عبر الفروع لقائمة الإشراف (المدير/المشرف — قرار المالك 2026-10-16):
    /// الأحدث أولًا بسقف 500. إسقاط عرضي — العدّادات تُحسَب في قاعدة البيانات
    /// دون تحميل صفوف المستلمين، وحالة القراءة فارغة (مستلم-محددة أصلًا).
    /// </summary>
    Task<List<HeadAlertDto>> ListForOversightAsync(CancellationToken ct = default);

    /// <summary>عدد تنبيهات المحامي غير المقروءة.</summary>
    Task<int> CountUnreadAsync(int userId, CancellationToken ct = default);

    /// <summary>تنبيه مع مستلميه ومنشئه وملفه (لتعليم القراءة أو العرض).</summary>
    Task<HeadAlert?> GetByIdWithRecipientsAsync(int id, CancellationToken ct = default);

    /// <summary>محامو الفرع المفعلون (مستلمو التعميم).</summary>
    Task<List<User>> ListActiveLawyersAsync(int branchId, CancellationToken ct = default);

    /// <summary>رؤساء أقسام الفرع المفعلون (مستلمو تنبيهات النظام المرحلية، كمراحل الإنابة).</summary>
    Task<List<User>> ListActiveHeadsAsync(int branchId, CancellationToken ct = default);

    /// <summary>
    /// محامو كل الفروع المفعلة مجمّعين حسب الفرع — للبث العام لكل المحامين
    /// عند تغييرٍ على الجهات (تعديل تسمية/دمج/حلول). يعيد زوج (BranchId، محامو الفرع النشطون).
    /// </summary>
    Task<List<(int BranchId, List<User> Lawyers)>> ListAllActiveLawyersGroupedByBranchAsync(CancellationToken ct = default);

    /// <summary>
    /// رؤساء أقسام كل الفروع المفعلة مجمّعين حسب الفرع — للبث الخاص برؤساء الأقسام
    /// عند تغييرٍ على الجهات. يعيد زوج (BranchId، رؤساء الفرع النشطون).
    /// </summary>
    Task<List<(int BranchId, List<User> Heads)>> ListAllActiveHeadsGroupedByBranchAsync(CancellationToken ct = default);

    /// <summary>كل تنبيهات الإنابة المحددة (لتصفيتها عند اعتمادها/إتمامها أو حذفها).</summary>
    Task<List<HeadAlert>> ListByDelegationAsync(int delegationId, CancellationToken ct = default);

    /// <summary>أحدث تنبيه للإنابة (لتحديث رسالته عند تعديل الإنابة).</summary>
    Task<HeadAlert?> FindLatestByDelegationAsync(int delegationId, CancellationToken ct = default);

    /// <summary>
    /// أحدث تنبيه مرآة (منيب ↔ مناب) لمستلمٍ بعينه على إنابةٍ بعينها — تدمج التنبيهات
    /// المتعاقبة لنفس المستلم في تنبيه واحد بآخر تغيّر (لا يُستخدم تحديث الإنابة العام
    /// الذي يلتقط أحدث تنبيهٍ للإنابة أيًّا كان جمهوره).
    /// </summary>
    Task<HeadAlert?> FindLatestByDelegationAndRecipientAsync(
        int delegationId, int recipientLawyerId, CancellationToken ct = default);

    /// <summary>تنبيهات الإنابة مع مستلميها ومنشئيها (لعرض «معلومات الملف المنيب»/«تشعبات الملف»).</summary>
    Task<List<HeadAlert>> ListByDelegationWithRecipientsAsync(int delegationId, CancellationToken ct = default);

    /// <summary>كل تنبيهات الاستئناف المحدد (لتصفية تنبيه «اختيار المحامي» عند الإسناد).</summary>
    Task<List<HeadAlert>> ListByAppealAsync(int appealId, CancellationToken ct = default);

    /// <summary>
    /// حذف انتقائي (§6.2/§6.5): صفوف استلام مستلم واحد من تنبيهات استئناف، مع
    /// حذف التنبيهات الميتة — بلا مساس بباقي المستلمين، وبلا حفظ (ضمن معاملة المتصل).
    /// </summary>
    Task<int> RemoveAppealRecipientAsync(int appealId, int userId, CancellationToken ct = default);

    /// <summary>
    /// تنبيه معلق (إعادة قيد) بعينه لمحامٍ (مدمج لكل محامٍ × دائرة برسالة ثابتة) — للإحالة المدمجة.
    /// </summary>
    Task<HeadAlert?> FindPendingAlertAsync(int lawyerId, string message, CancellationToken ct = default);

    /// <summary>كل تنبيهات إعادة القيد لمحامٍ (بادئة ثابتة) — للتنظيف التلقائي عند الصفر.</summary>
    Task<List<HeadAlert>> ListPendingAlertsForLawyerAsync(int lawyerId, string prefix, CancellationToken ct = default);

    /// <summary>
    /// تنبيهات الرسالة نفسها مقيدةً بالفرع في SQL (S4) — لترحيل التسمية؛
    /// الوحدانية الاسمية داخل الفرع فقط، فلا يُسحب أي تنبيه من فرع آخر أصلًا.
    /// </summary>
    Task<List<HeadAlert>> FindByMessageAndBranchAsync(string message, int branchId, CancellationToken ct = default);

    /// <summary>
    /// أحدث تنبيه تعديل غير مقروء مرتبط بجهة عامة معينة لمستلم محدد — تستخدمه
    /// الاقتراحات المتلاحقة لتعديل الجهة نفسها قبل الاعتماد لدمجها في تنبيه واحد
    /// بآخر تعديل بدل تراكم تنبيهات متعددة.
    /// </summary>
    Task<HeadAlert?> FindLatestPendingByEntityAsync(
        int publicEntityId, int recipientUserId, CancellationToken ct = default);

    /// <summary>
    /// أحدث تنبيه تعديل غير مقروء مرتبط بكتاب مطالعة محدد لمستلم محدد — يُحدَّث
    /// نُصّه وزمنه لدمج الردود المتتالية في تنبيه واحد بدل تراكم التنبيهات.
    /// </summary>
    Task<HeadAlert?> FindLatestUnseenByReviewLetterAsync(
        int reviewLetterId, int recipientUserId, CancellationToken ct = default);

    // ── مسارات النوع الدلالي (الخطة §6.4/§8.1) ──

    /// <summary>
    /// التنبيه النشط بنفس مفتاح منع التكرار — بحث داخل معاملة المعالج. الفهرس الفريد
    /// الجزئي هو الحكم النهائي للPrevented التكرار.
    /// </summary>
    Task<HeadAlert?> FindLatestActiveByDedupeKeyAsync(string dedupeKey, CancellationToken ct = default);

    /// <summary>
    /// التنبيهات النشطة لدورة (إنابة/استئناف) ضمن مجموعة أنواع صريحة — أساس الأرشفة
    /// المقيدة بالنوع (لا «أرشفة الكل»).
    /// </summary>
    Task<List<HeadAlert>> ListActiveByScopeAndKindsAsync(
        int scopeId, HeadAlertScopeKind scopeKind, IReadOnlySet<string> kinds, CancellationToken ct = default);

    /// <summary>مستلمو تنبيه بعينه مع حالة قراءتهم (نافذة «المستلمون»).</summary>
    Task<List<HeadAlertRecipient>> ListRecipientsAsync(int alertId, CancellationToken ct = default);

    /// <summary>
    /// أرشفة (لا حذف) التنبيهات النشطة لدورة ضمن مجموعة أنواع صريحة، مع استثناء
    /// اختياري للتنبيه البديل. النداء بلا حفظ (ضمن معاملة المتصل).
    /// </summary>
    Task<int> ArchiveByScopeAndKindsAsync(
        int scopeId, HeadAlertScopeKind scopeKind, IReadOnlySet<string> kinds, int? excludeAlertId,
        CancellationToken ct = default);

    /// <summary>
    /// تنبيهات المعلقات النشطة لمحامٍ ضمن نوع واحد — أساس التنظيف اليومي
    /// (<c>circuit_pending_documents</c> فقط؛ لا يمسّ <c>legacy</c>).
    /// </summary>
    Task<List<HeadAlert>> ListActivePendingByLawyerAndKindAsync(
        int lawyerId, string kind, CancellationToken ct = default);

    /// <summary>
    /// تنبيهات المعلقات النشطة لدائرة ضمن نوع واحد — أساس إعادة التسمية: تُحدَّث
    /// التنبيهات المرتبطة بالدائرة بالنص الجديد دون مطابقة النص.
    /// </summary>
    Task<List<HeadAlert>> ListActivePendingByCircuitAndKindAsync(
        int circuitId, string kind, CancellationToken ct = default);
}

/// <summary>نطاق دورة الحياة الذي تُرشَّف ضمنه التنبيهات.</summary>
public enum HeadAlertScopeKind
{
    /// <summary>دورة إنابة (<c>DelegationId</c>).</summary>
    Delegation = 0,

    /// <summary>دورة استئناف (<c>AppealId</c>).</summary>
    Appeal = 1
}