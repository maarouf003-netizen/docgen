namespace DocGenerator.Application.Common.Pipeline;

/// <summary>أمر كتابة في خط أنابيب `P4-01` — نتيجة واحدة معلنة.</summary>
public interface ICommand<TResult>;

/// <summary>استعلام قراءة في خط أنابيب `P4-01` — نتيجة واحدة معلنة.</summary>
public interface IQuery<TResult>;

/// <summary>معالج أمر واحد: نقطة الكتابة الوحيدة للأمر.</summary>
public interface ICommandHandler<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default);
}

/// <summary>معالج استعلام واحد: نقطة القراءة الوحيدة للاستعلام.</summary>
public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}

/// <summary>
/// سلوك يلتف حول معالج واحد (تحقق/معاملة/تدقيق...) بترتيب التسجيل:
/// الأول المسجَّل هو الخارجي، والأخير هو الأقرب إلى المعالج.
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, Func<Task<TResponse>> next, CancellationToken ct);
}

/// <summary>مدقق طلب نقي (بلا آثار جانبية) يستهلكه `ValidationBehavior`.</summary>
public interface IRequestValidator<in TRequest>
{
    IReadOnlyList<string> Validate(TRequest request);
}

/// <summary>وسم أمر يملك معاملته بنفسه — عندها فقط يفتح `TransactionBehavior` معاملة.</summary>
public interface ITransactionalRequest;

/// <summary>وسم طلب يحتاج تدقيقًا بعد نجاحه — يستهلكه `AuditBehavior`.</summary>
public interface IAuditedRequest
{
    string? AuditActorName { get; }
    string AuditAction { get; }
    int? AuditDocumentId { get; }
    string? AuditDocumentType { get; }
    string? AuditDetails { get; }
}
