namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// ينفّذ إجراءً ضمن معاملة قاعدة بيانات واحدة: يُثبَّت عند النجاح ويُتراجع عند أي استثناء.
/// يُستخدم لأي عملية تجارية تتضمن أكثر من حفظ (مثل العملية نفسها + سجل التدقيق)
/// لضمان الذرّية بينهما — إما أن ينجحا معًا أو يُلغيا معًا.
///
/// <para><b>عقد إعادة المحاولة (F-02):</b> التنفيذ <b>at-most-once</b> — لا يعيد
/// المُنفِّذ تنفيذ الـdelegate تلقائيًا عند فشل عابر، ولو كان
/// <c>EnableRetryOnFailure</c> مفعّلًا. الـ<c>DbContext</c> مشترك في نطاق الطلب،
/// فإعادة التنفيذ عليه تبقي كيانات/تغييرات المحاولة الفاشلة، والـdelegate قد يملك
/// آثارًا جانبية خارج القاعدة لا تتراجع. كل مستدعٍ يجب أن يكون قادرًا على النجاح
/// في محاولة واحدة بعد قراءة حالته من القاعدة داخل الـdelegate؛ وإعادة المحاولة
/// تكون بطلب/نطاق جديد. (تفصيل القرار في <c>TransactionRunner</c>.)</para>
/// </summary>
public interface ITransactionRunner
{
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default);
    Task RunAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}
