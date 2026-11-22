using System.Collections.Concurrent;
using DocGenerator.Application.Common;
using Microsoft.Extensions.Caching.Memory;

namespace DocGenerator.Infrastructure.Caching;

/// <summary>
/// تنفيذ <see cref="IReferenceDataCache"/> على <see cref="IMemoryCache"/>: كل مجموعة
/// تحمل رقم إصدار، والإبطال يرفع الرقم فتصبح المفاتيح القديمة غير مرئية فورًا (بلا
/// حذف عنيف)، وTTL قصير (3 دقائق) يحدّ أي بقايا عبر النسخ.
/// </summary>
public sealed class ReferenceDataCache : IReferenceDataCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(3);
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, long> _versions = new(StringComparer.Ordinal);

    public ReferenceDataCache(IMemoryCache cache) => _cache = cache;

    public async Task<T> GetOrCreateAsync<T>(
        string group,
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct = default) where T : class
    {
        var version = _versions.GetOrAdd(group, 0);
        var cacheKey = $"refdata:{group}:{version}:{key}";
        if (_cache.TryGetValue(cacheKey, out T? cached) && cached is not null)
            return cached;

        // لا single-flight: تسابق نادر عند أول قراءة بعد إبطال، والمصنع قراءة رخيصة.
        var value = await factory(ct);
        _cache.Set(cacheKey, value, Ttl);
        return value;
    }

    public void Invalidate(string group)
        => _versions.AddOrUpdate(group, 1, static (_, current) => current + 1);
}
