using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// مخزن صندوق الصادر (الخطة §8.2) — يعمل على <c>DbContext</c> المسجّل في النطاق نفسه،
/// فالتسجيل يقع في معاملة العمل نفسها (ذرّية)، والمعالجة تفتح معاملتها الخاصة عبر
/// <see cref="ExecuteInTransactionAsync"/> (لا تتداخل مع معاملة عمل).
/// </summary>
public sealed class HeadAlertOutboxStore : IHeadAlertOutboxStore
{
    private readonly DocGeneratorDbContext _db;

    public HeadAlertOutboxStore(DocGeneratorDbContext db) => _db = db;

    public void Enqueue(HeadAlertOutboxMessage message) => _db.HeadAlertOutboxMessages.Add(message);

    public async Task<HeadAlertOutboxMessage?> ClaimNextAsync(
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var candidate = await ClaimCandidateAsync(now, ct);
        if (candidate is null)
            return null;

        candidate.Status = HeadAlertOutboxStatus.Processing;
        candidate.Attempts += 1;
        candidate.LockedUntil = now.Add(leaseDuration);
        candidate.LockedBy = workerId;
        candidate.ProcessedAt = null;

        await _db.SaveChangesAsync(ct);
        return candidate;
    }

    /// <summary>
    /// يلتقط المرشّح: صف واحد بالترتيب <c>Id ASC</c>. على PostgreSQL يُضاف
    /// <c>FOR UPDATE SKIP LOCKED</c> كطبقة إضافية فوق قفل المعاملة (فلا يلتقط مشغّلان
    /// الصف نفسه)؛ وعلى SQLite تكفي معاملة الكتابة + القفل على مستوى العملية.
    /// </summary>
    private async Task<HeadAlertOutboxMessage?> ClaimCandidateAsync(DateTime now, CancellationToken ct)
    {
        if (_db.Database.IsNpgsql())
        {
            var sql = """
                SELECT * FROM "HeadAlertOutboxMessages"
                WHERE ("Status" = 'pending' AND "NextAttemptAt" <= {0})
                   OR ("Status" = 'processing' AND "LockedUntil" IS NOT NULL AND "LockedUntil" <= {0})
                ORDER BY "Id" ASC
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """;
            var rows = await _db.HeadAlertOutboxMessages
                .FromSqlRaw(sql, now)
                .ToListAsync(ct);
            return rows.FirstOrDefault();
        }

        return await _db.HeadAlertOutboxMessages
            .Where(m => (m.Status == HeadAlertOutboxStatus.Pending && m.NextAttemptAt <= now)
                     || (m.Status == HeadAlertOutboxStatus.Processing
                         && m.LockedUntil != null && m.LockedUntil <= now))
            .OrderBy(m => m.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task MarkProcessedAsync(long id, CancellationToken ct = default)
    {
        var message = await FindAsync(id, ct);
        if (message is null)
            return;

        message.Status = HeadAlertOutboxStatus.Processed;
        message.ProcessedAt = DateTime.UtcNow;
        message.LockedUntil = null;
        message.LastError = null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkSkippedAsync(long id, string reason, CancellationToken ct = default)
    {
        var message = await FindAsync(id, ct);
        if (message is null)
            return;

        message.Status = HeadAlertOutboxStatus.Skipped;
        message.ProcessedAt = DateTime.UtcNow;
        message.LockedUntil = null;
        message.LastError = Truncate(reason);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkRetryAsync(long id, DateTime nextAttemptAt, string? error, CancellationToken ct = default)
    {
        var message = await FindAsync(id, ct);
        if (message is null)
            return;

        message.Status = HeadAlertOutboxStatus.Pending;
        message.NextAttemptAt = nextAttemptAt;
        message.LockedUntil = null;
        message.LastError = Truncate(error);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(long id, string error, CancellationToken ct = default)
    {
        var message = await FindAsync(id, ct);
        if (message is null)
            return;

        message.Status = HeadAlertOutboxStatus.Failed;
        message.LockedUntil = null;
        message.LastError = Truncate(error);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> UpdateTargetUserForSuccessionAsync(
        int predecessorId,
        int successorId,
        CancellationToken ct = default)
    {
        // صفوف `pending` فقط: صف `processing` جارٍ معالجته فإعادة توجيهه تسبّب تعارضًا،
        // و`processed`/`failed` انتهت فلا معنى لإعادة توجيهها.
        var rows = await _db.HeadAlertOutboxMessages
            .Where(m => m.Status == HeadAlertOutboxStatus.Pending && m.TargetUserId == predecessorId)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return 0;

        foreach (var row in rows)
            row.TargetUserId = successorId;

        await _db.SaveChangesAsync(ct);
        return rows.Count;
    }

    public async Task<int> CleanupAsync(
        DateTime now,
        int processedRetentionDays,
        int skippedRetentionDays,
        CancellationToken ct = default)
    {
        var processedBefore = now.AddDays(-processedRetentionDays);
        var skippedBefore = now.AddDays(-skippedRetentionDays);

        var expired = await _db.HeadAlertOutboxMessages
            .Where(m => (m.Status == HeadAlertOutboxStatus.Processed
                         && m.ProcessedAt != null && m.ProcessedAt <= processedBefore)
                     || (m.Status == HeadAlertOutboxStatus.Skipped
                         && m.ProcessedAt != null && m.ProcessedAt <= skippedBefore))
            .ToListAsync(ct);

        if (expired.Count == 0)
            return 0;

        _db.HeadAlertOutboxMessages.RemoveRange(expired);
        await _db.SaveChangesAsync(ct);
        return expired.Count;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken ct = default)
    {
        // معاملة صريحة بلا ExecutionStrategy: التفعيل المستقبلي لـEnableRetryOnFailure
        // يعيد «المعاملات التي يبدؤها المستخدم» غير مدعومة، فنبقي المعاملة الشفافة.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await action(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private Task<HeadAlertOutboxMessage?> FindAsync(long id, CancellationToken ct)
        => _db.HeadAlertOutboxMessages.FirstOrDefaultAsync(m => m.Id == id, ct);

    /// <summary>
    /// يقصّ سبب الخطأ إلى 2000 محرف (بلا نص التنبيه الكامل ولا بيانات حساسة) — الحارس
    /// على حجم عمود <c>LastError</c> وعلى عدم تسريب نص كامل.
    /// </summary>
    private static string? Truncate(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return null;

        const int maxLength = 2000;
        var text = error.Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
