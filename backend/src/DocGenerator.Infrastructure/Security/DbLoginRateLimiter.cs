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

    /// <summary>
    /// إدراج مشروط ذرّي: يُدرج السطر فقط إن كان عدد المحاولات الفاشلة للمفتاح ضمن النافذة
    /// أقل من الحد. الفحص والتسجيل في جملة واحدة يزيل سباق TOCTOU بين فحص مبدئي منفصل
    /// وتسجيل لاحق منفصل، لكنه وحده لا يكفي على `PostgreSQL` — يلزمه القفل أدناه.
    /// أسماء الأعمدة مقتبسة بـ "" والمعاملات <c>@p0..@p3</c>.
    /// </summary>
    private const string InsertIfUnderLimitSql =
        "INSERT INTO \"LoginAttempts\" (\"Key\", \"AttemptedAtUtc\") " +
        "SELECT @p0, @p1 " +
        "WHERE (SELECT COUNT(*) FROM \"LoginAttempts\" " +
        "WHERE \"Key\" = @p0 AND \"AttemptedAtUtc\" >= @p2) < @p3;";

    /// <summary>
    /// على `PostgreSQL` الجملة أعلاه وحدها لا تكفي: تحت `READ COMMITTED` يأخذ كل
    /// `INSERT … SELECT` لقطةً عند بدء الجملة، فمحاولتان متزامنتان تقرآن العدّ نفسه
    /// (مثلًا 4) وتُدرجان معًا فتتجاوز الحد. قفل استشاري معاملاتي على المفتاح نفسه
    /// يُسلسل الإدراج للمفتاح الواحد (المفاتيح المختلفة لا تتزاحم)، ويُحرَّر تلقائيًا
    /// بانتهاء المعاملة.
    /// </summary>
    private const string KeyScopedAdvisoryLockSql =
        "SELECT pg_advisory_xact_lock(hashtextextended(@p0, 0));";

    private readonly DocGeneratorDbContext _db;
    private readonly int _maxAttempts;
    private readonly TimeSpan _window;
    private readonly TimeProvider _clock;

    public DbLoginRateLimiter(DocGeneratorDbContext db, IOptions<RateLimitOptions> options, TimeProvider? clock = null)
    {
        _db = db;
        _maxAttempts = Math.Max(1, options.Value.MaxLoginAttempts);
        _window = TimeSpan.FromMinutes(Math.Max(1, options.Value.WindowMinutes));
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<bool> IsAllowedAsync(string key, CancellationToken ct = default)
    {
        var cutoff = _clock.GetUtcNow().UtcDateTime - _window;
        var count = await _db.LoginAttempts.CountAsync(a => a.Key == key && a.AttemptedAtUtc >= cutoff, ct);
        return count < _maxAttempts;
    }

    public async Task<bool> TryRecordFailureAsync(string key, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var cutoff = now - _window;
        var inserted = await RecordUnderKeyLockAsync(key, now, cutoff, ct);

        await PruneOldAsync(ct);
        return inserted == 1;
    }

    private Task<int> ExecuteConditionalInsertAsync(
        string key, DateTime now, DateTime cutoff, CancellationToken ct)
        => _db.Database.ExecuteSqlRawAsync(
            InsertIfUnderLimitSql,
            new object[] { key, now, cutoff, _maxAttempts },
            ct);

    /// <summary>
    /// إدراج محكوم بقفل المفتاح على `PostgreSQL`: القفل المعاملاتي يغطي العدّ والإدراج
    /// معًا، فلا يتجاوز مفتاحٌ واحد الحدَّ مهما تزامنت محاولاته. إن كان هناك معاملة
    /// محيطة يشاركها القفل (بلا معاملة متداخلة)، وإلا تُفتح معاملة قصيرة صريحة.
    /// </summary>
    private async Task<int> RecordUnderKeyLockAsync(
        string key, DateTime now, DateTime cutoff, CancellationToken ct)
    {
        var ownsTransaction = _db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                KeyScopedAdvisoryLockSql,
                new object[] { key },
                ct);
            var inserted = await ExecuteConditionalInsertAsync(key, now, cutoff, ct);

            if (transaction is not null)
                await transaction.CommitAsync(ct);

            return inserted;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    public async Task ResetAsync(string key, CancellationToken ct = default)
    {
        _db.LoginAttempts.RemoveRange(_db.LoginAttempts.Where(a => a.Key == key));
        await _db.SaveChangesAsync(ct);
    }

    private async Task PruneOldAsync(CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
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
