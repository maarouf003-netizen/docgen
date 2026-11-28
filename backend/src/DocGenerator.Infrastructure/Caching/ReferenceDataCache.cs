using System.Collections.Concurrent;
using DocGenerator.Application.Common;
using Microsoft.Extensions.Caching.Memory;

namespace DocGenerator.Infrastructure.Caching;

/// <summary>
/// تنفيذ <see cref="IReferenceDataCache"/> على <see cref="IMemoryCache"/>: كل مجموعة
/// تحمل رقم إصدار، والإبطال يرفع الرقم فتصبح المفاتيح القديمة غير مرئية فورًا (بلا
/// حذف عنيف)، وTTL قصير (3 دقائق) يحدّ أي بقايا عبر النسخ.
/// <para>
/// `P3-06b`: `single-flight` لكل مفتاح (بوابة واحدة) حتى لا يتكرر التحميل البارد عند
/// تزامن الطلبات، وعدّادات إصابة/فقد على متر مستقل (`ReferenceDataDiagnostics`).
/// </para>
/// </summary>
public sealed class ReferenceDataCache : IReferenceDataCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(3);
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, long> _versions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public ReferenceDataCache(IMemoryCache cache) => _cache = cache;

    public async Task<T> GetOrCreateAsync<T>(
        string group,
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken ct = default) where T : class
    {
        var version = _versions.GetOrAdd(group, 0);
        var cacheKey = $"refdata:{group}:{version}:{key}";
        if (TryGetCached(cacheKey, out T? cached))
            return cached!;

        var gate = _locks.GetOrAdd(cacheKey, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            // فحص مزدوج بعد البوابة: طلب متزامن سبقنا فأنشأ القيمة.
            if (TryGetCached(cacheKey, out cached))
                return cached!;

            ReferenceDataDiagnostics.Misses.Add(1);
            var value = await factory(ct);
            _cache.Set(cacheKey, value, Ttl);
            return value;
        }
        finally
        {
            gate.Release();
            _locks.TryRemove(cacheKey, out _);
        }
    }

    public void Invalidate(string group)
        => _versions.AddOrUpdate(group, 1, static (_, current) => current + 1);

    /// <summary>عدد بوابات التحميل الجارية — للاختبار فقط (يجب أن يعود صفرًا).</summary>
    internal int InflightCount => _locks.Count;

    private bool TryGetCached<T>(string cacheKey, out T? value) where T : class
    {
        if (_cache.TryGetValue(cacheKey, out value) && value is not null)
        {
            ReferenceDataDiagnostics.Hits.Add(1);
            return true;
        }

        value = null;
        return false;
    }
}
