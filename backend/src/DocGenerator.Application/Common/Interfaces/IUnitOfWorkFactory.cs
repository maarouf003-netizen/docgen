namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// ينفّذ عملية كتابة في نطاق DI جديد لكل محاولة (P0-03b). عند
/// <c>retryable=true</c> تُستخدم إستراتيجية السياق (معيدة عند تفعيلها)، وكل محاولة
/// تحصل على <c>IUnitOfWork</c> وسياق EF جديدين — لذلك يجب أن يعيد الـdelegate
/// تحميل الكيانات من الـ<paramref name="action"/> الممرَّر، لا من نطاق الطلب.
/// </summary>
public interface IUnitOfWorkFactory
{
    Task<T> ExecuteAsync<T>(
        Func<IUnitOfWork, CancellationToken, Task<T>> action,
        bool retryable,
        CancellationToken ct = default);
}
