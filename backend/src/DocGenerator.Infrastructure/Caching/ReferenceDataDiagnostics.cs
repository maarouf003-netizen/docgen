using System.Diagnostics.Metrics;

namespace DocGenerator.Infrastructure.Caching;

/// <summary>
/// `P3-06b` — عدّادات كاش البيانات المرجعية (إصابة/فقد) لتغذية قياس نسبة الإصابة
/// المطلوبة في `P3-06`. لا تُسجِّل مفاتيح/قيمًا (لا PII)؛ عدّادات فقط.
/// </summary>
public static class ReferenceDataDiagnostics
{
    public const string MeterName = "DocGenerator.ReferenceData";

    public const string HitsInstrument = "docgen.refdata.cache.hits";
    public const string MissesInstrument = "docgen.refdata.cache.misses";

    internal static readonly Meter Meter = new(MeterName);

    internal static readonly Counter<long> Hits =
        Meter.CreateCounter<long>(HitsInstrument, unit: "{request}", description: "Reference-data cache hits.");

    internal static readonly Counter<long> Misses =
        Meter.CreateCounter<long>(MissesInstrument, unit: "{request}", description: "Reference-data cache misses (factory executions).");
}
