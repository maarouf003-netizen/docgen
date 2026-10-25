using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.DTOs;

/// <summary>
/// عقد داخلي لتسجيل تنبيه نظامي (لا يقبله HTTP ولا يمرّره العميل) — الخطة §9.2.
///
/// контракт: يحمل <c>Kind</c> صراحةً و<code>Audience</code> (مصدر نطاق الإشعار)،
/// و<code>CreatedById</code> الإلزامي (منشئ الحدث — لا يُستنتج من المستلم)، وحقول الربط
/// الدلالية. **لا يحمل <c>DedupeKey</c> ولا <c>TargetType</c>**: الأول يُبنى مركزيًا في
/// <c>HeadAlertDedupe.Build</c>، والثاني يُشتق من <c>Audience</c>.
/// </summary>
public record CreateSystemHeadAlertRequest(
    string Kind,
    string Message,
    HeadAlertAudience Audience,
    int BranchId,
    int CreatedById,
    int? DocumentId = null,
    int? TargetLawyerId = null,
    int? DelegationId = null,
    int? AppealId = null,
    int? ReviewLetterId = null,
    int? PublicEntityId = null,
    int? ExecutionCircuitId = null,
    int? RecipientUserId = null,
    string? EntityOperation = null,
    string? EntityGroupKey = null);

/// <summary>
/// خطة استبدال التنبيه (الخطة §9.2) — تُمرَّر إلى معالج التنبيه النظامي فقط، ولا تُقبل
/// من HTTP ولا في المسار اليدوي: لا مسار عميل يتحكم في أرشفة غيره.
///
/// يجب أن يحدد <c>DelegationId</c> أو <c>AppealId</c> (واحدًا على الأقل)، و<code>ArchiveKinds</code>
/// غير فارغة ومحصورة في عائلة الكتالوج المناسبة، و<code>Reason</code> غير فارغ.
/// </summary>
public sealed record HeadAlertSupersedePlan(
    int? DelegationId,
    int? AppealId,
    IReadOnlySet<string> ArchiveKinds,
    string Reason);

/// <summary>نتيجة كتابة تنبيه نظامي — يحوّلها المشغّل إلى <c>processed</c> أو <c>skipped</c>.</summary>
public enum HeadAlertWriteResult
{
    /// <summary>أُنشئ صف جديد.</summary>
    Created = 0,

    /// <summary>حُدِّث صف قائم (تغيّر النص ⇒ إعادة ضبط القراءة).</summary>
    Updated = 1,

    /// <summary>الصف قائم والنص مطابق — بلا تحديث (مع تطبيق خطة الاستبدال إن وُجدت).</summary>
    Unchanged = 2,

    /// <summary>الرسالة أقدم من آخر تغيير محتوى — لا شيء يُكتب (يُسجَّل <c>stale_event</c>).</summary>
    Stale = 3
}
