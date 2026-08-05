namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// ينفّذ إجراءً ضمن معاملة قاعدة بيانات واحدة: يُثبَّت عند النجاح ويُتراجع عند أي استثناء.
/// يُستخدم لأي عملية تجارية تتضمن أكثر من حفظ (مثل العملية نفسها + سجل التدقيق)
/// لضمان الذرّية بينهما — إما أن ينجحا معًا أو يُلغيا معًا.
/// </summary>
public interface ITransactionRunner
{
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default);
    Task RunAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}
