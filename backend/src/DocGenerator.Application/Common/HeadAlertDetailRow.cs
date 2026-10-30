using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Common;

/// <summary>
/// صف التفاصيل المسقط في SQL (ورقة إصلاح PR-4 §6، المرحلة 2): حقول العرض كلها +
/// <c>CreatedById</c> لقرار الرؤية (D2) + عدّادي المستلمين المحتفظ بهما حتى <c>PR-6</c>
/// (D5) + حالة قراءة الفاعل — في استعلام واحد بلا <c>Include</c> ولا كيانات محمَّلة.
///
/// <para><paramref name="IsRecipient"/> صريح ولا يُستنتج من <paramref name="IsRead"/>:
/// الأول قرار صلاحية، والثاني حالة قراءة قد تكون <c>false</c> لصف استلام قائم.</para>
/// </summary>
public sealed record HeadAlertDetailRow(
    int Id,
    string Kind,
    string Message,
    HeadAlertTargetType TargetType,
    int? DocumentId,
    bool DocumentExists,
    string? BorrowerName,
    string? BorrowerFather,
    string? BorrowerFamily,
    string? DocumentType,
    int? TargetLawyerId,
    string? TargetLawyerName,
    int? DelegationId,
    int? AppealId,
    int? ReviewLetterId,
    int? PublicEntityId,
    int? ExecutionCircuitId,
    DateTime CreatedAt,
    DateTime LastContentChangedAt,
    bool IsArchived,
    DateTime? ArchivedAt,
    string? CreatedByName,
    int CreatedById,
    bool IsRecipient,
    bool? IsRead,
    int RecipientCount,
    int UnreadCount);
