namespace DocGenerator.Application.DTOs;

/// <summary>
/// إنشاء تنبيه — رئيس القسم لفرعه فقط.
/// TargetType: "document" (مع DocumentId) / "lawyer" (مع TargetLawyerId) / "branch" (تعميم للفرع) /
/// "head" (تنبيهات النظام لرئيس القسم، مثل مراحل الإنابة والاستئناف — مع DelegationId/AppealId لتصفيتها تلقائيًا).
/// `RecipientUserId` (اختياري): توجيه لمالك واحد بدل البث — يُتحقق أنه رئيس مفعّل في الفرع.
/// </summary>
public record CreateHeadAlertRequest(
    string TargetType,
    int? DocumentId,
    int? TargetLawyerId,
    string Message,
    int? DelegationId = null,
    int? AppealId = null,
    int? RecipientUserId = null);

/// <summary>
/// تنبيه لعرض المحامي أو الرئيس (IsRead لحالة القارئ نفسه — §8: قراءة بالمستلم).
/// RecipientCount/UnreadCount عدّادات صف التنبيه.
/// </summary>
public record HeadAlertDto(
    int Id,
    string Message,
    string TargetType,
    int? DocumentId,
    string? DocumentTitle,
    int? TargetLawyerId,
    string? TargetLawyerName,
    bool? IsRead,
    int? RecipientCount,
    int? UnreadCount,
    DateTime CreatedAt,
    string? CreatedByName,
    /// <summary>الاستئناف المرتبط بالتنبيه — للانتقال المباشر إلى تفاصيله من الواجهة.</summary>
    int? AppealId = null,
    /// <summary>كتاب المطالعة المرتبط (تنبيه الرد) — للانتقال المباشر إلى صفحة الكتاب.</summary>
    int? ReviewLetterId = null,
    /// <summary>الإنابة المرتبطة بالتنبيه (تنبيهات المرآة/المتابعة) — لشريطي نشاط بطاقتي الإنابة.</summary>
    int? DelegationId = null);
