namespace DocGenerator.Application.DTOs;

/// <summary>
/// إنشاء تنبيه — رئيس القسم لفرعه فقط.
/// TargetType: "document" (مع DocumentId) / "lawyer" (مع TargetLawyerId) / "branch" (تعميم للفرع) /
/// "head" (تنبيهات النظام لرئيس القسم، مثل مراحل الإنابة والاستئناف — مع DelegationId/AppealId لتصفيتها تلقائيًا).
/// </summary>
public record CreateHeadAlertRequest(
    string TargetType,
    int? DocumentId,
    int? TargetLawyerId,
    string Message,
    int? DelegationId = null,
    int? AppealId = null);

/// <summary>
/// تنبيه لعرض المحامي (IsRead) أو رئيس القسم (RecipientCount/UnreadCount).
/// IsRead تُملأ لرأي المحامي، والعدادات تُملأ لرأي رئيس القسم.
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
    int? AppealId = null);
