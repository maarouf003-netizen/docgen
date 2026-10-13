using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.Time.Testing;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات مخزن عدم التكرار (RF-011) على ساعة مزيفة — تثبت أن R0-3 جعل
/// `IdempotencyStore` يقرأ `TimeProvider` المحقون لا `DateTime.UtcNow`:
/// التقديم المزيف للساعة يُسقط الحجز القديم حتميًا بلا انتظار حقيقي.
/// </summary>
public sealed class IdempotencyStoreTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly DocGeneratorDbContext _db;
    private readonly FakeTimeProvider _clock;
    private readonly IdempotencyStore _store;

    public IdempotencyStoreTests()
    {
        _db = TestDb.Create();
        _clock = new FakeTimeProvider(T0);
        _store = new IdempotencyStore(_db, _clock);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task BeginAsync_FreshKey_ReturnsTicket()
    {
        var begin = await _store.BeginAsync("k1", "op", 7, "fp", _clock.GetUtcNow().UtcDateTime.AddMinutes(5));

        Assert.NotNull(begin.Ticket);
        Assert.False(begin.Busy);
        Assert.Null(begin.ReplayBody);
    }

    [Fact]
    public async Task BeginAsync_SameFingerprintWhilePending_ReturnsBusy()
    {
        var expires = _clock.GetUtcNow().UtcDateTime.AddMinutes(5);
        var first = await _store.BeginAsync("k1", "op", 7, "fp", expires);

        Assert.NotNull(first.Ticket);

        var second = await _store.BeginAsync("k1", "op", 7, "fp", expires);

        Assert.Null(second.Ticket);
        Assert.True(second.Busy);
        Assert.Null(second.ReplayBody);
    }

    [Fact]
    public async Task BeginAsync_AfterExpiry_ReturnsFreshTicket()
    {
        var first = await _store.BeginAsync("k1", "op", 7, "fp", _clock.GetUtcNow().UtcDateTime.AddMinutes(5));

        Assert.NotNull(first.Ticket);

        _clock.Advance(TimeSpan.FromMinutes(10));

        var second = await _store.BeginAsync("k1", "op", 7, "fp", _clock.GetUtcNow().UtcDateTime.AddMinutes(5));

        Assert.NotNull(second.Ticket);
        Assert.False(second.Busy);
    }

    [Fact]
    public async Task BeginAsync_CompletedKey_ReplaysBody()
    {
        var first = await _store.BeginAsync("k1", "op", 7, "fp", _clock.GetUtcNow().UtcDateTime.AddMinutes(5));

        Assert.NotNull(first.Ticket);
        await _store.CompleteAsync(first.Ticket!, "{\"ok\":true}");

        var replay = await _store.BeginAsync("k1", "op", 7, "fp", _clock.GetUtcNow().UtcDateTime.AddMinutes(5));

        Assert.Null(replay.Ticket);
        Assert.False(replay.Busy);
        Assert.Equal("{\"ok\":true}", replay.ReplayBody);
    }
}
