using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// استعلامات كتب المطالعة على مستوى قاعدة البيانات:
/// قوائم المحامي/الفرع/الوصول الكامل مع البحث والترقيم، عدّاد بانتظار الرد،
/// وقراءة كتاب برسائله، وكتب ملف محدد.
/// </summary>
public interface IReviewLetterRepository : IRepository<ReviewLetter>
{
    /// <summary>كتب مطالعة المحامي (المسطَّرة منه)، الأحدث تحديثًا أولاً، مع البحث والترقيم.</summary>
    Task<(List<ReviewLetter> Items, int TotalCount)> SearchForLawyerAsync(
        int userId, string? q, int page, int perPage, CancellationToken ct = default);

    /// <summary>كل كتب الفرع (عرض رئيس القسم)، الأحدث تحديثًا أولاً، مع البحث والترقيم.</summary>
    Task<(List<ReviewLetter> Items, int TotalCount)> SearchForBranchAsync(
        int branchId, string? q, int page, int perPage, CancellationToken ct = default);

    /// <summary>كل الكتب بلا قيد فرع (مدير/مشرف)، مع البحث والترقيم.</summary>
    Task<(List<ReviewLetter> Items, int TotalCount)> SearchAllAsync(
        string? q, int page, int perPage, CancellationToken ct = default);

    /// <summary>عدد كتب الفرع التي لم يُرد عليها بعد (جرس رئيس القسم).</summary>
    Task<int> CountPendingForBranchAsync(int branchId, CancellationToken ct = default);

    /// <summary>كتاب برسائله مرتبة زمنيًا ومنشئه وملفه وفرعه.</summary>
    Task<ReviewLetter?> GetByIdWithDetailsAsync(int id, CancellationToken ct = default);

    /// <summary>كتب ملف محدد (بطاقة تفاصيل الملف)، الأحدث أولاً.</summary>
    Task<List<ReviewLetter>> ListByDocumentAsync(int documentId, CancellationToken ct = default);

    /// <summary>هل رقم الكتاب مستخدم سابقًا؟ (ضمان تفرّد التوليد العشوائي).</summary>
    Task<bool> NumberExistsAsync(string letterNumber, CancellationToken ct = default);

    /// <summary>
    /// عدد كتب المحامي التي فيها ردّ رئيس قسم لم يطّلع عليه بعد — عدّاد شارة بند المطالعات.
    /// </summary>
    Task<int> CountUnseenReplyLettersForLawyerAsync(int userId, CancellationToken ct = default);

    /// <summary>كتاب متتبَّع مع رسائله — لتحديث أعلام الإطلاع داخل معاملة الكاتب.</summary>
    Task<ReviewLetter?> GetTrackedWithMessagesAsync(int id, CancellationToken ct = default);
}
