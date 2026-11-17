using DocGenerator.Application.Common;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Health;

/// <summary>
/// RF-016: فاحص جاهزية قاعدة البيانات — اتصال خفيف بلا استعلامات عمل.
///
/// <para>
/// `P3-09`: يقيَّد بمهلة صريحة (`Database:Pool:HealthCheckTimeoutSeconds`) لأن
/// <c>CanConnectAsync</c> قد ينتظر اتصالًا من مجمّع مستنزف حتى مهلة الاتصال الكاملة،
/// بينما منصة النشر تقطع فحص الصحة عند مهلة أقصر فتحسب النسخة ميتة بلا سبب واضح.
/// انتهاء المهلة يعيد <c>Unhealthy</c> بلا كشف تفاصيل داخلية.
/// </para>
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly DocGeneratorDbContext _db;
    private readonly TimeSpan _timeout;

    public DatabaseHealthCheck(DocGeneratorDbContext db, IOptions<DatabaseOptions> options)
    {
        _db = db;
        _timeout = TimeSpan.FromSeconds(Math.Max(1, options.Value.Pool.HealthCheckTimeoutSeconds));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_timeout);

        try
        {
            return await _db.Database.CanConnectAsync(timeout.Token)
                ? HealthCheckResult.Healthy("database reachable")
                : HealthCheckResult.Unhealthy("database unreachable");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // إلغاء من المضيف (إيقاف الخدمة) لا يُحوَّل إلى نتيجة صحة كاذبة.
            throw;
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("database check timed out");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("database check failed", ex);
        }
    }
}
