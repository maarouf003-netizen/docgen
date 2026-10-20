using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocGenerator.Infrastructure.Outbox;

/// <summary>
/// مشغّل صندوق الصادر (الخطة §6.4) — خدمة خلفية واحدة نشطة مرتّبة بـ<code>Id</code>.
///
/// دورة المعالجة:
/// <list type="number">
/// <item>التقاط صف واحد في معاملة قصيرة (نطاق جديد).</item>
/// <item>معالجته في معاملة جديدة (نطاق جديد): فكّ الحمولة، التحقق من الإصدار، إنشاء/
/// تحديث التنبيه، ثم تحديث صف الصندوق <c>processed</c>/<c>skipped</c> في المعاملة نفسها.</item>
/// <item>عند الفشل: تحديث الصف في نطاق جديد (بعد التراجع) — <c>pending</c> مع تباعد
/// تصاعدي، أو <c>failed</c> بعد استنفاد المحاولات.</item>
/// </list>
///
/// **ملكية المعاملة:** لا يحقن <c>DbContext</c> ولا المخزن — ينشئ <c>IServiceScope</c>
/// مستقلًا لكل دورة، فلا تتبّع كيانات ملوّث ولا معاملة متداخلة.
/// </summary>
public sealed class HeadAlertOutboxDispatcher : BackgroundService
{
    /// <summary>علامة «إعادة واحدة» عند التعارض — تُميّزها عن عدّاد المحاولات.</summary>
    private const string ConflictRetryMarker = "conflict_retry";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HeadAlertOutboxOptions _options;
    private readonly HeadAlertOutboxLock _lock;
    private readonly ILogger<HeadAlertOutboxDispatcher> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public HeadAlertOutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        IOptions<HeadAlertOutboxOptions> options,
        HeadAlertOutboxLock outboxLock,
        ILogger<HeadAlertOutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _lock = outboxLock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("مشغّل صندوق الصادر معطَّل — تُسجَّل الرسائل دون معالجة.");
            return;
        }

        var poll = TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await _lock.TryAcquireAsync(stoppingToken).ConfigureAwait(false))
                {
                    await Task.Delay(poll, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    var processed = await ProcessNextAsync(stoppingToken).ConfigureAwait(false);
                    if (!processed)
                        await Task.Delay(poll, stoppingToken).ConfigureAwait(false);
                }
                finally
                {
                    await _lock.ReleaseAsync().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "فشل مشغّل صندوق الصادر في دورة — تُؤجَّل المعالجة.");
                try { await Task.Delay(poll, stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    /// <summary>يعالج رسالة واحدة؛ يُرجع <c>false</c> حين لا توجد رسالة جاهزة.</summary>
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        var claimed = await ClaimAsync(ct).ConfigureAwait(false);
        if (claimed is null)
            return false;

        try
        {
            await ProcessAsync(claimed, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // الإلغاء لا يحوّل الصف إلى failed: يبقى processing حتى ينتهي الـlease.
            throw;
        }
        catch (HeadAlertNoRecipientsException ex)
        {
            await MarkSkippedAsync(claimed.Id, HeadAlertOutboxSkipReasons.NoRecipients, ct).ConfigureAwait(false);
            _logger.LogInformation("قُطعت رسالة صندوق الصادر (لا مستلمين): {Message}", ex.Message);
        }
        catch (HeadAlertConflictException ex)
        {
            await HandleConflictAsync(claimed, ex, ct).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            await MarkFailedAsync(claimed.Id, $"invalid_payload: {ex.Message}", ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await HandleTransientAsync(claimed, ex, ct).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>يلتقط صفًا واحدًا في معاملة قصيرة (نطاق جديد).</summary>
    private async Task<HeadAlertOutboxMessage?> ClaimAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IHeadAlertOutboxStore>();

        return await store.ExecuteInTransactionAsync(
            token => store.ClaimNextAsync(_workerId, TimeSpan.FromMinutes(Math.Max(1, _options.LeaseDurationMinutes)), token),
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// يعالج الرسالة داخل معاملة واحدة: إنشاء/تحديث التنبيه **و** تحديث صف الصندوق
    /// في المعاملة نفسها — فلا يُعاد التنبيه عند إعادة المحاولة.
    /// </summary>
    private async Task ProcessAsync(HeadAlertOutboxMessage message, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IHeadAlertOutboxStore>();
        var alerts = scope.ServiceProvider.GetRequiredService<IHeadAlertService>();

        await store.ExecuteInTransactionAsync(async token =>
        {
            if (message.PayloadVersion != HeadAlertOutboxPayloadSerializer.CurrentPayloadVersion)
            {
                await store.MarkFailedAsync(
                    message.Id,
                    $"invalid_payload: إصدار غير معروف ({message.PayloadVersion})",
                    token).ConfigureAwait(false);
                return true;
            }

            var payload = HeadAlertOutboxPayloadSerializer.Deserialize(message.Payload);
            ValidatePayload(message, payload);

            var outcome = await DispatchAsync(alerts, payload, message, token).ConfigureAwait(false);

            if (outcome == HeadAlertWriteResult.Stale)
                await store.MarkSkippedAsync(message.Id, HeadAlertOutboxSkipReasons.StaleEvent, token).ConfigureAwait(false);
            else
                await store.MarkProcessedAsync(message.Id, token).ConfigureAwait(false);

            return true;
        }, ct).ConfigureAwait(false);
    }

    private static async Task<HeadAlertWriteResult> DispatchAsync(
        IHeadAlertService alerts,
        HeadAlertOutboxPayloadV1 payload,
        HeadAlertOutboxMessage message,
        CancellationToken ct)
    {
        if (message.Operation == HeadAlertOutboxOperation.Circuit_Renamed)
        {
            if (payload.CircuitId is null)
                throw new JsonException("حمالة إعادة التسمية بلا معرّف دائرة");

            await alerts.ApplyCircuitRenamedAsync(
                payload.CircuitId.Value, payload.Message ?? string.Empty, payload.OccurredAt, ct).ConfigureAwait(false);
            return HeadAlertWriteResult.Updated;
        }

        if (payload.Request is null)
            throw new JsonException("حمالة الإنشاء بلا طلب");

        var supersede = payload.SupersedePlan is null
            ? null
            : new HeadAlertSupersedePlan(
                payload.SupersedePlan.DelegationId,
                payload.SupersedePlan.AppealId,
                new HashSet<string>(payload.SupersedePlan.ArchiveKinds, StringComparer.Ordinal),
                payload.SupersedePlan.Reason);

        return await alerts.CreateSystemAsync(
            payload.Request,
            payload.OccurredAt,
            supersede,
            payload.TargetUserId,
            ct).ConfigureAwait(false);
    }

    /// <summary>يتحقق من تطابق العملية والنوع بين العمود والحمولة (يُرفض التعارض).</summary>
    private static void ValidatePayload(HeadAlertOutboxMessage message, HeadAlertOutboxPayloadV1 payload)
    {
        var expected = message.Operation.ToString().ToLowerInvariant();
        if (!string.Equals(payload.Operation, expected, StringComparison.Ordinal))
            throw new JsonException($"تعارض بين عملية الصف ({expected}) والحمولة ({payload.Operation})");

        if (message.Operation == HeadAlertOutboxOperation.Upsert && string.IsNullOrWhiteSpace(message.Kind))
            throw new JsonException("رسالة الإنشاء بلا نوع");
    }

    /// <summary>
    /// سياسة التعارض: إعادة واحدة فورية (بلا backoff) بعلامة <c>conflict_retry</c> صريحة،
    /// ثم <c>failed</c> إن تكرر. لا تعتمد على عدّاد <c>Attempts</c>.
    /// </summary>
    private async Task HandleConflictAsync(
        HeadAlertOutboxMessage message,
        HeadAlertConflictException exception,
        CancellationToken ct)
    {
        if (string.Equals(message.LastError, ConflictRetryMarker, StringComparison.Ordinal))
        {
            await MarkFailedAsync(message.Id, $"{ConflictRetryMarker}: {exception.Message}", ct).ConfigureAwait(false);
            _logger.LogWarning("تعارض متكرر على مفتاح منع التكرار — الرسالة فاشلة: {Message}", exception.Message);
            return;
        }

        await MarkRetryAsync(message.Id, DateTime.UtcNow, ConflictRetryMarker, ct).ConfigureAwait(false);
        _logger.LogInformation("تعارض على مفتاح منع التكرار — إعادة واحدة فورية: {Message}", exception.Message);
    }

    /// <summary>استثناء عابر: تباعد تصاعدي، ثم <c>failed</c> بعد <c>MaxAttempts</c>.</summary>
    private async Task HandleTransientAsync(
        HeadAlertOutboxMessage message,
        Exception exception,
        CancellationToken ct)
    {
        if (message.Attempts >= _options.MaxAttempts)
        {
            await MarkFailedAsync(message.Id, Describe(exception), ct).ConfigureAwait(false);
            _logger.LogError(exception, "فشل معالجة رسالة صندوقihara بعد {Attempts} محاولة.", message.Attempts);
            return;
        }

        var next = DateTime.UtcNow.Add(Backoff(message.Attempts));
        await MarkRetryAsync(message.Id, next, Describe(exception), ct).ConfigureAwait(false);
        _logger.LogWarning(exception, "فشل عابر في معالجة رسالة صندوق الصادر — إعادة بعد {Next}.", next);
    }

    private static TimeSpan Backoff(int attempts) => attempts switch
    {
        1 => TimeSpan.FromSeconds(5),
        2 => TimeSpan.FromSeconds(30),
        3 => TimeSpan.FromMinutes(2),
        4 => TimeSpan.FromMinutes(10),
        5 => TimeSpan.FromHours(1),
        _ => TimeSpan.FromHours(6),
    };

    private static string Describe(Exception exception)
        => $"{exception.GetType().Name}: {exception.Message}";

    private async Task MarkSkippedAsync(long id, string reason, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IHeadAlertOutboxStore>();
        await store.MarkSkippedAsync(id, reason, ct).ConfigureAwait(false);
    }

    private async Task MarkFailedAsync(long id, string error, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IHeadAlertOutboxStore>();
        await store.MarkFailedAsync(id, error, ct).ConfigureAwait(false);
    }

    private async Task MarkRetryAsync(long id, DateTime nextAttemptAt, string? error, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IHeadAlertOutboxStore>();
        await store.MarkRetryAsync(id, nextAttemptAt, error, ct).ConfigureAwait(false);
    }
}
