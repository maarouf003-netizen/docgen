using DocGenerator.Application.Common.Interfaces;

namespace DocGenerator.Application.Common.Pipeline;

/// <summary>
/// تحقق قبل النواة: يجمع أخطاء كل المدققين المسجّلين للطلب، ويرفض برسالة واحدة
/// عربية (نفس عقد `ArgumentException` القائم) قبل أي أثر جانبي.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IRequestValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IRequestValidator<TRequest>> validators)
        => _validators = validators;

    public Task<TResponse> HandleAsync(TRequest request, Func<Task<TResponse>> next, CancellationToken ct)
    {
        var errors = _validators.SelectMany(v => v.Validate(request)).ToList();
        if (errors.Count > 0)
            throw new ArgumentException(string.Join("، ", errors));
        return next();
    }
}

/// <summary>
/// يفتح معاملة حول النواة فقط للأوامر الموسومة بـ`ITransactionalRequest`؛
/// غيرها يمرّ كما هو (الخدمات الحالية تملك معاملاتها، فلا معاملة متداخلة).
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ITransactionRunner _tx;

    public TransactionBehavior(ITransactionRunner tx) => _tx = tx;

    public async Task<TResponse> HandleAsync(TRequest request, Func<Task<TResponse>> next, CancellationToken ct)
    {
        if (request is not ITransactionalRequest)
            return await next();

        return await _tx.RunAsync(_ => next(), ct);
    }
}

/// <summary>يسجّل تدقيق الطلب بعد نجاحه فقط، للطلبات الموسومة بـ`IAuditedRequest`.</summary>
public sealed class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuditLogger _audit;

    public AuditBehavior(IAuditLogger audit) => _audit = audit;

    public async Task<TResponse> HandleAsync(TRequest request, Func<Task<TResponse>> next, CancellationToken ct)
    {
        var response = await next();
        if (request is IAuditedRequest audited)
        {
            await _audit.LogAsync(audited.AuditActorName, audited.AuditAction, audited.AuditDocumentId,
                audited.AuditDocumentType, audited.AuditDetails, ct);
        }

        return response;
    }
}
