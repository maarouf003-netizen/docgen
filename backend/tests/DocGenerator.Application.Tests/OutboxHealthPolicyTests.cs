using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;

namespace DocGenerator.Application.Tests;

/// <summary>
/// P5-03 — سياسة جهوزية صندوق الصادر: تأخر أقدم رسالة معلّقة فوق العتبة = غير جاهز،
/// ورسائل فاشلة = متدهور (بلا إسقاط الجهوزية)، وإلا سليم.
/// </summary>
[Trait("Category", "Unit")]
public class OutboxHealthPolicyTests
{
    private static readonly DateTime Now = new(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Threshold = TimeSpan.FromMinutes(15);

    [Fact]
    public void EmptyOutbox_IsHealthy()
    {
        var verdict = OutboxHealthPolicy.Evaluate(new HeadAlertOutboxHealth(0, null, 0), Now, Threshold);

        Assert.Equal(OutboxHealthStatus.Healthy, verdict.Status);
    }

    [Fact]
    public void FreshPending_IsHealthy()
    {
        var health = new HeadAlertOutboxHealth(3, Now.AddMinutes(-2), 0);

        var verdict = OutboxHealthPolicy.Evaluate(health, Now, Threshold);

        Assert.Equal(OutboxHealthStatus.Healthy, verdict.Status);
        Assert.Contains("3", verdict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PendingExactlyAtThreshold_IsStillHealthy()
    {
        // الحد نفسه ليس تأخرًا (المقارنة `>` صريحة)، فلا ترتجف الجهوزية على الحدود.
        var health = new HeadAlertOutboxHealth(1, Now - Threshold, 0);

        Assert.Equal(OutboxHealthStatus.Healthy, OutboxHealthPolicy.Evaluate(health, Now, Threshold).Status);
    }

    [Fact]
    public void PendingBeyondThreshold_IsUnhealthy()
    {
        var health = new HeadAlertOutboxHealth(2, Now.AddMinutes(-16), 0);

        var verdict = OutboxHealthPolicy.Evaluate(health, Now, Threshold);

        Assert.Equal(OutboxHealthStatus.Unhealthy, verdict.Status);
        Assert.Contains("16", verdict.Message, StringComparison.Ordinal);
        Assert.Contains("15", verdict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FailedMessages_AreDegraded()
    {
        var health = new HeadAlertOutboxHealth(0, null, 4);

        var verdict = OutboxHealthPolicy.Evaluate(health, Now, Threshold);

        Assert.Equal(OutboxHealthStatus.Degraded, verdict.Status);
        Assert.Contains("4", verdict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LagTakesPrecedence_OverFailedMessages()
    {
        var health = new HeadAlertOutboxHealth(1, Now.AddHours(-2), 1);

        Assert.Equal(OutboxHealthStatus.Unhealthy, OutboxHealthPolicy.Evaluate(health, Now, Threshold).Status);
    }

    [Fact]
    public void NonPositiveThreshold_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OutboxHealthPolicy.Evaluate(new HeadAlertOutboxHealth(0, null, 0), Now, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OutboxHealthPolicy.Evaluate(new HeadAlertOutboxHealth(0, null, 0), Now, TimeSpan.FromMinutes(-1)));
    }
}
