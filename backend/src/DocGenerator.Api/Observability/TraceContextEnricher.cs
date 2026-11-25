using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace DocGenerator.Api.Observability;

/// <summary>
/// `P5-01` — يضيف `TraceId`/`SpanId` إلى كل حدث سجل عندما يوجد نشاط حالي
/// (<see cref="Activity.Current"/>). لا يغيّر نص أي رسالة، ولا يضيف شيئًا إطلاقًا
/// عندما يكون الرصد معطَّلًا (لا نشاط في السياق) — فلا أثر على التشغيل العادي.
/// <para>
/// يُستعمل بدل حقن <c>ILogger</c> في أساس كل معالج أمر: المشروع يستخدم `Serilog`
/// مع `Enrich.FromLogContext()`، فالإثراء هو «آلية نطاق السجل» القائمة، وإضافته هنا
/// تغطي كل المعالجات (~200) بلا مساس بمُنشئيها. المعرّفات عشوائية ولا تحمل بيانات.
/// </para>
/// </summary>
public sealed class TraceContextEnricher : ILogEventEnricher
{
    public const string TraceIdPropertyName = "TraceId";

    public const string SpanIdPropertyName = "SpanId";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        var activity = Activity.Current;
        if (activity is null)
            return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            TraceIdPropertyName, activity.TraceId.ToHexString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
            SpanIdPropertyName, activity.SpanId.ToHexString()));
    }
}
