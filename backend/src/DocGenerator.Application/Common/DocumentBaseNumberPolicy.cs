using System.Globalization;
using System.Text;
using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Common;

/// <summary>هوية رقم أساس فعّالة: السنة والرقم والصف المصدر.</summary>
public sealed record EffectiveDocumentNumber(int Year, string Number, DocumentBaseNumber Row);

/// <summary>
/// سياسة رقم الأساس التنفيذي النقية (بلا قاعدة/بلا حالة): التطبيع، والسنة،
/// والأهلية، والهوية الفعّالة، وبناء السجل. كل المسارات تقرأ القواعد من هنا
/// حتى لا تتباعد بين شاشة وأخرى.
/// </summary>
public static class DocumentBaseNumberPolicy
{
    /// <summary>أقصى طول لرقم الأساس — مطابق لعمود <c>DocumentBaseNumbers.BaseNumber</c>.</summary>
    public const int MaxNumberLength = 50;

    /// <summary>تطبيع نوع الملف — نفس مطبّع الأسماء العربية (idempotent).</summary>
    public static string NormalizeType(string? fileType) => ArabicNameNormalizer.Normalize(fileType);

    /// <summary>تطبيع الرقم: أرقام عربية/فارسية إلى لاتينية ثم حذف كل المسافات. يرمي عند الفارغ.</summary>
    public static string NormalizeNumber(string? number)
    {
        var normalized = NormalizeNumberOrNull(number);
        if (normalized is null)
            throw new ArgumentException("رقم الأساس مطلوب");
        return normalized;
    }

    /// <summary>تطبيع الرقم مع إعادة <c>null</c> للفارغ (بلا رمي).</summary>
    public static string? NormalizeNumberOrNull(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
            return null;
        var digits = DigitNormalizer.NormalizeDigits(number) ?? string.Empty;
        var builder = new StringBuilder(digits.Length);
        foreach (var ch in digits)
        {
            if (char.IsWhiteSpace(ch))
                continue;
            builder.Append(ch);
        }
        return builder.Length == 0 ? null : builder.ToString();
    }

    /// <summary>تحقق الطول: الفارغ يعني «لا رقم» ويعود null؛ ما عدا ذلك يُطبَّع ويُفحص الطول.</summary>
    public static string? ValidateNumber(string? number, string fieldName = "رقم الأساس")
    {
        var normalized = NormalizeNumberOrNull(number);
        if (normalized is null)
            return null;
        if (normalized.Length > MaxNumberLength)
            throw new ArgumentException($"{fieldName} يتجاوز الطول المسموح ({MaxNumberLength})");
        return normalized;
    }

    /// <summary>سنة رقمية ضمن 1900..السنة الحالية؛ الفارغ يعني null؛ غير الصالح يرمي.</summary>
    public static int? ValidateYear(string? rawYear, int currentYear, string fieldName = "السنة")
    {
        if (string.IsNullOrWhiteSpace(rawYear))
            return null;
        var normalized = DigitNormalizer.NormalizeDigits(rawYear)?.Trim();
        if (normalized is null
            || !int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year))
            throw new ArgumentException($"{fieldName} غير صالح — أدخل سنة مثل 2026");
        if (year < 1900 || year > currentYear)
            throw new ArgumentException($"{fieldName} غير صالح — أدخل سنة بين 1900 و{currentYear}");
        return year;
    }

    /// <summary>أهلية التدوير (V-12) مقيّدة بدائرة الملف الحالية.</summary>
    public static bool IsEligible(Document doc, int currentYear)
    {
        if (doc.IsDraft || doc.NeedsRegistration || doc.IsDeleted || doc.ExecutionCircuitId is null)
            return false;

        var executable = GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide)
            ? doc.ExecutedStatus == ExecutedStatusCatalog.None
            : !ExecutionStatusCatalog.IsExecuted(doc.ExecStatus, doc.ExecSubStatus)
              && doc.ExecStatus != ExecutionStatusCatalog.StateStruckOff;
        if (!executable)
            return false;

        var rows = doc.BaseNumbers;
        var hasCurrent = rows.Any(b =>
            b.VoidedAt is null
            && b.ExecutionCircuitId == doc.ExecutionCircuitId
            && b.Year == currentYear);
        if (hasCurrent)
            return false;

        return rows.Any(b =>
            b.VoidedAt is null
            && b.ExecutionCircuitId == doc.ExecutionCircuitId
            && b.Year < currentYear);
    }

    /// <summary>يحتاج تدويرًا = مؤهل.</summary>
    public static bool NeedsRotation(Document doc, int currentYear) => IsEligible(doc, currentYear);

    /// <summary>
    /// الرقم الفعّال (V-04): أحدث صف غير ملغى في دائرة الملف الحالية بسنة ≤ asOfYear،
    /// ترتيب السنة ثم CreatedAt ثم Id تنازليًا؛ وإلا null (فيعود المستدعي إلى FileNumber).
    /// </summary>
    public static EffectiveDocumentNumber? ResolveEffective(Document? doc, int asOfYear)
    {
        if (doc is null || doc.ExecutionCircuitId is null)
            return null;
        var row = doc.BaseNumbers
            .Where(b => b.VoidedAt is null
                && b.ExecutionCircuitId == doc.ExecutionCircuitId
                && b.Year <= asOfYear)
            .OrderByDescending(b => b.Year)
            .ThenByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .FirstOrDefault();
        return row is null ? null : new EffectiveDocumentNumber(row.Year, row.BaseNumber, row);
    }

    /// <summary>بناء سجل العرض: كل الصفوف (بما فيها الملغاة) مرتّبة بالسنة ثم الإنشاء تنازليًا.</summary>
    public static IReadOnlyList<DTOs.BaseNumberHistoryDto> BuildHistory(
        Document doc, IEnumerable<DocumentBaseNumber> rows, int currentYear)
    {
        var effective = ResolveEffective(doc, currentYear)?.Row.Id;
        return rows
            .OrderByDescending(b => b.Year)
            .ThenByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .Select(b => new DTOs.BaseNumberHistoryDto(
                b.Year,
                b.BaseNumber,
                b.Id,
                b.Source,
                b.ExecutionCircuitId,
                b.CircuitName,
                b.VoidedAt is not null,
                b.VoidReason,
                b.Id == effective))
            .ToList();
    }
}
