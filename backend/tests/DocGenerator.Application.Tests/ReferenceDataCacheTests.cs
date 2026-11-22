using DocGenerator.Application.Common;
using DocGenerator.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace DocGenerator.Application.Tests;

/// <summary>
/// التخزين المرجعي: إعادة الاستخدام داخل نفس المجموعة، وإبطال فوري للمجموعة،
/// وعدم تسرّب مفاتيح مجموعة إلى أخرى — بلا اعتماد على قاعدة بيانات.
/// </summary>
public class ReferenceDataCacheTests
{
    [Fact]
    public async Task GetOrCreate_ReusesValue_UntilInvalidated()
    {
        var cache = new ReferenceDataCache(new MemoryCache(new MemoryCacheOptions()));
        var calls = 0;
        Task<List<string>> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult(new List<string> { "a" });
        }

        var first = await cache.GetOrCreateAsync("g", "k", Factory);
        var second = await cache.GetOrCreateAsync("g", "k", Factory);
        Assert.Same(first, second);
        Assert.Equal(1, calls);

        cache.Invalidate("g");
        var third = await cache.GetOrCreateAsync("g", "k", Factory);
        Assert.NotSame(first, third);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Groups_AreIsolated()
    {
        var cache = new ReferenceDataCache(new MemoryCache(new MemoryCacheOptions()));
        var calls = 0;
        Task<List<int>> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult(new List<int> { calls });
        }

        await cache.GetOrCreateAsync("one", "k", Factory);
        await cache.GetOrCreateAsync("two", "k", Factory);
        cache.Invalidate("one");
        await cache.GetOrCreateAsync("one", "k", Factory);
        await cache.GetOrCreateAsync("two", "k", Factory);

        // one: إنشاء + بعد الإبطال = 2، two: إنشاء واحد = 1.
        Assert.Equal(3, calls);
    }
}
