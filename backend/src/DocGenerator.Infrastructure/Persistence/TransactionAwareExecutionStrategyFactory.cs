using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// إستراتيجية واعية بالمعاملة (P0-03a): تُغلِّف إستراتيجية غير معيدة وأخرى معيدة،
/// وتختار **عند التنفيذ** بحسب وجود <c>CurrentTransaction</c>. لا تفحص المعاملة
/// داخل <see cref="Create"/> عمدًا: قراءة <c>Database.CurrentTransaction</c> هناك
/// تعيد حلّ <c>DatabaseFacadeDependencies</c> الذي يطلب هذا المصنع نفسه (دورة حلّ).
///
/// <para>هذا لا يعيد الـdelegate الكامل؛ إعادة المحاولة الآمنة للـdelegate تتطلب
/// <see cref="UnitOfWorkFactory"/> ونطاق DI جديدًا لكل محاولة (P0-03b).</para>
/// </summary>
public class TransactionAwareExecutionStrategyFactory : IExecutionStrategyFactory
{
    private readonly ExecutionStrategyDependencies _dependencies;

    public TransactionAwareExecutionStrategyFactory(ExecutionStrategyDependencies dependencies)
        => _dependencies = dependencies;

    /// <summary>قابل للتجاوز في الاختبارات لفرض إعدادات دون تغيير الحالة العامة.</summary>
    protected virtual DatabaseRetrySettings.RetrySettings RetrySettings
        => DatabaseRetrySettings.Current;

    /// <summary>تبعيات EF — متاحة لتجاوزات الاختبار لإنشاء إستراتيجية بديلة.</summary>
    protected ExecutionStrategyDependencies Dependencies => _dependencies;

    public IExecutionStrategy Create()
    {
        var nonRetrying = new NonRetryingExecutionStrategy(_dependencies);
        var settings = RetrySettings;
        var retrying = settings.MaxRetryCount > 0
            ? CreateRetryingStrategy(settings)
            : null;

        return new TransactionAwareExecutionStrategy(_dependencies, nonRetrying, retrying);
    }

    /// <summary>قابل للتجاوز في الاختبارات لتفادي مزوّد Npgsql الحقيقي.</summary>
    protected virtual IExecutionStrategy CreateRetryingStrategy(
        DatabaseRetrySettings.RetrySettings settings)
        => new NpgsqlRetryingExecutionStrategy(
            _dependencies,
            settings.MaxRetryCount,
            TimeSpan.FromSeconds(settings.MaxRetryDelaySeconds),
            errorCodesToAdd: null);
}

/// <summary>
/// تختار الإستراتيجية غير المعيدة عند وجود معاملة نشطة، والمعيدة خارجها.
/// فحص المعاملة يقع في <see cref="ExecuteAsync{TState,TResult}"/> (وقت التنفيذ)،
/// حيث يكون <c>DatabaseFacade</c> مبنيًا بالفعل.
/// </summary>
internal sealed class TransactionAwareExecutionStrategy : IExecutionStrategy
{
    private readonly ExecutionStrategyDependencies _dependencies;
    private readonly IExecutionStrategy _nonRetrying;
    private readonly IExecutionStrategy? _retrying;

    public TransactionAwareExecutionStrategy(
        ExecutionStrategyDependencies dependencies,
        IExecutionStrategy nonRetrying,
        IExecutionStrategy? retrying)
    {
        _dependencies = dependencies;
        _nonRetrying = nonRetrying;
        _retrying = retrying;
    }

    // لا تفحص CurrentTransaction هنا: قد تُقرأ أثناء بناء DatabaseFacade.
    // الاختيار الفعلي يقع في Current عند التنفيذ فقط.
    public bool RetriesOnFailure => _retrying is not null;

    private IExecutionStrategy Current
        => _retrying is not null
           && _dependencies.CurrentContext.Context.Database.CurrentTransaction is null
            ? _retrying
            : _nonRetrying;

    public TResult Execute<TState, TResult>(
        TState state,
        Func<DbContext, TState, TResult> operation,
        Func<DbContext, TState, ExecutionResult<TResult>>? verifySucceeded)
        => Current.Execute(state, operation, verifySucceeded);

    public Task<TResult> ExecuteAsync<TState, TResult>(
        TState state,
        Func<DbContext, TState, CancellationToken, Task<TResult>> operation,
        Func<DbContext, TState, CancellationToken, Task<ExecutionResult<TResult>>>? verifySucceeded,
        CancellationToken cancellationToken = default)
        => Current.ExecuteAsync(state, operation, verifySucceeded, cancellationToken);
}
