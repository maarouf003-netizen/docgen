namespace DocGenerator.Api.Health;

using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// RF-016: فاحص جاهزية قاعدة البيانات — اتصال خفيف بلا استعلامات عمل.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly DocGeneratorDbContext _db;

    public DatabaseHealthCheck(DocGeneratorDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(ct)
                ? HealthCheckResult.Healthy("database reachable")
                : HealthCheckResult.Unhealthy("database unreachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("database check failed", ex);
        }
    }
}
