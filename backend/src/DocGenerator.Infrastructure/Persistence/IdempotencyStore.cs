using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// مخزن مفاتيح عدم التكرار (RF-011) فوق `DbContext` المشترك للنطاق نفسه —
/// فيرى الحجوزات الملتزَمة فورًا. الحجز التزام مستقل خارج معاملة العملية.
/// كل التعديلات بعد الإدراج الأولي بلا تتبع (`ExecuteUpdate`/`ExecuteDelete`)
/// فلا تلوّث كيانات العملية المتتبَّعة ولا تُعاد محاولة حفظ فاشلة.
/// </summary>
public class IdempotencyStore : IIdempotencyStore
{
    private readonly DocGeneratorDbContext _db;
    private readonly DbExceptionClassifier _errors = new();

    public IdempotencyStore(DocGeneratorDbContext db) => _db = db;

    public async Task<IdempotencyBegin> BeginAsync(
        string key, string operation, int userId, string fingerprint, DateTime expiresAt,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var existing = await _db.IdempotencyKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Key == key && k.Operation == operation && k.UserId == userId, ct);

        if (existing is not null && existing.ExpiresAt > now)
        {
            if (existing.ResponseBody is not null)
            {
                // مكتمل بنفس البصمة = تكرار يُعاد؛ ببصمة مختلفة = نية جديدة
                // تُنفَّذ طازجة (يُستبدَل الصف أدناه).
                if (string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
                    return new IdempotencyBegin(null, existing.ResponseBody, false);
            }
            else if (string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                return new IdempotencyBegin(null, null, true);
            }
            // بصمة مختلفة = نية جديدة على مفتاح قديم: يُستأنَف الحجز طازجًا
            // (فلا تُعاد نتيجة حمولة أخرى، ولا يُرفَض تصحيح-ثم-إعادة-إرسال).
            await _db.IdempotencyKeys
                .Where(k => k.Key == key && k.Operation == operation && k.UserId == userId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(k => k.Fingerprint, fingerprint)
                    .SetProperty(k => k.ResponseBody, (string?)null)
                    .SetProperty(k => k.CreatedAt, now)
                    .SetProperty(k => k.ExpiresAt, expiresAt), ct);
            await CleanupExpiredAsync(ct);
            return new IdempotencyBegin(new IdempotencyTicket(key, operation, userId), null, false);
        }

        if (existing is not null)
        {
            await _db.IdempotencyKeys
                .Where(k => k.Key == key && k.Operation == operation && k.UserId == userId)
                .ExecuteDeleteAsync(ct);
        }

        // لحظة الحجز: الإدراج وحده متتبَّع، وقبل أي كتابة للعملية — فلا شيء آخر معلق.
        _db.IdempotencyKeys.Add(new IdempotencyKey
        {
            Key = key,
            Operation = operation,
            UserId = userId,
            Fingerprint = fingerprint,
            ResponseBody = null,
            CreatedAt = now,
            ExpiresAt = expiresAt,
        });
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (_errors.IsUniqueViolation(ex))
        {
            // سباق حجز متزامن: الفائز التزم قبيلنا — نفضّل القراءة الطازجة له،
            // ونفصل الكيان الفاشل فلا تُعاد محاولة إدراجه مع حفظ العملية لاحقًا.
            foreach (var entry in _db.ChangeTracker.Entries<IdempotencyKey>())
            {
                if (entry.State == EntityState.Added)
                    entry.State = EntityState.Detached;
            }
            var winner = await _db.IdempotencyKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.Key == key && k.Operation == operation && k.UserId == userId, ct);
            if (winner is not null && winner.ExpiresAt > DateTime.UtcNow)
            {
                if (winner.ResponseBody is not null)
                    return new IdempotencyBegin(null, winner.ResponseBody, false);
                return new IdempotencyBegin(null, null, true);
            }
            // لا يُتصوَّر عمليًا (الفائز التزم للتو) — يُعامَل كانشغال آمن.
            return new IdempotencyBegin(null, null, true);
        }

        await CleanupExpiredAsync(ct);
        return new IdempotencyBegin(new IdempotencyTicket(key, operation, userId), null, false);
    }

    public async Task CompleteAsync(IdempotencyTicket ticket, string responseBody, CancellationToken ct = default)
    {
        await _db.IdempotencyKeys
            .Where(k => k.Key == ticket.Key && k.Operation == ticket.Operation && k.UserId == ticket.UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.ResponseBody, responseBody), ct);
    }

    public async Task ReleaseAsync(IdempotencyTicket ticket, CancellationToken ct = default)
    {
        try
        {
            await _db.IdempotencyKeys
                .Where(k => k.Key == ticket.Key && k.Operation == ticket.Operation && k.UserId == ticket.UserId)
                .ExecuteDeleteAsync(ct);
        }
        catch (Exception)
        {
            // التحرير أفضل جهد: بقاء الحجز «قيد المعالجة» حتى انتهائه آمن
            // (التكرار يُرفَض `409` مؤقتًا بدل تنفيذ مكرر) — فلا يُخفي فشل العملية.
        }
    }

    /// <summary>كنس انتهازي للمنتهية عند كل حجز — بلا مهمة خلفية.</summary>
    private async Task CleanupExpiredAsync(CancellationToken ct)
    {
        try
        {
            var now = DateTime.UtcNow;
            await _db.IdempotencyKeys
                .Where(k => k.ExpiresAt <= now)
                .ExecuteDeleteAsync(ct);
        }
        catch (Exception)
        {
            // أفضل جهد — تُعاد المحاولة في الحجز التالي.
        }
    }
}
