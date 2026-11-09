using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// نتيجة فحص تعارض رقم الأساس. الحقول التفصيلية (المعرّفات/الحالة) للتدقيق الداخلي
/// فقط؛ الرسائل والرد العام يحملان النطاق (السنة/النوع/الدائرة) لا هوية الملف الآخر.
/// </summary>
public sealed record NumberConflict(
    bool IsHard,
    IReadOnlyList<int> ConflictingRowIds,
    IReadOnlyList<int> ExistingDocumentIds,
    int Year,
    int CircuitId,
    string CircuitName,
    string FileType,
    string? Status,
    bool IsSelf,
    string Message,
    string Fingerprint,
    string? WarningToken);

/// <summary>
/// الكاتب الوحيد لرقم الملف وأرقام الأساس (V-15): قيد/تدوير/تصحيح/تراجع/تجديد/إلغاء.
/// كل عملية داخل معاملة + قفل (دائرة، سنة) + إعادة قراءة + فحص تفرد طبقتين.
/// لا حذف صفي: الحذف/التراجع = <c>VoidedAt</c> وبقاء الصف في السجل.
/// </summary>
public interface IDocumentNumberRegistry
{
    /// <summary>
    /// هل يسمح بسلوك «بلا دائرة» الانتقالي؟ false في الإنتاج؛ true في بناءات الاختبار
    /// القائمة فقط. كل المسارات الإنتاجية ترفض رقمًا بلا دائرة (V-18).
    /// </summary>
    bool AllowsCircuitlessLegacy { get; }

    /// <summary>مزامنة صف القيد مع حقول المستند (إنشاء/تعديل/إعادة قيد/تسجيل إنابة).</summary>
    Task SyncRegistrationAsync(
        Document doc, int? actorUserId, string? actorName, string? warningToken, CancellationToken ct,
        bool isNewDocument = false);

    /// <summary>فحص تعارض (داخل القفل): صلب لا يُتجاوز، ولين يحمل رمز تحذير.</summary>
    Task<NumberConflict?> FindConflictAsync(
        Document doc,
        int? excludeRowId,
        int? actorUserId,
        int circuitId,
        string typeKey,
        int year,
        string numberKey,
        CancellationToken ct);

    /// <summary>حفظ دفعة تدوير ذرّيًا (الإدخال الفارغ = لا تغيير).</summary>
    Task SaveRotationsAsync(
        int userId,
        string? actorName,
        IReadOnlyList<BaseNumberEntry> entries,
        string? warningToken,
        CancellationToken ct);

    /// <summary>تصحيح موضعي لصف سنة محددة (لا حذف، ولا مساس بصفوف سنة أخرى).</summary>
    Task CorrectAsync(
        int documentId,
        int year,
        int? rowId,
        string number,
        int actorUserId,
        string? actorName,
        string? warningToken,
        CancellationToken ct);

    /// <summary>التراجع عن تدوير السنة الحالية: إلغاء صف التدوير فقط مع بقائه معروضًا.</summary>
    Task UndoRotationAsync(
        int documentId, int year, int actorUserId, string? actorName, CancellationToken ct);

    /// <summary>إضافة صف تجديد/إعادة شطب (Source=renewal) مع فحص السنة والتفرد.</summary>
    Task ApplyRenewalAsync(
        Document doc,
        string number,
        int year,
        string? fileType,
        int actorUserId,
        string? actorName,
        string? warningToken,
        CancellationToken ct);

    /// <summary>حذف الملف = إلغاء كل صفوفه (document-deleted) مع بقائها معروضة.</summary>
    Task VoidForDocumentDeleteAsync(
        Document doc, int actorUserId, string? actorName, CancellationToken ct);

    /// <summary>السجل الكامل (يشمل الملغى وصفوف الملف المحذوف عند فتحه من سلة المحذوفات).</summary>
    Task<IReadOnlyList<BaseNumberHistoryDto>> GetHistoryAsync(
        int documentId, bool includeDeletedDocument, CancellationToken ct);
}
