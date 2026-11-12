using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Services;

/// <summary>
/// خدمة «المتدخلين»: عمليات الإضافة/التعديل/الحذف المستقلة عن حفظ نموذج الملف
/// (كما الإجراءات)، مع مزامنة وقعة «طلب تدخل» في نفس المعاملة وإعادة بناء نص البحث.
/// الاتجاه: النواة ← المتدخلين فقط عند الحاجة لقراءة المجموعة من استجابة الملف.
/// </summary>
public interface IDocumentInterventionService
{
    /// <summary>إضافة متدخل (يمنع صفة «عرض وايداع» ويتحقق من السجل المرجعي للجهة العامة).</summary>
    Task<DocumentInterventionDto> AddAsync(
        int documentId,
        DocumentInterventionUpsertRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default);

    /// <summary>تعديل متدخل قائم ومزامنة وقعته (يعيد `null` إذا لم يكن للملف).</summary>
    Task<DocumentInterventionDto?> UpdateAsync(
        int documentId,
        int interventionId,
        DocumentInterventionUpsertRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default);

    /// <summary>حذف متدخل وورثته ووقعته (يعيد `false` إذا لم يكن للملف).</summary>
    Task<bool> DeleteAsync(
        int documentId,
        int interventionId,
        string? actorName,
        CancellationToken ct = default);
}
