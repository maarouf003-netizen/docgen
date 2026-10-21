using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// قفل «نسخة واحدة» لمشغّل صندوق الصادر (الخطة §0.1-15 و§16.1).
///
/// <list type="bullet">
/// <item>على <c>PostgreSQL</c>: قفل استشاري جلسي (<c>pg_try_advisory_lock</c>) على اتصال
/// مخصص — يمنع معالجة الصف نفسه من نسختين.</item>
/// <item>على <c>SQLite</c>: قفل على مستوى العملية (<c>SemaphoreSlim</c>) — ولا يحمي
/// نسخًا على مضيفين مختلفين.</item>
/// </list>
///
/// **قيد النسخة الواحدة تشغيلي:** النشر متعدد النسخ يتطلب <c>PostgreSQL</c> + هذا القفل
/// الاستشاري (موثَّق في <c>RUN_GUIDE.md</c>).
/// </summary>
public sealed class HeadAlertOutboxLock : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly bool _usePostgres;
    private readonly string _connectionString;
    private readonly ILogger<HeadAlertOutboxLock> _logger;
    private DbConnection? _advisoryConnection;
    private bool _held;

    public HeadAlertOutboxLock(
        string connectionString,
        bool usePostgres,
        ILogger<HeadAlertOutboxLock> logger)
    {
        _connectionString = connectionString;
        _usePostgres = usePostgres;
        _logger = logger;
    }

    /// <summary>هل القفل محمَّل الآن؟</summary>
    public bool IsHeld => _held;

    /// <summary>
    /// يحاول حيازة القفل بلا انتظار. يُرجع <c>false</c> عند التعذّر (نسخة أخرى نشطة)
    /// فلا تُعالَج رسالة موازية.
    /// </summary>
    public async Task<bool> TryAcquireAsync(CancellationToken ct = default)
    {
        if (!await _semaphore.WaitAsync(0, ct).ConfigureAwait(false))
            return false;

        try
        {
            if (_usePostgres && !await TryAcquireAdvisoryLockAsync(ct).ConfigureAwait(false))
            {
                _semaphore.Release();
                return false;
            }

            _held = true;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "تعذّر حيازة قفل صندوق الصادر — تُؤجَّل المعالجة.");
            _semaphore.Release();
            return false;
        }
    }

    /// <summary>يحرّر القفل (يُستدعى عند إيقاف الخدمة أو بعد كل دورة).</summary>
    public async Task ReleaseAsync()
    {
        if (!_held)
            return;

        _held = false;

        if (_usePostgres && _advisoryConnection is not null)
        {
            try
            {
                await using var command = _advisoryConnection.CreateCommand();
                command.CommandText = "SELECT pg_advisory_unlock(912736451)";
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "تعذّر تحرير القفل الاستشاري للجلسة.");
            }
        }

        _semaphore.Release();
    }

    private async Task<bool> TryAcquireAdvisoryLockAsync(CancellationToken ct)
    {
        _advisoryConnection ??= new NpgsqlConnection(_connectionString);
        if (_advisoryConnection.State != ConnectionState.Open)
            await _advisoryConnection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = _advisoryConnection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(912736451)";
        var result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is true;
    }

    public void Dispose()
    {
        if (_advisoryConnection is not null)
        {
            try { _advisoryConnection.Dispose(); } catch { /* best effort */ }
            _advisoryConnection = null;
        }

        _semaphore.Dispose();
    }
}
