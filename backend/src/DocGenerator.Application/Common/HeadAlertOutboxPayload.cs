using System.Text.Json;
using System.Text.Json.Serialization;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Common;

/// <summary>
/// حمولة رسالة صندوق الصادر — الإصدار 1 (الخطة §6.4).
///
/// <list type="bullet">
/// <item><c>operation</c>: <c>upsert</c> أو <c>circuit_renamed</c> (ويُتحقق من تطابقها مع العمود).</item>
/// <item><c>occurredAt</c>: زمن الحدث (UTC) — يُستخدم كـ<code>CreatedAt</code> للتنبيه.</item>
/// <item><c>targetUserId</c>: تجاوز صريح للمستلم (الجمهور الأحادي) عند التعاقب.</item>
/// <item><c>request</c>: الطلب الداخلي كاملًا (بما فيه <c>CreatedById</c> الإلزامي).</item>
/// <item><c>supersedePlan</c>: خطة الاستبدال — تُسلسَل <c>archiveKinds</c> كمصفوفة نصية.</item>
/// </list>
///
/// لا تُخزَّن كيانات EF في الحمولة، ولا <c>DedupeKey</c> (يُبنى عند المعالجة)، ولا
/// <c>TargetType</c> (يُشتق من <c>Audience</c>).
/// </summary>
public sealed record HeadAlertOutboxPayloadV1
{
    /// <summary>نوع العملية — يُطابق عمود <c>Operation</c> (يُرفض التعارض).</summary>
    public string Operation { get; init; } = string.Empty;

    /// <summary>زمن وقوع الحدث (UTC).</summary>
    public DateTime OccurredAt { get; init; }

    /// <summary>المستخدم المستهدف للجمهور الأحادي (تجاوز صريح عند التعاقب).</summary>
    public int? TargetUserId { get; init; }

    /// <summary>الطلب الداخلي — لحالة <c>upsert</c>.</summary>
    public CreateSystemHeadAlertRequest? Request { get; init; }

    /// <summary>خطة الاستبدال — اختيارية.</summary>
    public HeadAlertSupersedePlanPayload? SupersedePlan { get; init; }

    /// <summary>معرّف الدائرة — لحالة <c>circuit_renamed</c>.</summary>
    public int? CircuitId { get; init; }

    /// <summary>نص إعادة التسمية — لحالة <c>circuit_renamed</c>.</summary>
    public string? Message { get; init; }

    /// <summary>خطة الاستبدال في صيغتها القابلة للتسلسل.</summary>
    public sealed record HeadAlertSupersedePlanPayload
    {
        public int? DelegationId { get; init; }
        public int? AppealId { get; init; }
        public string[] ArchiveKinds { get; init; } = [];
        public string Reason { get; init; } = string.Empty;
    }
}

/// <summary>
/// إعدادات صندوق الصادر (الخطة §8.2) — القسم <c>HeadAlerts:Outbox</c>.
/// </summary>
public sealed class HeadAlertOutboxOptions
{
    /// <summary>اسم القسم في الإعدادات.</summary>
    public const string SectionName = "HeadAlerts:Outbox";

    /// <summary>هل يعمل المشغّل؟ (إيقافه يمنع معالجة الرسائل — فتتراكم <c>pending</c>).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>فاصل الاستطلاع بين الدورات (بالدقائق).</summary>
    public int PollIntervalSeconds { get; set; } = 2;

    /// <summary>مدة حجز الرسالة (lease) قبل أن تُستعاد (بالدقائق).</summary>
    public int LeaseDurationMinutes { get; set; } = 2;

    /// <summary>حدّ المحاولات قبل وضع الرسالة <c>failed</c>.</summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>مدة الاحتفاظ بالرسائل المعالجة (بالأيام).</summary>
    public int ProcessedRetentionDays { get; set; } = 7;

    /// <summary>مدة الاحتفاظ بالرسائل المتخطاة (بالأيام).</summary>
    public int SkippedRetentionDays { get; set; } = 30;
}

/// <summary>
/// إعدادات الجسر الانتقالي للصفوف <c>legacy</c> (الخطة §6.5) — القسم
/// <c>HeadAlerts:LegacyBridge</c>.
///
/// **مغلق افتراضيًا:** الجسر لا يُفعَّل إلا بقرار بوابة G0 على بيانات الإنتاج
/// (الخطة §2.3)، فتبقى كل الصفوف القديمة <c>legacy</c> للعرض التاريخي.
/// </summary>
public sealed class HeadAlertLegacyBridgeOptions
{
    /// <summary>اسم القسم في الإعدادات.</summary>
    public const string SectionName = "HeadAlerts:LegacyBridge";

    /// <summary>هل يعمل الجسر الانتقالي؟</summary>
    public bool Enabled { get; set; }
}

/// <summary>
///Reasons موحّدة لتخطي رسالة — قيم قصيرة لا استثناءات (الخطة §6.4).
/// </summary>
public static class HeadAlertOutboxSkipReasons
{
    public const string StaleEvent = "stale_event";
    public const string RecipientInactive = "recipient_inactive";
    public const string NoActiveAlerts = "no_active_alerts";
    public const string MissingReference = "missing_reference";
    public const string NoRecipients = "no_recipients";
}

/// <summary>
/// أدوات تسلسل حمولة صندوق الصادر — إعدادات واحدة تُستخدم للكتابة والقراءة (الخطة §6.4).
/// </summary>
public static class HeadAlertOutboxPayloadSerializer
{
    /// <summary>الإصدار الحالي من الحمولة.</summary>
    public const int CurrentPayloadVersion = 1;

    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    /// <summary>يحوّل الحمولة إلى نص JSON.</summary>
    public static string Serialize(HeadAlertOutboxPayloadV1 payload) => JsonSerializer.Serialize(payload, Options);

    /// <summary>
    /// يفكّ الحمولة. أي فشل (JSON تالف أو نوع غير متوقع) يرمي استثناءً صريحًا —
    /// المعالج يحوّله إلى <c>failed</c> (‏<c>invalid_payload</c>) بلا إعادة.
    /// </summary>
    public static HeadAlertOutboxPayloadV1 Deserialize(string payload)
        => JsonSerializer.Deserialize<HeadAlertOutboxPayloadV1>(payload, Options)
           ?? throw new JsonException("حمالة صندوق الصادر فارغة أو غير صالحة");
}
