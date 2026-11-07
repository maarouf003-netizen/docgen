using System.Globalization;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common;

/// <summary>
/// هوية رقم أساس استئنافي فعّالة: سنة الهوية (أو <c>null</c> لسنة قديمة غير رقمية)،
/// والرقم المطبَّع، وصف التدوير المصدر إن وُجد (وإلا فالمصدر حقول القيد).
/// </summary>
public sealed record AppealBaseIdentity(int? Year, string Number, AppealBaseNumber? Row);

/// <summary>سطر في تاريخ أرقام الأساس: سنة صالحة ومقبولة، والرقم، ووسم هوية السنة الحالية.</summary>
public sealed record AppealBaseHistoryEntry(int Year, string Number, bool IsCurrent);

/// <summary>
/// السياسة الوحيدة لرقم الأساس الاستئنافي: قراءة السنة، وانتخاب الهوية الحالية، وبناء
/// الأرشيف، وفحص وجود هوية لسنة. تمر كل المسارات من هنا (العرض والقيد والتدوير
/// والتصحيح والبوابة) حتى لا تتباعد القواعد بين شاشة وأخرى.
/// </summary>
/// <remarks>
/// القواعد الملزمة:
/// <list type="bullet">
/// <item>رقم أساس واحد فعّال لكل استئناف: أحدث هوية بسنة <c>≤</c> السنة الحالية،
/// ثم الأحدث إنشاءً عند تعادل السنة، وصف التدوير يغلب حقول القيد عند التعادل.</item>
/// <item>السنة المستقبلية (أو غير الموجبة) لا تُحتسب هويةً ولا تُعرض في الأرشيف: إدخالها
/// ممنوع في كل مسارات الكتابة، وبيانات قديمة مخالفة تُهمَل بدل عرضها.</item>
/// <item>السنة غير الرقمية القديمة (<c>AppealYear</c> كان نصًا حرًا) لا تُعطّل العرض:
/// يُعرض الرقم للقراءة بلا سنة، ولا يُعدّ هوية للسنة الحالية (يستوجب تدويرًا).</item>
/// </list>
/// </remarks>
public static class AppealBaseNumberPolicy
{
    /// <summary>أقصى طول لرقم الأساس — مطابق لعمود <c>AppealBaseNumbers.BaseNumber</c> (50).</summary>
    public const int MaxNumberLength = 50;

    /// <summary>قراءة سنة مخزَّنة أو مُدخَلة بلا رفض: تُطبَّع الأرقام العربية ويعود <c>null</c> لغير الرقمي.</summary>
    public static int? TryParseYear(string? value)
    {
        var normalized = DigitNormalizer.NormalizeDigitsOrNull(value);
        return normalized is not null
            && int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)
            ? year
            : null;
    }

    /// <summary>قراءة سنة مُدخَلة صرامةً: يرفض غير الصالح غير الفارغ برسالة عربية واضحة.</summary>
    public static int? ParseYear(string? value, string fieldName = "السنة")
    {
        var normalized = DigitNormalizer.NormalizeDigitsOrNull(value);
        if (normalized is null)
            return null;
        if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year))
            throw new ArgumentException($"{fieldName} غير صالح — أدخل سنة مثل 2026");
        return year;
    }

    /// <summary>قراءة سنة مُدخَلة مع منع الصفر والسالب والسنوات المستقبلية.</summary>
    public static int? ValidateYear(string? value, int currentYear, string fieldName = "لعام")
    {
        var year = ParseYear(value, fieldName);
        if (year is null)
            return null;
        EnsureAllowedYear(year.Value, currentYear, fieldName);
        return year;
    }

    /// <summary>التحقق من سنة رقمية جاهزة (المسار الذي يستقبل السنة كعدد صحيح).</summary>
    public static void EnsureAllowedYear(int year, int currentYear, string fieldName = "السنة")
    {
        if (year <= 0)
            throw new ArgumentException($"{fieldName} غير صالح — أدخل سنة مثل 2026");
        if (year > currentYear)
            throw new ArgumentException($"لا يمكن إدخال رقم أساس لسنة مستقبلية — السنة الحالية {currentYear}");
    }

    /// <summary>هل السنة صالحة للعرض والتصحيح (موجبة ولا تتجاوز السنة الحالية)؟</summary>
    public static bool IsUsableYear(int year, int currentYear) => year > 0 && year <= currentYear;

    /// <summary>
    /// الهوية الحالية: أحدث هوية سنة ≤ السنة الحالية من صفوف التدوير أو حقول القيد،
    /// أو <c>null</c> إن لم يوجد رقم. السنة غير الرقمية القديمة تُعرض بلا سنة (ليست هوية).
    /// </summary>
    public static AppealBaseIdentity? ResolveCurrent(DocumentAppeal appeal, int currentYear)
    {
        var candidates = new List<AppealBaseIdentity>();
        foreach (var row in appeal.BaseNumbers)
        {
            if (!HasNumber(row.BaseNumber) || !IsUsableYear(row.Year, currentYear))
                continue;
            candidates.Add(new AppealBaseIdentity(row.Year, row.BaseNumber.Trim(), row));
        }

        if (!string.IsNullOrWhiteSpace(appeal.AppealBaseNumber))
        {
            var registeredYear = TryParseYear(appeal.AppealYear);
            if (registeredYear is int year && IsUsableYear(year, currentYear))
            {
                candidates.Add(new AppealBaseIdentity(
                    year, appeal.AppealBaseNumber.Trim(), null));
            }
            else if (registeredYear is null)
            {
                // سنة قديمة غير رقمية: رقم مسجّل بلا سنة — أرشيف غير قابل للتصحيح بمعرّف سنة.
                candidates.Add(new AppealBaseIdentity(null, appeal.AppealBaseNumber.Trim(), null));
            }
            // سنة رقمية غير صالحة (مستقبلية أو ≤ 0): تُهمَل ولا تُعرض هويةً أصلًا.
        }

        return candidates
            .OrderByDescending(c => c.Year)
            .ThenByDescending(c => c.Row?.CreatedAt ?? DateTime.MinValue)
            .FirstOrDefault();
    }

    /// <summary>
    /// أرشيف أرقام الأساس: سطر واحد لكل سنة صالحة (الأحدث إنشاءً هو المعتبر)، مع رقم
    /// القيد الأصلي إن لم يكن لسنته صف تدوير. السنوات المستقبلية وغير الموجبة مستبعدة.
    /// </summary>
    public static IReadOnlyList<AppealBaseHistoryEntry> BuildHistory(DocumentAppeal appeal, int currentYear)
    {
        var history = appeal.BaseNumbers
            .Where(b => HasNumber(b.BaseNumber) && IsUsableYear(b.Year, currentYear))
            .GroupBy(b => b.Year)
            .Select(g => g.OrderByDescending(b => b.CreatedAt).First())
            .OrderBy(b => b.Year)
            .Select(b => new AppealBaseHistoryEntry(
                b.Year, b.BaseNumber.Trim(), b.Year == currentYear))
            .ToList();

        if (!string.IsNullOrWhiteSpace(appeal.AppealBaseNumber)
            && TryParseYear(appeal.AppealYear) is int registeredYear
            && IsUsableYear(registeredYear, currentYear)
            && !history.Any(h => h.Year == registeredYear))
        {
            history.Add(new AppealBaseHistoryEntry(
                registeredYear, appeal.AppealBaseNumber.Trim(), registeredYear == currentYear));
            history = history.OrderBy(h => h.Year).ToList();
        }

        return history;
    }

    /// <summary>هل توجد هوية رقم أساس مسجّلة لهذه السنة (صف تدوير أو حقول القيد)؟</summary>
    public static bool HasIdentityForYear(DocumentAppeal appeal, int year)
        => appeal.BaseNumbers.Any(b => b.Year == year && HasNumber(b.BaseNumber))
           || (!string.IsNullOrWhiteSpace(appeal.AppealBaseNumber)
               && TryParseYear(appeal.AppealYear) == year);

    /// <summary>أحدث صف مسجَّل لسنة محددة (الأحدث إنشاءً هو المعتبر) — أو <c>null</c>.</summary>
    public static AppealBaseNumber? LatestRowForYear(DocumentAppeal appeal, int year)
        => appeal.BaseNumbers
            .Where(b => b.Year == year && HasNumber(b.BaseNumber))
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefault();

    private static bool HasNumber(string? value) => !string.IsNullOrWhiteSpace(value);
}
