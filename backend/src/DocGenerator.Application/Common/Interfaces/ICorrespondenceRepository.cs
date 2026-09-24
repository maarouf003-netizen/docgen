using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// استعلامات المراسلات على مستوى قاعدة البيانات:
/// قوائم الطرف/الفرع (المحافظة)/الوصول الكامل (محافظة إجبارية) مع البحث والترقيم،
/// عدّاد العاجل غير المؤكَّد، وقراءة مراسلة برسائلها وتوثيق مشاهداتها، ومراسلات ملف محدد.
/// </summary>
public interface ICorrespondenceRepository : IRepository<Correspondence>
{
    /// <summary>مراسلات المستخدم كطرف (منشئ أو مستلم معيَّن)، الأحدث تحديثًا أولاً.</summary>
    Task<(List<Correspondence> Items, int TotalCount)> SearchForPartyAsync(
        int userId, string? q, string? importance, int page, int perPage,
        CancellationToken ct = default);

    /// <summary>
    /// مراسلات محافظة رئيس القسم: المرتبطة بفرعه (BranchId) أو العامة من مندوبي
    /// محافظته (BranchId فارغ + Governorate مطابقة).
    /// </summary>
    Task<(List<Correspondence> Items, int TotalCount)> SearchForBranchAsync(
        int branchId, string governorate, string? q, string? importance,
        int page, int perPage, CancellationToken ct = default);

    /// <summary>
    /// مراسلات محافظة منتقاة (مدير/مشرف)؛ فراغ governorate يُنفَّذ بلا عناصر
    /// (الحجب قبل اختيار المحافظة).
    /// </summary>
    Task<(List<Correspondence> Items, int TotalCount)> SearchAllAsync(
        string? governorate, string? q, string? importance, int page, int perPage,
        CancellationToken ct = default);

    /// <summary>المحافظات المميزة (خيارات فلتر المدير/المشرف).</summary>
    Task<List<string>> GetGovernoratesAsync(CancellationToken ct = default);

    /// <summary>مراسلة برسائلها وتوثيق مشاهداتها وأطرافها وملفها وفرعها.</summary>
    Task<Correspondence?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);

    /// <summary>مراسلات ملف محدد (بطاقة تفاصيل الملف)، الأحدث أولاً.</summary>
    Task<List<Correspondence>> ListByDocumentAsync(int documentId, CancellationToken ct = default);

    /// <summary>هل رقم المراسلة مستخدم سابقًا؟ (ضمان تفرّد التوليد العشوائي).</summary>
    Task<bool> NumberExistsAsync(string correspondenceNumber, CancellationToken ct = default);

    /// <summary>
    /// عدد مراسلات المستخدم العاجلة (كمستلم معيَّن) التي لم يؤكد مشاهدتها بعد —
    /// عدّاد الجرس. المنشئ مستثنى عمدًا: الجرس تنبيه المستلم لا تذكير الكاتب.
    /// </summary>
    Task<int> CountUrgentUnseenForTargetAsync(int userId, CancellationToken ct = default);

    /// <summary>توثيق مشاهدة مستخدم لمراسلة — قراءة احتياطية عند تعارض الإدراج المتزامن.</summary>
    Task<CorrespondenceReceipt?> FindReceiptAsync(int correspondenceId, int userId,
        CancellationToken ct = default);

    /// <summary>مراسلة متتبَّعة مع رسائلها وتوثيق مشاهداتها — للكتابة داخل معاملة.</summary>
    Task<Correspondence?> GetTrackedWithDetailsAsync(int id, CancellationToken ct = default);
}
