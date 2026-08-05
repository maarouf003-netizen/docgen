using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DocGenerator.Infrastructure.Security;

/// <summary>
/// محدد محاولات الدخول المعتمد على قاعدة البيانات: عدّاد مشترك بين العقد
/// (كل عقدة تكتب/تقرأ نفس الجدول LoginAttempts). يطابق سلوك LoginRateLimiter
/// في تطبيق Flask المرجعي (5 محاولات فاشلة خلال 5 دقائق لكل مفتاح IP+username).
/// التنظيف الدوري (مرة كل 5 دقائق) محمي بفحص مزدوج مع قفل قصير لضمان سلامة الخيوط
/// رغم كون الخدمة scoped والحقل static مشتركًا بين الطلبات.
/// </summary>
public sealed class DbLoginRateLimiter : ILoginRateLimiter
{
    private static readonly TimeSpan PruneEvery = TimeSpan.FromMinutes(5);
    private static readonly object PruneLock = new();
    private static long _lastPruneTicks = DateTime.MinValue.Ticks;

    private readonly DocGeneratorDbContext _db;
    private readonly int _maxAttempts;
    private readonly TimeSpan _window;

    public DbLoginRateLimiter(DocGeneratorDbContext db, IOptions<RateLimitOptions> options)
    {
        _db = db;
        _maxAttempts = Math.Max(1, options.Value.MaxLoginAttempts);
        _window = TimeSpan.FromMinutes(Math.Max(1, options.Value.WindowMinutes));
    }

    public async Task<bool> IsAllowedAsync(string key, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - _window;
        var count = await _db.LoginAttempts.CountAsync(a => a.Key == key && a.AttemptedAtUtc >= cutoff, ct);
        return count < _maxAttempts;
    }

    public async Task RecordFailureAsync(string key, CancellationToken ct = default)
    {
        _db.LoginAttempts.Add(new LoginAttempt { Key = key, AttemptedAtUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync(ct);
        await PruneOldAsync(ct);
    }

    public async Task ResetAsync(string key, CancellationToken ct = default)
    {
        _db.LoginAttempts.RemoveRange(_db.LoginAttempts.Where(a => a.Key == key));
        await _db.SaveChangesAsync(ct);
    }

    private async Task PruneOldAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (now.Ticks - Interlocked.Read(ref _lastPruneTicks) < PruneEvery.Ticks)
            return;

        lock (PruneLock)
        {
            if (now.Ticks - Interlocked.Read(ref _lastPruneTicks) < PruneEvery.Ticks)
                return;
            Interlocked.Exchange(ref _lastPruneTicks, now.Ticks);
        }

        var cutoff = now - _window;
        _db.LoginAttempts.RemoveRange(_db.LoginAttempts.Where(a => a.AttemptedAtUtc < cutoff));
        await _db.SaveChangesAsync(ct);
    }
}
