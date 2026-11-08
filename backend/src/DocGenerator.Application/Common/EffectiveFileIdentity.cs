using System.Diagnostics;
using System.Globalization;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common;

/// <summary>
/// المحلل المركزي لهوية رقم الملف: يختار «آخر رقم فعّال» من أرقام الأساس
/// في دائرة الملف الحالية (أحدث صف غير ملغى بسنة ≤ سنة الفحص، مرتبًا بالسنة ثم
/// CreatedAt ثم Id تنازليًا)، وإلا رقم الملف الأصلي. صفوف الدوائر الأخرى والملغاة
/// توثيق لا يدخل الهوية اليومية.
/// </summary>
public static class EffectiveFileIdentity
{
    /// <summary>
    /// أحدث سجل رقم أساس فعّال لملف في دائرته الحالية بسنة ≤ سنة الفحص، وإلا null.
    /// سنة الفحص إلزامية؛ يُمرَّر int.MaxValue للانتقاء غير المقيد بحلول سنة السجلات.
    /// </summary>
    public static DocumentBaseNumber? Latest(Document? doc, int asOfYear)
        => Pick(doc?.BaseNumbers, doc?.ExecutionCircuitId, asOfYear);

    /// <summary>
    /// المحلل المشترك على أرقام أساس ملف: أحدث سجل فعّال بسنة ≤ سنة الفحص
    /// في الدائرة الممرَّرة (أو الصفوف بلا دائرة عند null)، وإلا null.
    /// </summary>
    public static DocumentBaseNumber? LatestFrom(
        ICollection<DocumentBaseNumber>? numbers, int asOfYear, int? circuitId = null)
        => Pick(numbers, circuitId, asOfYear);

    /// <summary>الرقم الفعّال لملف: رقم أحدث رقم أساس فعّال ≤ سنة الفحص، وإلا رقم الملف الأصلي.</summary>
    public static string? Number(Document? doc, int asOfYear)
        => Latest(doc, asOfYear)?.BaseNumber ?? doc?.FileNumber;

    /// <summary>سنة الرقم الفعّال لملف: سنة سجل الأساس المختار، وإلا سنة قيد الملف الأصلية.</summary>
    public static string? Year(Document? doc, int asOfYear)
        => Latest(doc, asOfYear)?.Year.ToString(CultureInfo.InvariantCulture) ?? doc?.FileYear;

    /// <summary>
    /// الانتخاب المشترك: أحدث صف غير ملغى في الدائرة المطلوبة وبسنة ≤ سنة الفحص.
    /// يُسجَّل كشف تشخيصي (Debug.Write) عندما تُحُمِّل أرقامٌ دون موافِق قطعي — مؤشر على
    /// Include ناقص أو بيانات مستقبلية فقط — فيبقى الاحتياطي (FileNumber) لا التدهور الصامت.
    /// </summary>
    private static DocumentBaseNumber? Pick(
        ICollection<DocumentBaseNumber>? items, int? circuitId, int asOfYear)
    {
        if (items is null || items.Count == 0)
            return null;

        var match = items
            .Where(b => b.VoidedAt is null
                && (circuitId is null ? b.ExecutionCircuitId is null : b.ExecutionCircuitId == circuitId)
                && b.Year <= asOfYear)
            .OrderByDescending(b => b.Year)
            .ThenByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .FirstOrDefault();
        if (match is not null)
            return match;

        // مؤشر على Include ناقص أو بيانات مستقبلية فقط؛ الاحتياطي يعود FileNumber.
        Debug.WriteLine($"EffectiveFileIdentity: عند ({asOfYear}) أرقام محمّلة دون موافِق قطَعي — رجع الاحتياطي.");
        return null;
    }
}
