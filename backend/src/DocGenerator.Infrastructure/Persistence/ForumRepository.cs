using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

public class ForumRepository : Repository<ForumMessage>, IForumRepository
{
    public ForumRepository(DocGeneratorDbContext db) : base(db) { }

    private IQueryable<ForumMessage> Ordered => Db.ForumMessages
        .AsNoTracking()
        .OrderBy(m => m.CreatedAt)
        .ThenBy(m => m.Id);

    public async Task<List<ForumMessage>> ListSliceAsync(int limit, int? before, int? after, string? q, CancellationToken ct = default)
    {
        var query = Ordered;
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(m => m.Body.Contains(q.Trim()));
        else if (before.HasValue)
            query = query.Where(m => m.Id < before.Value);
        else if (after.HasValue)
            query = query.Where(m => m.Id > after.Value);

        var items = after.HasValue
            // الجديد بعد المؤشر: الأول تصاعديًا (يُسقَف بالحد — الزر يقفز لذيل التيار).
            ? await query.Take(limit).ToListAsync(ct)
            // الذيل الأحدث (تيار أو بحث): الأحدث أولًا ثم القلب للترتيب التصاعدي للعرض.
            : await query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
                .Take(limit).ToListAsync(ct);
        if (!after.HasValue)
            items.Reverse();
        return items;
    }

    public Task<bool> HasOlderAsync(int oldestId, string? q, CancellationToken ct = default)
    {
        var query = Db.ForumMessages.AsNoTracking().Where(m => m.Id < oldestId);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(m => m.Body.Contains(q.Trim()));
        return query.AnyAsync(ct);
    }

    public Task<ForumMessage?> GetPinnedAsync(CancellationToken ct = default)
        => Db.ForumMessages.AsNoTracking().FirstOrDefaultAsync(m => m.IsPinned, ct);

    public Task<ForumMessage?> GetTrackedPinnedAsync(CancellationToken ct = default)
        => Db.ForumMessages.FirstOrDefaultAsync(m => m.IsPinned, ct);

    public Task<ForumMessage?> GetTrackedByIdAsync(int id, CancellationToken ct = default)
        => Db.ForumMessages.FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task<Dictionary<int, int>> CountReadsAsync(IReadOnlyCollection<int> messageIds, CancellationToken ct = default)
    {
        if (messageIds.Count == 0)
            return new Dictionary<int, int>();
        return await Db.ForumMessageReads.AsNoTracking()
            .Where(r => messageIds.Contains(r.MessageId))
            .GroupBy(r => r.MessageId)
            .Select(g => new { MessageId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MessageId, x => x.Count, ct);
    }

    public async Task<int> MaxReadMessageIdAsync(int userId, CancellationToken ct = default)
        => await Db.ForumMessageReads.AsNoTracking()
            .Where(r => r.UserId == userId)
            .MaxAsync(r => (int?)r.MessageId, ct) ?? 0;

    public Task<List<int>> ExistingIdsInRangeAsync(int userId, int fromExclusive, int toInclusive, CancellationToken ct = default)
        => Db.ForumMessages.AsNoTracking()
            .Where(m => m.Id > fromExclusive && m.Id <= toInclusive && m.AuthorId != userId)
            .Select(m => m.Id)
            .ToListAsync(ct);

    public virtual Task<List<int>> ReadIdsAsync(int userId, IReadOnlyCollection<int> messageIds, CancellationToken ct = default)
    {
        if (messageIds.Count == 0)
            return Task.FromResult(new List<int>());
        return Db.ForumMessageReads.AsNoTracking()
            .Where(r => r.UserId == userId && messageIds.Contains(r.MessageId))
            .Select(r => r.MessageId)
            .ToListAsync(ct);
    }

    public Task<int> CountUnreadAsync(int userId, int maxReadId, CancellationToken ct = default)
        => Db.ForumMessages.AsNoTracking().CountAsync(m => m.Id > maxReadId && m.AuthorId != userId, ct);

    public Task<List<ForumMessageRead>> ListReadersAsync(int messageId, CancellationToken ct = default)
        => Db.ForumMessageReads.AsNoTracking()
            .Where(r => r.MessageId == messageId)
            .OrderBy(r => r.ReadAtUtc)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default)
    {
        var stale = Db.ForumMessages.Where(m => m.CreatedAt < cutoff && !m.IsPinned);
        // الاقتباسات ظلال: تُصفَّر المراجع إلى المحذوف وتبقى اللقطات —
        // الحذف الخام (`ExecuteDelete`) لا يُفعّل `SetNull` تلقائيًا.
        await Db.ForumMessages
            .Where(m => m.QuotedMessageId != null && stale.Any(s => s.Id == m.QuotedMessageId))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.QuotedMessageId, (int?)null), ct);
        await Db.ForumMessageReads
            .Where(r => stale.Any(m => m.Id == r.MessageId))
            .ExecuteDeleteAsync(ct);
        var deleted = await stale.ExecuteDeleteAsync(ct);
        // الحذف الخام يتجاوز متعقّب التغييرات: أي كيانات محمّلة في النطاق نفسه
        // (المحذوفة والمقتبِسة) تبقى نسخًا قديمة — التفريغ يمنع قراءتها لاحقًا.
        Db.ChangeTracker.Clear();
        return deleted;
    }

    public async Task<bool> DeleteMessageAsync(int messageId, CancellationToken ct = default)
    {
        await Db.ForumMessages
            .Where(m => m.QuotedMessageId == messageId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.QuotedMessageId, (int?)null), ct);
        await Db.ForumMessageReads
            .Where(r => r.MessageId == messageId)
            .ExecuteDeleteAsync(ct);
        var deleted = await Db.ForumMessages
            .Where(m => m.Id == messageId)
            .ExecuteDeleteAsync(ct) > 0;
        // كأعلاه: التفريغ يمنع بقاء النسخ المتعقّبة القديمة في النطاق نفسه.
        Db.ChangeTracker.Clear();
        return deleted;
    }
}
