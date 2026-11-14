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
///
/// <para><b>حدود العقد:</b> هذا يخص إعادة تنفيذ الـdelegate كاملًا. قد يعيد EF محاولة
/// <c>SaveChangesAsync</c> الداخلي عبر الإستراتيجية العامة للسياق إن كانت معيدة، لأن
/// <c>StateManager</c> يستخدم إستراتيجية السياق لا إستراتيجية المُنفِّذ المحلية. لذلك
/// لا تُفعّل <c>EnableRetryOnFailure</c> قبل استراتيجية واعية بالمعاملة (تعطّل
/// المحاولة عند وجود <c>CurrentTransaction</c>) أو idempotency لكل عملية كتابة.</para>
/// </summary>
public interface ITransactionRunner
{
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default);
    Task RunAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);

    /// <summary>
    /// يشغّل عملية كتابة في نطاق DI <b>جديد لكل محاولة</b> (P0-03b). عند
    /// <paramref name="retryable"/> = <c>true</c> تُستخدم إستراتيجية السياق
    /// (المعيدة عند تفعيل <c>Database:RetryMaxCount</c>)، وكل محاولة تحصل على
    /// <see cref="IUnitOfWork"/> وسياق EF جديدين — فيجب على الـdelegate إعادة
    /// تحميل الكيانات من الـuow الممرَّر، لا من نطاق الطلب.
    ///
    /// <para>الاستخدام الافتراضي الآمن يبقى <c>retryable=false</c>؛ لا تُفعّل
    /// الإعادة لعملية غير idempotent أو تعتمد على كيانات محمّلة مسبقًا.</para>
    /// </summary>
    Task<T> RunAsync<T>(
        Func<IUnitOfWork, CancellationToken, Task<T>> action,
        bool retryable,
        CancellationToken ct = default);
}
