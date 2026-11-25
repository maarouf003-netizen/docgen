namespace DocGenerator.Application.Common.Pipeline;

/// <summary>
/// تجميع سلوكيات الخط حول نواة المعالج — ترتيب التسجيل: الأول خارجي.
/// </summary>
internal static class PipelineExecutor
{
    public static Task<TResponse> ExecuteAsync<TRequest, TResponse>(
        TRequest request,
        Func<TRequest, CancellationToken, Task<TResponse>> core,
        IReadOnlyList<IPipelineBehavior<TRequest, TResponse>> behaviors,
        CancellationToken ct)
    {
        Func<Task<TResponse>> next = () => core(request, ct);
        for (var index = behaviors.Count - 1; index >= 0; index--)
        {
            var inner = next;
            var behavior = behaviors[index];
            next = () => behavior.HandleAsync(request, inner, ct);
        }

        return next();
    }
}

/// <summary>أساس معالج الأوامر: يضمن تنفيذ السلوكيات ثم النواة بالترتيب المعلن.</summary>
public abstract class CommandHandlerBase<TCommand, TResult> : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    private readonly IReadOnlyList<IPipelineBehavior<TCommand, TResult>> _behaviors;

    protected CommandHandlerBase(IEnumerable<IPipelineBehavior<TCommand, TResult>> behaviors)
        => _behaviors = behaviors.ToList();

    protected abstract Task<TResult> HandleCoreAsync(TCommand command, CancellationToken ct);

    public Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default)
    {
        // `P5-01`: بلا مستمع على `ApplicationDiagnostics` يبقى المسار القديم نفسه حرفيًا
        // (بلا نشاط ولا آلة حالة إضافية)، فلا تكلفة على التشغيل غير المرصود.
        if (!PipelineActivity.IsEnabled)
            return PipelineExecutor.ExecuteAsync(command, HandleCoreAsync, _behaviors, ct);

        return PipelineActivity.RunAsync<TCommand, TResult>(
            this,
            () => PipelineExecutor.ExecuteAsync(command, HandleCoreAsync, _behaviors, ct));
    }
}

/// <summary>أساس معالج الاستعلامات — نفس عقد الأوامر بلا فرق سلوكي.</summary>
public abstract class QueryHandlerBase<TQuery, TResult> : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    private readonly IReadOnlyList<IPipelineBehavior<TQuery, TResult>> _behaviors;

    protected QueryHandlerBase(IEnumerable<IPipelineBehavior<TQuery, TResult>> behaviors)
        => _behaviors = behaviors.ToList();

    protected abstract Task<TResult> HandleCoreAsync(TQuery query, CancellationToken ct);

    public Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default)
    {
        // نفس سياسة معالج الأوامر: نشاط اختياري، وصفر تكلفة بلا مستمع.
        if (!PipelineActivity.IsEnabled)
            return PipelineExecutor.ExecuteAsync(query, HandleCoreAsync, _behaviors, ct);

        return PipelineActivity.RunAsync<TQuery, TResult>(
            this,
            () => PipelineExecutor.ExecuteAsync(query, HandleCoreAsync, _behaviors, ct));
    }
}
