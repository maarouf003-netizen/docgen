using DocGenerator.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// يشغّل الإجراء ضمن معاملة واحدة على <c>DbContext</c> المسجّل في النطاق نفسه، ويعرض
/// حالة المعاملة الجارية عبر <see cref="ITransactionContext"/> (الخطة §0.1-18).
/// </summary>
public sealed class TransactionRunner : ITransactionRunner, ITransactionContext
{
    private readonly DocGeneratorDbContext _db;

    public TransactionRunner(DocGeneratorDbContext db) => _db = db;

    /// <summary>هلThere معاملة نشطة على هذا النطاق؟</summary>
    public bool HasActiveTransaction => _db.Database.CurrentTransaction is not null;

    /// <summary>معرّف المعاملة النشطة (إن وُجدت) — للتشخيص فقط.</summary>
    public Guid? TransactionId => _db.Database.CurrentTransaction?.TransactionId;

    public Task CreateSavepointAsync(string name, CancellationToken ct = default)
    {
        var transaction = _db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("لا توجد معاملة نشطة لإنشاء نقطة حفظ");
        return transaction.CreateSavepointAsync(name, ct);
    }

    public Task RollbackToSavepointAsync(string name, CancellationToken ct = default)
    {
        var transaction = _db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("لا توجد معاملة نشطة للتراجع إلى نقطة حفظ");
        return transaction.RollbackToSavepointAsync(name, ct);
    }

    public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
        => RunInTransactionAsync(ct, action);

    public Task RunAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
        => RunInTransactionAsync(ct, async token =>
        {
            await action(token);
            return true;
        });

    /// <summary>
    /// يشغّل الإجراء ضمن معاملة واحدة عبر ExecutionStrategy ليظل آمنًا إن فُعّل لاحقًا
    /// EnableRetryOnFailure في PostgreSQL. أي حفظ داخلي (بما فيه حفظ سجل التدقيق)
    /// يلتحم بنفس المعاملة، فيُثبَّت الكل أو يُتراجع الكل.
    /// B1 (قرار تسلسل موثّق): لا معامل عزل هنا عمدًا — التسلسل بين متزامنَي التسطير
    /// يضمنه القيد الفريد لجدول حجوزات الأصول (B3) على العزل الافتراضي، لا Serializable
    /// (الذي لا يغلق السباق على SQLite المؤجَّلة، ويرمي serialization_failure بلا إعادة
    /// محاولة مفعّلة على PostgreSQL).
    /// </summary>
    private async Task<T> RunInTransactionAsync<T>(CancellationToken ct, Func<CancellationToken, Task<T>> action)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
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
        });
    }
}
