using DocGenerator.Domain.Enums;

namespace DocGenerator.Domain.Entities;

/// <summary>
/// رسالة صندوق الصادر (Outbox) لتسليم التنبيهات النظامية: تُسجَّل داخل معاملة العمل
/// نفسها، ثم يعالجها مشغّل واحد مرتّب.
///
/// الضمانات:
/// <list type="bullet">
/// <item>لا علاقات (Navigation) ولا مفاتيح أجنبية: الحمولة JSON دلالية فقط.</item>
/// <item><c>Id</c> متزايد — أساس الالتقاط المرتّب (<c>Id ASC</c>).</item>
/// <item>الحالة تُخزَّن كنص صغير (lowercase) عبر محوّل صريح — لا عدد رقمي.</item>
/// </list>
/// </summary>
public class HeadAlertOutboxMessage
{
    /// <summary>معرّف متزايد — ترتيب الالتقاط في المشغّل.</summary>
    public long Id { get; set; }

    /// <summary>نوع العملية (<c>upsert</c> / <c>circuit_renamed</c>).</summary>
    public HeadAlertOutboxOperation Operation { get; set; }

    /// <summary>حالة الرسالة في دورة المعالجة.</summary>
    public HeadAlertOutboxStatus Status { get; set; }

    /// <summary>نوع التنبيه — إلزامي لـ<c>upsert</c>، و<c>NULL</c> لـ<c>circuit_renamed</c>.</summary>
    public string? Kind { get; set; }

    /// <summary>الحمولة بصيغة JSON (الإصدار 1) — تُبنى عند التسجيل وتُفكّ عند المعالجة.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>إصدار الحمولة — يُرفض أي إصدار غير معروف بلا إعادة محاولة.</summary>
    public int PayloadVersion { get; set; } = 1;

    /// <summary>زمن وقوع الحدث — يُستخدم كـ<code>CreatedAt</code> للتنبيه و<code>LastContentChangedAt</code>.</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>زمن تسجيل الرسالة في الصندوق (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>أأبكر موعد مسموح به للالتقاط (تباعد تصاعدي بين المحاولات).</summary>
    public DateTime NextAttemptAt { get; set; }

    /// <summary>زمن المعالجة النهائية (نجاح/تخطٍّ) — يُستخدم في التنظيف.</summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>نهاية مدة الحجز (lease) — بعدها تُستعاد الرسالة تلقائيًا.</summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>معرّف المشغّل الذي حمل الرسالة (لتشخيص التعارض بين النسخ).</summary>
    public string? LockedBy { get; set; }

    /// <summary>عدد المحاولات — يُستخدم لجدولة التباعد التصاعدي (وليس لتقرير «إعادة واحدة»).</summary>
    public int Attempts { get; set; }

    /// <summary>سبب آخر فشل أو تخطٍّ (مُنقّى، بلا نص التنبيه الكامل).</summary>
    public string? LastError { get; set; }

    /// <summary>المستخدم المستهدف للجمهور الأحادي — يُعاد توجيهه عند التعاقب.</summary>
    public int? TargetUserId { get; set; }
}
