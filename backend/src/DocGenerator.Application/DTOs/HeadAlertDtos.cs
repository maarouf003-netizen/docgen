namespace DocGenerator.Application.DTOs;

/// <summary>
/// إنشاء تنبيه يدوي — رئيس القسم/الشعبة لفرعه فقط.
/// TargetType: <c>document</c> (مع DocumentId) / <c>lawyer</c> (مع TargetLawyerId) /
/// <c>branch</c> (تعميم للفرع). لا يقبل هذا العقد أي ربط بدورة حياة نظامية
/// (`DelegationId`/`AppealId`) ولا مستلمًا داخليًا (`RecipientUserId`)؛ تلك
/// للمسار النظامي الداخلي <see cref="CreateSystemHeadAlertRequest"/> فقط (الخطة §5.3/§9.2).
/// </summary>
public record CreateHeadAlertRequest(
    string TargetType,
    int? DocumentId,
    int? TargetLawyerId,
    string Message);

/// <summary>
/// تنبيه لعرض المحامي أو الرئيس (IsRead لحالة القارئ نفسه — §8: قراءة بالمستلم).
/// RecipientCount/UnreadCount عدّادات صف التنبيه.
///
/// ملاحظة تسلسل: هذا هو عقد **القوائم** الحالي. في **PR-4** يُستبدل بعقد القائمة
/// `HeadAlertSummaryDto` (معاينة بلا نص كامل) وعقد التفاصيل `HeadAlertDto` الكامل،
/// ضمن نافذة إصدار واحدة مع PR-5 (§15 — اقتران نشر PR-4/PR-5).
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

/// <summary>
/// ملخّص تنبيه للقوائم — لا يحمل النص الكامل أبدًا (الخطة §9.2/§13).
/// <c>MessagePreview</c> معاينة عرض واعية بـUnicode، و<code>MessageLength</code> عدد نقاط Unicode،
/// و<code>PreviewTruncated</code> محسوب من الخادم (لا مقارنة أطوال في الواجهة).
/// </summary>
public record HeadAlertSummaryDto(
    int Id,
    string Kind,
    string MessagePreview,
    int MessageLength,
    bool PreviewTruncated,
    string TargetType,
    int? DocumentId,
    string? DocumentTitle,
    int? TargetLawyerId,
    string? TargetLawyerName,
    bool? IsRead,
    int? RecipientCount,
    int? UnreadCount,
    DateTime CreatedAt,
    DateTime LastContentChangedAt,
    string? CreatedByName,
    int? AppealId = null,
    int? ReviewLetterId = null,
    int? DelegationId = null,
    int? ExecutionCircuitId = null,
    bool IsArchived = false,
    DateTime? ArchivedAt = null);

/// <summary>مستلم تنبيه مع حالة قراءته — لقائمة «المستلمون» (الخطة §9.2).</summary>
public record HeadAlertRecipientDto(
    int Id,
    int UserId,
    string FullName,
    string Role,
    bool IsRead,
    DateTime? ReadAt);
