using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// خدمة الإنابات التنفيذية: تسطير إنابة من محامي الملف المنيب، واعتمادها من رئيس القسم
/// (اختيار المحامي المختص وإنشاء الملف المناب تلقائيًا)، وتسجيلها أصولًا من محامي الفرع
/// المناب، ثم إتمامها ببيع الأموال موضوع الإنابة بالمزاد العلني وإعادة الملف للدائرة المنيبة.
/// </summary>
public interface IDocumentDelegationService
{
    /// <summary>تسطير إنابة جديدة على ملف منيب (المحامي المالك للملف فقط).</summary>
    Task<DelegationDto> CreateAsync(int sourceDocumentId, UpsertDelegationRequest request, int userId, string? actorName, CancellationToken ct = default);

    /// <summary>تعديل إنابة معلّقة (قبل اعتماد رئيس القسم) — محامي الملف المنيب فقط.</summary>
    Task<DelegationDto?> UpdateAsync(int delegationId, UpsertDelegationRequest request, int userId, string? actorName, CancellationToken ct = default);

    /// <summary>حذف إنابة معلّقة (قبل اعتماد رئيس القسم) — محامي الملف المنيب فقط.</summary>
    Task<bool> DeleteAsync(int delegationId, int userId, string? actorName, CancellationToken ct = default);

    /// <summary>إنابات ملف (المنيب: المصدر؛ أو المناب: إنابته) — بطاقة «تشعبات الملف».</summary>
    Task<List<DelegationDto>> ListForDocumentAsync(int documentId, CancellationToken ct = default);

    /// <summary>
    /// طلبات الإنابة المعلّقة لنطاق الرئيس (§7.1): رئيس القسم (دوائر القسم وبلا
    /// دائرة + الخارجية غير الموجَّهة في فرعه)، ورئيس الشعبة (دوائر شعبته +
    /// الخارجية الموجَّهة لشعبته). `rejectedOnly` لفلتر «مرفوض بانتظار التصحيح» (§7.4).
    /// </summary>
    Task<List<DelegationDto>> ListPendingForHeadAsync(int branchId, int? ownerSectionId = null, bool rejectedOnly = false, CancellationToken ct = default);

    /// <summary>
    /// عدد طلبات الإنابة المعلّقة لنطاق الرئيس (§7.1 + §7.4) — نفس نطاق
    /// <see cref="ListPendingForHeadAsync"/> دون تحميل القوائم المرتبطة.
    /// </summary>
    Task<int> CountPendingForHeadAsync(int branchId, int? ownerSectionId = null, bool rejectedOnly = false, CancellationToken ct = default);

    /// <summary>توجيه إنابة خارجية معلّقة لشعبة في الفرع المناب — رئيس قسم الفرع المناب فقط (§7.3).</summary>
    Task<DelegationDto?> RedirectToSectionAsync(int delegationId, RedirectDelegationRequest request, int userId, string? actorName, CancellationToken ct = default);

    /// <summary>التراجع عن توجيه الشعبة قبل الإسناد — رئيس قسم فرع الاعتماد فقط (§7.3).</summary>
    Task<DelegationDto?> RecallRedirectAsync(int delegationId, int userId, string? actorName, CancellationToken ct = default);

    /// <summary>رفض الدائرة الخطأ برسالة تُعيد المحامي للتصحيح (§7.4) — المعتمد الحالي فقط.</summary>
    Task<DelegationDto?> RejectAsync(int delegationId, RejectDelegationRequest request, int userId, string? actorName, CancellationToken ct = default);

    /// <summary>
    /// اعتماد الإنابة (§7.1): الاعتماد لمالك الدائرة المنابة لا المنيب — داخلية
    /// بدائرة ذات شعبة لرئيس شعبته، وبدائرة قسم (أو بلا دائرة انتقاليًا) لرئيس
    /// قسم فرع الدائرة، وخارجية غير موجَّهة لرئيس قسم الفرع المناب، وخارجية
    /// موجَّهة لرئيس الشعبة الموجَّه لها. المحامي المختص من فرع الدائرة المنابة،
    /// والملف المناب بفرع الدائرة ودائرتها (قرار §2.25). يُنشأ الملف المناب
    /// تلقائيًا (وحدانية `SourceDelegationId` تحمي التوأم برسالة ودية).
    /// </summary>
    Task<DelegationDto?> AssignAsync(int delegationId, AssignDelegationRequest request, int userId, int? headBranchId, string? actorName, CancellationToken ct = default);

    /// <summary>
    /// تسجيل الإنابة أصولًا من محامي الفرع المناب: إدخال رقم أساس الإنابة وتاريخ قيدها
    /// (بيانات الملف المناب) فيُصبح الملف المناب مقيدًا. محامي الملف المناب فقط.
    /// </summary>
    Task<DelegationDto?> RegisterAsync(int delegationId, RegisterDelegationRequest request, int userId, string? actorName, CancellationToken ct = default);

    /// <summary>
    /// إتمام الإنابة من محامي الملف المناب: بيع الأموال موضوع الإنابة بالمزاد العلني
    /// (بدل المبيع لكل أصل بالليرة) وتاريخ إعادة الملف للدائرة المنيبة، فيُصبح الملف المناب
    /// «منفذ إنابة» (حالة نهائية تُعامل منفذًا في القوائم والإحصاءات).
    /// </summary>
    Task<DelegationDto?> CompleteAsync(int delegationId, CompleteDelegationRequest request, int userId, string? actorName, CancellationToken ct = default);

    /// <summary>
    /// حارس تعديل الملف المناب («الملف المناب مرآةً للمنيب»): يرفض الفروق القيمية في الحقول
    /// المقفولة، ويرصد الإضافات المحلية المسموحة (وريث جديد/ممثل جديد) تقريرًا للتنبيه العكسي.
    /// حارس قرائي قبل الكتابة — خارج المعاملة كسائر حراس التعديل.
    /// </summary>
    TargetEditReport ValidateTargetEdit(Document doc, DocumentUpsertRequest request);

    /// <summary>
    /// قفل «الحجز والأصل بعد التسطير» على الملف المنيب: تاريخ القاء الحجز على أصلٍ تحجبه
    /// إنابة سارية يُعدَّل فقط ولا يُحذف، وحذف صف الأصل نفسه مرفوض.
    /// حارس قرائي قبل الكتابة — خارج المعاملة كسائر حراس التعديل.
    /// </summary>
    Task ValidateSeizureLockAsync(Document doc, DocumentUpsertRequest request, CancellationToken ct);

    /// <summary>
    /// مزامنة لقطات أصول الإنابات غير المنفذة للملف المنيب بعد تعديل أصوله —
    /// داخل معاملة المستدعي حصرًا (لا تفتح معاملة خاصة).
    /// </summary>
    Task SyncSnapshotsAsync(Document doc, CancellationToken ct);

    /// <summary>
    /// نسخ تغييرات المنيب إلى مناباته المعلقة (داخل معاملة المستدعي حصرًا) وإعادة اشتقاق
    /// الحقول المحسوبة فيها — تعيد إشعارات تُطلق بعد نجاح المعاملة (نقطة خروج واحدة).
    /// </summary>
    Task<List<MirrorNotification>> MirrorSourceEditsAsync(Document doc, CancellationToken ct);

    /// <summary>
    /// إطلاق إشعارات المرآة المجمعة — بعد تثبيت معاملة الحفظ حصرًا
    /// (كل فشل معزول ولا يُفشل الحفظ).
    /// </summary>
    Task FireMirrorNotificationsAsync(IReadOnlyList<MirrorNotification> notifications, CancellationToken ct);

    /// <summary>
    /// إطلاق تنبيه المنيب عند الإضافات المحلية (وريث/ممثل) على الملف المناب معزولًا —
    /// بعد تثبيت معاملة التعديل حصرًا.
    /// </summary>
    Task FireTargetEditReportAsync(Document target, TargetEditReport report, CancellationToken ct);

    /// <summary>
    /// تنبيه تغيّر حالة المنيب إلى مناباته المعلقة (الصيغ T2-T5 بصياغات المستخدم المقفلة؛
    /// المشطوب والجزئيا لا يصلان هنا) — بعد تثبيت معاملة الحالة حصرًا.
    /// </summary>
    Task FireStatusChangeAlertsAsync(Document doc, DelegationStatusAlertKind kind, CancellationToken ct);
}
