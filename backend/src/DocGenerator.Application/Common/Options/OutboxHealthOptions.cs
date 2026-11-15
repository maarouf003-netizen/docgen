namespace DocGenerator.Application.Common.Options;

/// <summary>
/// P5-03 — عتبات فحص صحة صندوق الصادر (`/readyz` و`/healthz/outbox`).
/// </summary>
public sealed class OutboxHealthOptions
{
    /// <summary>
    /// أقصى عمر مقبول لأقدم رسالة معلّقة قبل اعتبار الجهوزية غير متحققة.
    /// القيمة الافتراضية 15 دقيقة: أكبر بكثير من دورة الاستطلاع (ثوانٍ) وأقل بكثير
    /// من أي نافذة تصعيد تشغيلي.
    /// </summary>
    public int LagThresholdMinutes { get; set; } = 15;
}
