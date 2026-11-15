using DocGenerator.Application.Common.Interfaces;

namespace DocGenerator.Application.Common;

/// <summary>نتيجة تقييم صحة الصندوق — منفصلة عن <c>HealthCheckResult</c> لتُختبر بلا مضيف.</summary>
public enum OutboxHealthStatus
{
    Healthy,
    Degraded,
    Unhealthy,
}

/// <summary>حكم صحة الصندوق مع رسالة قصيرة صالحة للسجلات والتشخيص.</summary>
public readonly record struct OutboxHealthVerdict(OutboxHealthStatus Status, string Message);

/// <summary>
/// P5-03 — سياسة جهوزية صندوق الصادر (نقية، بلا ساعة ولا قاعدة).
/// <para>
/// القاعدة: تأخّر أقدم رسالة معلّقة فوق العتبة = **غير جاهز** (الحركة تُوجَّه لنسخة
/// أخرى أو تنبيه تشغيلي)، ووجود رسائل فاشلة = **متدهور** (لا يُسقط الجهوزية لأن
/// الإجراء العلاجي بشري، لكنه ظاهر في الفحص)، وإلا سليم.
/// </para>
/// </summary>
public static class OutboxHealthPolicy
{
    public static OutboxHealthVerdict Evaluate(
        HeadAlertOutboxHealth health,
        DateTime utcNow,
        TimeSpan lagThreshold)
    {
        ArgumentNullException.ThrowIfNull(health);
        if (lagThreshold <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lagThreshold), lagThreshold, "عتبة تأخر الصندوق يجب أن تكون موجبة.");
        }

        if (health.OldestPendingAt is { } oldestPendingAt)
        {
            var lag = utcNow - oldestPendingAt;
            if (lag > lagThreshold)
            {
                return new OutboxHealthVerdict(
                    OutboxHealthStatus.Unhealthy,
                    $"تأخر صندوق الصادر {lag.TotalMinutes:F1} دقيقة يتجاوز العتبة "
                    + $"{lagThreshold.TotalMinutes:F0} دقيقة (معلّق: {health.PendingCount}).");
            }
        }

        if (health.FailedCount > 0)
        {
            return new OutboxHealthVerdict(
                OutboxHealthStatus.Degraded,
                $"صندوق الصادر يحمل {health.FailedCount} رسالة فاشلة تحتاج مراجعة يدوية.");
        }

        return new OutboxHealthVerdict(
            OutboxHealthStatus.Healthy,
            $"صندوق الصادر سليم (معلّق: {health.PendingCount}).");
    }
}
