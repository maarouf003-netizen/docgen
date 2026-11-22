using System.Globalization;
using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentDelegationService : IDocumentDelegationService
{
    /// <summary>التسمية المعروضة للملف المناب في إشعار الإتمام: «ملف الرقم/السنة» عبر قاعدة
    /// DisplayFileNumber نفسها (رقم أساس سنة التدوير الحالية إن وُجد، وإلا رقم ملفه الأصلي)،
    /// وإلا رقمه الداخلي.</summary>
    private static string TargetFileLabel(Document? target, int currentYear)
    {
        if (target is null) return "الملف المناب";
        var number = TargetFileNumber(target, currentYear);
        var year = TargetFileYear(target, currentYear);
        if (!string.IsNullOrWhiteSpace(number))
            return string.IsNullOrWhiteSpace(year) ? $"ملف {number}" : $"ملف {number}/{year}";
        return $"ملف {target.Id}";
    }

    private static DelegationDto ToDto(
        DocumentDelegation d, Document source, int currentYear) => new(
        d.Id,
        d.SourceDocumentId,
        SourceLabel(source),
        SourceFileNumber(source, currentYear),
        SourceFileYear(source, currentYear),
        d.TargetDocument?.Id,
        TargetCourt(d.TargetDocument, d.DelegatedCourt),
        d.IsExternal,
        d.ExternalBranchId,
        d.ExternalBranch?.Name,
        FreeDateParser.ToResponse(d.DelegationDate),
        d.DelegationText,
        d.DepositBookNumber,
        FreeDateParser.ToResponse(d.DepositBookDate),
        d.AssignedLawyerId,
        d.AssignedLawyer?.FullName,
        FreeDateParser.ToResponse(d.ReturnDate),
        d.Status,
        d.CreatedAt,
        d.CreatedBy?.FullName,
        d.Assets.Select(a => new DelegationAssetDto(a.Id, a.AssetKind, a.AssetLabel, a.SalePrice, a.SnapshotAdjusted)).ToList(),
        d.CreatedById,
        d.SaleCoversFullDebt,
        TargetFileNumber(d.TargetDocument, currentYear),
        TargetFileYear(d.TargetDocument, currentYear),
        SourceFileType(source),
        d.TargetDocument?.ExecStatus,
        Normalize(source.Court),
        DelegationActivityPolicy.IsAssetBlocking(d),
        d.TargetDocument is not null && DelegationActivityPolicy.IsTargetTerminal(d.TargetDocument),
        d.DelegatedCircuitId,
        d.TargetDocument?.BranchId,
        d.TargetDocument?.Branch?.Name,
        d.TargetDocument?.Lawyer,
        RejectReason: d.RejectReason,
        RedirectedToSectionId: d.RedirectedToSectionId,
        RedirectedToSectionName: d.RedirectedToSection?.Name,
        Version: d.Version,
        LocalAddition: ToLocalAddition(d),
        ForcedExecutionDate: source.ForcedExecutionDate);

    /// <summary>ملخص الإضافات المحلية من سجل الإنابة (عدّاد + ملخص + وقت) — لا من التنبيهات.</summary>
    private static DelegationLocalAdditionDto? ToLocalAddition(DocumentDelegation d) =>
        d.LocalAdditionCount <= 0
            ? null
            : new DelegationLocalAdditionDto(
                d.LocalAdditionCount,
                d.LocalAdditionLastMessage ?? string.Empty,
                d.LocalAdditionLastAt);

    private static string SourceLabel(Document source)
    {
        var parts = new[] { source.BorrowerName, source.BorrowerFather, source.BorrowerFamily }
            .Where(v => !string.IsNullOrWhiteSpace(v));
        var name = string.Join(' ', parts);
        return string.IsNullOrWhiteSpace(name) ? source.DocumentType ?? $"ملف {source.Id}" : name;
    }

    /// <summary>رقم المنيب المعروض وفق المحلل المركزي: آخر رقم أساس ≤ سنة اليوم، وإلا رقم ملفه الأصلي.</summary>
    private static string? SourceFileNumber(Document source, int currentYear) => Normalize(EffectiveFileIdentity.Number(source, currentYear));

    /// <summary>سنة الرقم الفعّال المعروض للمنيب وفق المحلل المركزي.</summary>
    private static string? SourceFileYear(Document source, int currentYear) => Normalize(EffectiveFileIdentity.Year(source, currentYear));

    /// <summary>نوع الملف المنيب المعروض في «معلومات الملف المنيب» (الفارغ يُخفى في الواجهة).</summary>
    private static string? SourceFileType(Document source) => Normalize(source.FileType);

    /// <summary>رقم المناب المعروض في «تشعبات الملف»: آخر رقم أساس ≤ سنته عبر المحلل المركزي، وإلا رقم ملفه الأصلي.</summary>
    private static string? TargetFileNumber(Document? target, int currentYear) =>
        target is null ? null : Normalize(EffectiveFileIdentity.Number(target, currentYear));

    /// <summary>سنة الرقم الفعّال المعروض للمناب وفق المحلل المركزي.</summary>
    private static string? TargetFileYear(Document? target, int currentYear) =>
        target is null ? null : Normalize(EffectiveFileIdentity.Year(target, currentYear));

    /// <summary>الدائرة المنابة المعروضة: دائرة المناب الحيّة عند وجوده (حقيقته المسجَّلة
    /// والقابلة للتعديل)، وإلا نيّة التسطير المحفوظة على السجل (قبل الاعتماد).</summary>
    private static string? TargetCourt(Document? target, string? recorded) =>
        Normalize(target?.Court) ?? Normalize(recorded);

    /// <summary>
    /// اسم دائرة للعرض مع بادئة «دائرة تنفيذ» مرة واحدة فقط، ويمنع «دائرة دائرة تنفيذ»
    /// أو «دائرة تنفيذ تنفيذ …» حين يكون المخزَّن يحمل البادئة أصلًا.
    /// </summary>
    private static string CourtWithExecutionPrefix(string? court)
    {
        var value = Normalize(court);
        if (value is null)
            return "الدائرة المنابة";

        static bool HasPrefix(string text, string prefix) =>
            text == prefix || text.StartsWith($"{prefix} ", StringComparison.Ordinal);

        return HasPrefix(value, "دائرة") || HasPrefix(value, "محكمة") || HasPrefix(value, "تنفيذ")
            ? value
            : $"دائرة تنفيذ {value}";
    }

    /// <summary>يجهّز اسم الدائرة للقوالب التي تضيف «دائرة » قبل الحقل، فيمنع «دائرة دائرة تنفيذ».</summary>
    private static string AlertCourtAfterDaira(string? court)
    {
        var value = Normalize(court);
        if (value is null)
            return string.Empty;

        const string prefix = "دائرة ";
        return value.StartsWith(prefix, StringComparison.Ordinal)
            ? value[prefix.Length..]
            : value;
    }

    /// <summary>يجهّز اسم الدائرة للقوالب التي تضيف «دائرة تنفيذ » قبل الحقل.</summary>
    private static string AlertCourtBare(string? court)
    {
        var value = AlertCourtAfterDaira(court);
        const string executionPrefix = "تنفيذ ";
        return value.StartsWith(executionPrefix, StringComparison.Ordinal)
            ? value[executionPrefix.Length..]
            : value;
    }
}
