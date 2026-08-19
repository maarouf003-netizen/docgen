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

    /// <summary>طلبات الإنابة المعلّقة لفرع رئيس القسم — نافذة «طلبات الإنابة والاستئنافات والمطالعات».</summary>
    Task<List<DelegationDto>> ListPendingForHeadAsync(int branchId, CancellationToken ct = default);

    /// <summary>
    /// اعتماد الإنابة: يختار رئيس القسم المحامي المختص (وفي الإنابة الخارجية الفرع وكتاب الإرسال)،
    /// ويُحدَّث بيانات الإرسال إن وُجدت، ويُنشأ الملف المناب تلقائيًا، ويُشعر المحامي المختص
    /// بتنبييه. رئيس القسم (فرعه) فقط (يُقيد الدور في المتحكم، وهنا يُتحقق الفرع والمحامي).
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
