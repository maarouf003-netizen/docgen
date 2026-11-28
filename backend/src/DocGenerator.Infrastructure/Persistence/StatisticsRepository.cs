using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// تجميع العدادات في قاعدة البيانات عبر GroupBy/Count؛
/// وجمع المبالغ عميل-side بالنوع decimal للحفاظ على الدقة دون Sum(double).
/// </summary>
public partial class StatisticsRepository : IStatisticsRepository
{
    private readonly DocGeneratorDbContext _db;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public StatisticsRepository(DocGeneratorDbContext db, TimeProvider clock, TimeZoneInfo timeZone)
    {
        _db = db;
        _clock = clock;
        _timeZone = timeZone;
    }

    /// <summary>العملات المعروفة في شاشة الإحصاءات بترتيب العرض الثابت.</summary>
    private static readonly string[] KnownCurrencies =
        { "ليرة سورية", "دولار أمريكي", "يورو" };

    /// <summary>سلة العملات خارج المعروفة (`BQ-030`): تُجمَّع هنا مع تنبيه مراجعة بدل إسقاطها بصمت.</summary>
    private const string OtherCurrencyBucket = "أخرى";

    private static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? "ليرة سورية" : currency.Trim();

    /// <summary>
    /// يُضيف مبلغًا لسلة عملته متجاهلًا الصفر والغائب؛
    /// والعملات خارج المعروفة تُجمَّع في سلة «أخرى» (`BQ-030`) لمراجعتها بدل إسقاطها بصمت.
    /// </summary>
    private static void AddAmount(Dictionary<string, decimal> buckets, string? currency, decimal? amount)
    {
        if (amount == null || amount.Value == 0)
            return;
        var key = NormalizeCurrency(currency);
        if (!KnownCurrencies.Contains(key))
            key = OtherCurrencyBucket;
        buckets[key] = buckets.TryGetValue(key, out var current) ? current + amount.Value : amount.Value;
    }

    /// <summary>السلات بالترتيب الثابت المعروف ثم «أخرى» أخيرًا، وتستبعد العملات غير المجمّعة (صفرية).</summary>
    private static List<CurrencyAmountDto> ToCurrencyAmounts(Dictionary<string, decimal> buckets) =>
        KnownCurrencies
            .Where(buckets.ContainsKey)
            .Select(c => new CurrencyAmountDto(c, buckets[c]))
            .Concat(buckets.ContainsKey(OtherCurrencyBucket)
                ? new[] { new CurrencyAmountDto(OtherCurrencyBucket, buckets[OtherCurrencyBucket]) }
                : Array.Empty<CurrencyAmountDto>())
            .ToList();


    /// <summary>
    /// قيد نطاق المالك (§12/قرار 27): الفرع ككل عند `fullAccess` (إدارة)، وإلا
    /// دوائر القسم وبلا دائرة (قسم)، أو دوائر الشعبة — كل ملف لمالك واحد
    /// (قرار §2.20: المناب في دائرته، والمنيب في دائرته، وبلا دائرة للقسم).
    /// </summary>
    private static IQueryable<Document> InOwnerScope(
        IQueryable<Document> source, int? ownerSectionId, bool fullAccess)
    {
        if (fullAccess)
            return source;
        return ownerSectionId.HasValue
            ? source.Where(d => d.ExecutionCircuitId != null && d.ExecutionCircuit!.SectionId == ownerSectionId.Value)
            : source.Where(d => d.ExecutionCircuitId == null || d.ExecutionCircuit!.SectionId == null);
    }

    /// <summary>
    /// تاريخ محلي (بتوقيت النظام الزمني) من طابع UTC — لحدود الشهر/الفترة والتصنيف الشهري؛
    /// العمود `CreatedAt` يُخزَّن UTC، وحدود الفترة تُحسب بتوقيت دمشق (قرار المالك).
    /// </summary>
    private DateTime LocalDateOf(DateTime utcDateTime)
        => TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utcDateTime), _timeZone).Date;

    /// <summary>توحيد الطابع إلى UTC مع احترام `Kind` (كـ`UtcDateTimeConverter` عند القراءة).</summary>
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    /// <summary>
    /// حدود النافذة المحلية محوَّلة إلى UTC — لمقارنة الأعمدة الزمنية المخزَّنة UTC
    /// (مثل `CreatedAt`) دون إزاحة يوم كامل قرب منتصف الليل.
    /// </summary>
    private (DateTime Start, DateTime End) ToUtcWindow((DateTime Start, DateTime End) window)
        => (TimeZoneInfo.ConvertTimeToUtc(window.Start, _timeZone),
            TimeZoneInfo.ConvertTimeToUtc(window.End, _timeZone));

    /// <summary>
    /// نطاق الفترة: شهري = شهر محدد (افتراضيًا الحالي)، ربعي = ربع محدد (افتراضيًا الحالي)،
    /// عام = سنة محددة (افتراضيًا الحالية). النطاق نصف مفتوح [Start, End).
    /// year/month/quarter تُتحقق من صحتها في المتحكم قبل الوصول إلى هنا.
    /// </summary>
    private (DateTime Start, DateTime End) GetPeriodWindow(StatsPeriod period,
        int? year = null, int? month = null, int? quarter = null)
    {
        var now = ServerClock.Now(_clock, _timeZone);
        var months = period switch
        {
            StatsPeriod.Monthly => 1,
            StatsPeriod.Quarterly => 3,
            _ => 12,
        };
        var startYear = year ?? now.Year;
        var startMonth = period switch
        {
            StatsPeriod.Monthly => month ?? now.Month,
            StatsPeriod.Quarterly when quarter is >= 1 and <= 4 => (quarter!.Value - 1) * 3 + 1,
            StatsPeriod.Quarterly => ((now.Month - 1) / 3) * 3 + 1,
            _ => 1,
        };
        var start = new DateTime(startYear, startMonth, 1);
        return (start, start.AddMonths(months));
    }
}
