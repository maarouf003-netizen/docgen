namespace DocGenerator.Application.DTOs;

/// <summary>
/// إنشاء تنبيه — رئيس القسم لفرعه فقط.
/// TargetType: "document" (مع DocumentId) / "lawyer" (مع TargetLawyerId) / "branch" (تعميم للفرع).
/// </summary>
public record CreateHeadAlertRequest(
    string TargetType,
    int? DocumentId,
    int? TargetLawyerId,
    string Message);

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
    string? CreatedByName);
