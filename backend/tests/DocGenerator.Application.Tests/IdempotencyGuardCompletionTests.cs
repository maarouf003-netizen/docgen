using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using Xunit;

namespace DocGenerator.Application.Tests;

/// <summary>
/// W1/B4: إتمام/تحرير حجز عدم التكرار لا يستخدم رمز إلغاء الطلب — وإلا بقي الحجز
/// «قيد المعالجة» 72 ساعة (409) أو أُعيد إنشاء مكرر عند انقطاع اتصال العميل.
/// </summary>
public sealed class IdempotencyGuardCompletionTests
{
    [Fact]
    public async Task CompleteAsync_UsesNonCancelableToken()
    {
        var store = new RecordingStore();
        var ticket = new IdempotencyTicket("key-1", "POST /api/documents", 7);

        await IdempotencyGuard.CompleteAsync(store, ticket, "{\"id\":1}");

        Assert.NotNull(store.CompletedToken);
        Assert.False(store.CompletedToken!.Value.CanBeCanceled);
        Assert.Equal(ticket, store.CompletedTicket);
    }

    [Fact]
    public async Task ReleaseAsync_UsesNonCancelableToken()
    {
        var store = new RecordingStore();
        var ticket = new IdempotencyTicket("key-2", "POST /api/documents", 7);

        await IdempotencyGuard.ReleaseAsync(store, ticket);

        Assert.NotNull(store.ReleasedToken);
        Assert.False(store.ReleasedToken!.Value.CanBeCanceled);
    }

    [Fact]
    public async Task CompleteAsync_SwallowsStoreFailure()
    {
        var store = new RecordingStore { Fail = true };
        var ticket = new IdempotencyTicket("key-3", "POST /api/documents", 7);

        // لا يُفشل العملية الناجحة بسبب تعذّر تخزين لقطة الإعادة.
        await IdempotencyGuard.CompleteAsync(store, ticket, "{}");
    }

    private sealed class RecordingStore : IIdempotencyStore
    {
        public bool Fail { get; init; }
        public CancellationToken? CompletedToken { get; private set; }
        public CancellationToken? ReleasedToken { get; private set; }
        public IdempotencyTicket? CompletedTicket { get; private set; }

        public Task<IdempotencyBegin> BeginAsync(
            string key, string operation, int userId, string fingerprint, DateTime expiresAt,
            CancellationToken ct = default)
            => Task.FromResult(new IdempotencyBegin(new IdempotencyTicket(key, operation, userId), null, false));

        public Task CompleteAsync(
            IdempotencyTicket ticket,
            string responseBody,
            int statusCode = 200,
            string? contentType = null,
            string? responseLocation = null,
            CancellationToken ct = default)
        {
            if (Fail) throw new InvalidOperationException("store down");
            CompletedToken = ct;
            CompletedTicket = ticket;
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(IdempotencyTicket ticket, CancellationToken ct = default)
        {
            ReleasedToken = ct;
            return Task.CompletedTask;
        }
    }
}
