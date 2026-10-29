using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// استعلامات تنبيهات الرؤساء على مستوى قاعدة البيانات:
/// قوائم المستلم، عدّاد غير المقروء، واستعلامات الاستهداف.
/// </summary>
public interface IHeadAlertRepository : IRepository<HeadAlert>
{
    /// <summary>
    /// صفحة ملخصات المستلَم: المحامي بلا قيد فرع (استلام شخصي)، ورئيس القسم/الشعبة
    /// بفرعه (§9.3). الإسقاط في SQL بلا <c>Message</c> كامل وبلا صفوف مستلمين،
    /// والعدد الكلي باستعلام منفصل. ترتيب حتمي <c>LastContentChangedAt DESC, Id DESC</c>.
    /// </summary>
    Task<HeadAlertSummaryPage> ListSummariesForRecipientAsync(
        int userId,
        int? branchId,
        AlertArchiveFilter archive,
        bool unreadOnly,
        int? delegationId,
        int skip,
        int take,
        bool includeCounters,
        CancellationToken ct = default);

    /// <summary>صفحة ملخصات الصادر (رئيس/شعبة بفرعه) — بلا نص كامل، والعدّادات محسوبة قاعديًا.</summary>
    Task<HeadAlertSummaryPage> ListSummariesSentAsync(
        int userId,
        int branchId,
        AlertArchiveFilter archive,
        int skip,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// صفحة ملخصات الإشراف عبر الفروع (المدير/المشرف) — بلا سقف 500، وبلا
    /// <c>Include(Recipients)</c>؛ حالة القراءة <c>null</c> والعدّادات محسوبة قاعديًا.
    /// </summary>
    Task<HeadAlertSummaryPage> ListSummariesForOversightAsync(
        AlertArchiveFilter archive,
        int? delegationId,
        int skip,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// صفحة ملخصات تنبيهات إنابةٍ محددة — نشطة فقط (شريط نشاط البطاقتين)،
    /// وحالة القراءة بحسب المستخدم. النص الكامل غير محمول.
    /// </summary>
    Task<HeadAlertSummaryPage> ListSummariesByDelegationAsync(
        int delegationId,
        int userId,
        int skip,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// ملخّص صفٍّ واحد بالإسقاط نفسه الذي تبنيه القوائم — لجسم <c>201</c> بعد الإنشاء
    /// بلا تحميل <c>Recipients</c> ولا <c>Message</c> كامل، و<b>بلا <c>COUNT</c> إضافي</b>
    /// (العدّادات تُحسب داخل الإسقاط نفسه عند طلبها). <c>null</c> إن لم يوجد التنبيه.
    /// </summary>
    Task<HeadAlertSummaryDto?> GetSummaryAsync(
        int alertId,
        int? recipientUserId,
        bool includeCounters,
        CancellationToken ct = default);

    /// <summary>
    /// تنبيه مع مستلميه ومنشئه وملفه — لتعليم القراءة وعرض التفاصيل حتى استبدالها
    /// بإسقاط التفاصيل الأحادي (ورقة إصلاح PR-4 §6، المرحلة 2).
    /// </summary>
    Task<HeadAlert?> GetByIdWithRecipientsAsync(int id, CancellationToken ct = default);

    /// <summary>عدد تنبيهات المستخدم غير المقروءة (يستبعد المؤرشف).</summary>
    Task<int> CountUnreadAsync(int userId, CancellationToken ct = default);

    /// <summary>مستلمو تنبيه بعينه مع بيانات المستخدم — لقائمة المستلمين.</summary>
    Task<List<HeadAlertRecipient>> ListRecipientsWithUserAsync(int alertId, CancellationToken ct = default);

    /// <summary>
    /// التنبيهات النشطة التي للمستخدم فيها استلام غير مقروء — مع كل مستلميها
    /// (متتبَّعة: التعاقب يحرّك/يدمج صفوف الاستلام داخلها).
    /// </summary>
    Task<List<HeadAlert>> ListActiveUnreadByRecipientWithRecipientsAsync(
        int userId, CancellationToken ct = default);

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

    /// <summary>أحدث تنبيه للإنابة (لتحديث رسالته عند تعديل الإنابة).</summary>
    Task<HeadAlert?> FindLatestByDelegationAsync(int delegationId, CancellationToken ct = default);

    /// <summary>
    /// أحدث تنبيه مرآة (منيب ↔ مناب) لمستلمٍ بعينه على إنابةٍ بعينها — تدمج التنبيهات
    /// المتعاقبة لنفس المستلم في تنبيه واحد بآخر تغيّر (لا يُستخدم تحديث الإنابة العام
    /// الذي يلتقط أحدث تنبيهٍ للإنابة أيًّا كان جمهوره).
    /// </summary>
    Task<HeadAlert?> FindLatestByDelegationAndRecipientAsync(
        int delegationId, int recipientLawyerId, CancellationToken ct = default);

    /// <summary>كل تنبيهات الاستئناف المحدد (لتصفية تنبيه «اختيار المحامي» عند الإسناد).</summary>
    Task<List<HeadAlert>> ListByAppealAsync(int appealId, CancellationToken ct = default);

    /// <summary>
    /// حذف انتقائي (§6.2/§6.5): صفوف استلام مستلم واحد من تنبيهات استئناف، مع
    /// حذف التنبيهات الميتة — بلا مساس بباقي المستلمين، وبلا حفظ (ضمن معاملة المتصل).
    /// </summary>
    Task<int> RemoveAppealRecipientAsync(int appealId, int userId, CancellationToken ct = default);

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
    /// يفحص وجود المراجع التي يستند إليها التنبيه النظامي قبل الإدراج (الخطة §8.1):
    /// يُعاد أول مرجع مفقود، أو <c>null</c> إذا كانت كل المراجع المحددة موجودة.
    /// الفحص للوجود فقط (لا لحالة التفعيل): غياب الصف هو ما يمنع الإدراج، أما
    /// التفعيل فشأن دلالي يعالجه محلّل المستلمين.
    /// </summary>
    Task<HeadAlertReferenceKind?> FindMissingReferenceAsync(
        CreateSystemHeadAlertRequest request, CancellationToken ct = default);

    /// <summary>
    /// تنبيهات المعلقات النشطة لدائرة ضمن نوع واحد — أساس إعادة التسمية: تُحدَّث
    /// التنبيهات المرتبطة بالدائرة بالنص الجديد دون مطابقة النص.
    /// </summary>
    Task<List<HeadAlert>> ListActivePendingByCircuitAndKindAsync(
        int circuitId, string kind, CancellationToken ct = default);

    /// <summary>
    /// التنبيهات النشطة المرتبطة بدائرة أو بملفاتها (متتبَّعة) — تُؤرشف قبل حذف
    /// الدائرة/تطهير ملفاتها حتى لا تبقى إشارات إلى كيانات محذوفة.
    /// </summary>
    Task<List<HeadAlert>> ListActiveByCircuitOrDocumentsAsync(
        int circuitId, IReadOnlyCollection<int> documentIds, CancellationToken ct = default);

    /// <summary>
    /// أرشفة تنبيهات إعادة القيد النشطة للمحامي التي لم تعد مفاتيحها ضمن المعلقات
    /// الحالية (متتبَّعة، بلا حفظ). تُستخدم في خطاف التصفية بعد اكتمال/نقل المعلقات.
    /// </summary>
    Task<int> ArchiveStalePendingByLawyerAsync(
        int lawyerId, IReadOnlySet<string> activeDedupeKeys, DateTime archivedAt, CancellationToken ct = default);
}

/// <summary>نطاق دورة الحياة الذي تُرشَّف ضمنه التنبيهات.</summary>
public enum HeadAlertScopeKind
{
    /// <summary>دورة إنابة (<c>DelegationId</c>).</summary>
    Delegation = 0,

    /// <summary>دورة استئناف (<c>AppealId</c>).</summary>
    Appeal = 1
}