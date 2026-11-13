using DocGenerator.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

    /// <summary>
    /// قفل نطاق داخل المعاملة: في PostgreSQL قفل استشاري على مفتاح مستمد من النص
    /// (يُحرَّر تلقائيًا عند نهاية المعاملة)، وفي SQLite لا شيء (كاتب واحد).
    /// لا يفعل شيئًا بلا معاملة نشطة — والمسارات الملزمة تفتح معاملة قبل الاستدعاء.
    /// </summary>
    public async Task AcquireScopeLockAsync(string key, CancellationToken ct = default)
    {
        if (!_db.Database.IsNpgsql() || _db.Database.CurrentTransaction is null)
            return;
        await _db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(hashtextextended({0}, 0))",
            new object[] { key },
            ct);
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
    /// يشغّل الإجراء ضمن معاملة واحدة. أي حفظ داخلي (بما فيه حفظ سجل التدقيق) يلتحم
    /// بنفس المعاملة، فيُثبَّت الكل أو يُتراجع الكل.
    ///
    /// <para><b>عقد إعادة المحاولة (F-02 — at-most-once):</b> لا يُعاد تنفيذ الإجراء
    /// تلقائيًا أبدًا، حتى لو كان <c>EnableRetryOnFailure</c> مفعّلًا على المزوّد.
    /// السبب مزدوج: (1) الـ<c>DbContext</c> مسجّل في نطاق الطلب نفسه، فتبقى فيه كيانات
    /// المجموعات المحمّلة وتغييرات المحاولة الفاشلة، وإعادة التنفيذ عليه تُكرّر
    /// الإدراج أو تُبقي حالة قديمة تُبنى عليها قرارات خاطئة؛ (2) الإجراء نفسه قد يحمل
    /// آثارًا جانبية غير idempotent (ملفات، تدقيق، صندوق صادر) لا تتراجع مع المعاملة.
    /// إعادة المحاولة الآمنة تتطلب سياقًا/نطاق DI جديدًا لكل محاولة وإجراءات idempotent
    /// — وهي ترقية معمارية مستقلة، لا تُحاكى بـ<c>ClearChanges</c> لأن تفريغ المتتبَّع
    /// يفصل كيانات محمّلة قبل المعاملة فتُفقد تعديلاتها صامتة.
    /// لذلك الفشل العابر يظهر للمستخدم، وإعادة المحاولة تكون بنطاق جديد.</para>
    ///
    /// B1 (قرار تسلسل موثّق): لا معامل عزل هنا عمدًا — التسلسل بين متزامنَي التسطير
    /// يضمنه القيد الفريد لجدول حجوزات الأصول (B3) على العزل الافتراضي، لا Serializable
    /// (الذي لا يغلق السباق على SQLite المؤجَّلة، ويرمي serialization_failure بلا إعادة
    /// محاولة مفعّلة على PostgreSQL).
    /// </summary>
    private async Task<T> RunInTransactionAsync<T>(CancellationToken ct, Func<CancellationToken, Task<T>> action)
    {
        // F-02: نستخدم الإستراتيجية المضبوطة إن كانت غير معيدة للمحاولة (الوضع الحالي)،
        // ونستبدلها صراحةً بـNonRetryingExecutionStrategy إن فُعّل EnableRetryOnFailure
        // لاحقًا — فلا تعيد المحاولة على معاملة تفاعلية أبدًا.
        var configured = _db.Database.CreateExecutionStrategy();
        var strategy = configured.RetriesOnFailure
            ? new NonRetryingExecutionStrategy(_db)
            : configured;

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
