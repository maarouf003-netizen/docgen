using System.Diagnostics.Metrics;
using DocGenerator.Application.Common;
using DocGenerator.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace DocGenerator.Application.Tests;

[CollectionDefinition("ReferenceDataMetrics", DisableParallelization = true)]
public sealed class ReferenceDataMetricsCollection;

/// <summary>
/// التخزين المرجعي: إعادة الاستخدام داخل نفس المجموعة، وإبطال فوري للمجموعة،
/// وعدم تسرّب مفاتيح مجموعة إلى أخرى — بلا اعتماد على قاعدة بيانات.
/// </summary>
[Collection("ReferenceDataMetrics")]
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

    [Fact]
    public async Task GetOrCreate_ConcurrentCalls_ExecuteFactoryOnce()
    {
        var cache = new ReferenceDataCache(new MemoryCache(new MemoryCacheOptions()));
        var calls = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<List<string>> Factory(CancellationToken ct)
        {
            Interlocked.Increment(ref calls);
            entered.TrySetResult();
            await release.Task;
            return ["a"];
        }

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => cache.GetOrCreateAsync("g", "k", Factory))
            .ToArray();

        await entered.Task;
        release.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, calls);
        Assert.All(results, value => Assert.Same(results[0], value));
        Assert.Equal(0, cache.InflightCount);
    }

    [Fact]
    public async Task GetOrCreate_DoesNotBlockOtherKeys()
    {
        var cache = new ReferenceDataCache(new MemoryCache(new MemoryCacheOptions()));
        var releaseA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startedA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var taskA = cache.GetOrCreateAsync("g", "a", async _ =>
        {
            startedA.TrySetResult();
            await releaseA.Task;
            return new List<string> { "a" };
        });

        await startedA.Task;
        // مع بوابة عالمية واحدة كان B سينتظر A؛ المهلة تجعل الطفرة تفشل لا تتعلّق.
        var b = await cache.GetOrCreateAsync(
                "g", "b", _ => Task.FromResult(new List<string> { "b" }))
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("b", Assert.Single(b));
        releaseA.SetResult();
        await taskA;
        Assert.Equal(0, cache.InflightCount);
    }

    [Fact]
    public async Task Metrics_CountOneMissThenHits()
    {
        long hits = 0;
        long misses = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name == ReferenceDataDiagnostics.MeterName)
                current.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == ReferenceDataDiagnostics.HitsInstrument)
                Interlocked.Add(ref hits, measurement);
            else if (instrument.Name == ReferenceDataDiagnostics.MissesInstrument)
                Interlocked.Add(ref misses, measurement);
        });
        listener.Start();

        var cache = new ReferenceDataCache(new MemoryCache(new MemoryCacheOptions()));
        var calls = 0;
        Task<List<string>> Factory(CancellationToken _)
        {
            calls++;
            return Task.FromResult(new List<string> { "a" });
        }

        await cache.GetOrCreateAsync("g", "k", Factory);
        await cache.GetOrCreateAsync("g", "k", Factory);

        Assert.Equal(1, calls);
        Assert.Equal(1, misses);
        Assert.Equal(1, hits);
    }
}
