using DocGenerator.Application.DTOs;

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
    /// هل المستخدم طرفٌ في الإنابة؟ (محامي الملف المنيب، محامي الملف المناب، المحامي المختص،
    /// أو أحد محتلفيها بالنقل) — لترخيص قراءة تنبيهات الإنابة المدموجة عبر by-delegation.
    /// </summary>
    Task<bool> IsPartyAsync(int delegationId, int userId, CancellationToken ct = default);

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
}
