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
/// عقد **التفاصيل** الوحيد الذي يحمل <see cref="Message"/> كاملًا (الخطة §9.2) —
/// يُعاد من <c>GET /api/alerts/{id}</c> فقط. القوائم تستخدم <see cref="HeadAlertSummaryDto"/>.
///
/// <para><c>IsRead</c> هي حالة القارئ نفسه (أو <c>null</c> لمن ليس مستلمًا)،
/// و<c>CanViewRecipients</c> قرار خادم (<c>CreatedById == actorUserId</c> أو مدير/مشرف)
/// لا يملك العميل تعيينه. **دلالته محصورة بقائمة المستلمين** وحدها (زر «عرض المستلمين»)،
/// ولا تحكم <c>RecipientCount</c>/<c>UnreadCount</c> — العدّادان يُرسَلان دائمًا في هذا
/// العقد (مؤقتًا حتى PR-6)، فلا يجوز للواجهة استنتاج غيابهما من <c>false</c>.
/// لا يحمل هذا العقد <c>Recipients</c> أبدًا؛ قائمة المستلمين
/// تُطلب من <c>GET /api/alerts/{id}/recipients</c> وحدها (§9.2).</para>
///
/// <para><c>RecipientCount</c>/<c>UnreadCount</c> محتفظ بهما مؤقتًا من العقد السابق
/// (ورقة انطلاق PR-4 §3.2: لا حذف في هذه المرحلة) ويُزالان في PR-6 بعد توقف الواجهة
/// عن عقد التفاصيل القديم.</para>
/// </summary>
public record HeadAlertDto(
    int Id,
    string Kind,
    string Message,
    string TargetType,
    int? DocumentId,
    string? DocumentTitle,
    int? TargetLawyerId,
    string? TargetLawyerName,
    /// <summary>الإنابة المرتبطة بالتنبيه (تنبيهات المرآة/المتابعة) — لشريطي نشاط بطاقتي الإنابة.</summary>
    int? DelegationId,
    /// <summary>الاستئناف المرتبط بالتنبيه — للانتقال المباشر إلى تفاصيله من الواجهة.</summary>
    int? AppealId,
    /// <summary>كتاب المطالعة المرتبط (تنبيه الرد) — للانتقال المباشر إلى صفحة الكتاب.</summary>
    int? ReviewLetterId,
    int? PublicEntityId,
    int? ExecutionCircuitId,
    DateTime CreatedAt,
    DateTime LastContentChangedAt,
    bool IsArchived,
    DateTime? ArchivedAt,
    string? CreatedByName,
    bool? IsRead,
    bool CanViewRecipients,
    /// <summary>محتفظ به مؤقتًا (PR-6) — عدّاد مستلمي الصف كما في العقد السابق.</summary>
    int? RecipientCount = null,
    /// <summary>محتفظ به مؤقتًا (PR-6) — عدّاد غير المقروء كما في العقد السابق.</summary>
    int? UnreadCount = null);

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
