namespace DocGenerator.Api.Health;

using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

/// <summary>
/// P5-03 — جهوزية صندوق الصادر: يقرأ لقطة الصحة من المخزن ويطبّق
/// <see cref="OutboxHealthPolicy"/> (تأخر معلّق فوق العتبة = غير جاهز، رسائل فاشلة =
/// متدهور). السياسة نفسها نقية ومغطّاة بوحدات اختبار مستقلة.
/// </summary>
public sealed class HeadAlertOutboxHealthCheck : IHealthCheck
{
    private readonly IHeadAlertOutboxStore _store;
    private readonly TimeProvider _clock;
    private readonly OutboxHealthOptions _options;

    public HeadAlertOutboxHealthCheck(
        IHeadAlertOutboxStore store,
        TimeProvider clock,
        IOptions<OutboxHealthOptions> options)
    {
        _store = store;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var health = await _store.GetHealthAsync(ct);
            var verdict = OutboxHealthPolicy.Evaluate(
                health,
                _clock.GetUtcNow().UtcDateTime,
                TimeSpan.FromMinutes(Math.Max(1, _options.LagThresholdMinutes)));

            return verdict.Status switch
            {
                OutboxHealthStatus.Unhealthy => HealthCheckResult.Unhealthy(verdict.Message),
                OutboxHealthStatus.Degraded => HealthCheckResult.Degraded(verdict.Message),
                _ => HealthCheckResult.Healthy(verdict.Message),
            };
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("فشل فحص صحة صندوق الصادر.", ex);
        }
    }
}
