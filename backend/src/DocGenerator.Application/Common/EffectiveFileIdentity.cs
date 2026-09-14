using System.Diagnostics;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common;

/// <summary>
/// المحلل المركزي لهوية رقم الملف: يختار «آخر رقم فعّال» من أرقام الأساس
/// (أحدث سجل بسنة ≤ سنة الفحص، مرتبًا بالسنة ثم CreatedAt تنازليًا)، وإلا رقم الملف الأصلي.
/// مبدأ «الأحدث هو المعتبر»: رقم سنة سابقة يحل محل الرقم الأصلي، ولا يُستخدم
/// رقمُ مستقبلي قبل حلول سنته، ولا يُخلط رقمُ سنةٍ مع سنةِ قيدٍ أصلية.
/// </summary>
public static class EffectiveFileIdentity
{
    /// <summary>أحدث سجل رقم أساس لملف بسنة ≤ سنة الفحص، وإلا null.</summary>
    public static DocumentBaseNumber? Latest(Document? doc, int? asOfYear = null)
        => Pick(doc?.BaseNumbers, b => b.Year, b => b.CreatedAt, asOfYear);

    /// <summary>
    /// المحلل المشترك على أرقام أساس ملف: أحدث سجل بسنة ≤ سنة الفحص
    /// مرتبًا بالسنة ثم CreatedAt تنازليًا، وإلا null. لا تُستعمل الأرقام المستقبلية قبل حلول سنتها.
    /// </summary>
    public static DocumentBaseNumber? LatestFrom(ICollection<DocumentBaseNumber>? numbers, int? asOfYear = null)
        => Pick(numbers, b => b.Year, b => b.CreatedAt, asOfYear);

    /// <summary>
    /// المحلل المشترك على أرقام أساس استئناف (نفس قاعدة الرقم الفعّال للملفات).
    /// </summary>
    public static AppealBaseNumber? LatestFrom(ICollection<AppealBaseNumber>? numbers, int? asOfYear = null)
        => Pick(numbers, b => b.Year, b => b.CreatedAt, asOfYear);

    /// <summary>الرقم الفعّال لملف: رقم أحدث رقم أساس ≤ سنة الفحص، وإلا رقم الملف الأصلي.</summary>
    public static string? Number(Document? doc, int? asOfYear = null)
        => Latest(doc, asOfYear)?.BaseNumber ?? doc?.FileNumber;

    /// <summary>سنة الرقم الفعّال لملف: سنة سجل الأساس المختار، وإلا سنة قيد الملف الأصلية.</summary>
    public static string? Year(Document? doc, int? asOfYear = null)
        => Latest(doc, asOfYear)?.Year.ToString() ?? doc?.FileYear;

    /// <summary>
    /// الانتخاب المشترك: أحدث سجل بسنة ≤ سنة الفحص مرتبًا بالسنة ثم CreatedAt تنازليًا.
    /// يُسجَّل كشف تشخيصي (Debug.Write) عندما تُحُمِّل أرقامٌ دون موافِق قطعي — مؤشر على Include
    /// ناقص أو بيانات مستقبلية فقط — فيبقى الاحتياطي (FileNumber) لا التدهور الصامت المطلق.
    /// </summary>
    private static T? Pick<T>(
        ICollection<T>? items,
        Func<T, int> getYear,
        Func<T, DateTime> getCreated,
        int? asOfYear)
    {
        if (items is null || items.Count == 0)
            return default;

        var asOf = asOfYear ?? DateTime.Today.Year;
        var match = items
            .Where(b => getYear(b) <= asOf)
            .OrderByDescending(getYear)
            .ThenByDescending(getCreated)
            .FirstOrDefault();
        if (match is not null)
            return match;

        // مؤشر على Include ناقص أو بيانات مستقبلية فقط؛ الاحتياطي يعود FileNumber.
        Debug.WriteLine($"EffectiveFileIdentity: عند ({asOf}) أرقام محمّلة دون موافِق قطَعي — رجع الاحتياطي.");
        return default;
    }
}