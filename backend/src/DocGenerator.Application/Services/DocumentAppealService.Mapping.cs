using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentAppealService : IDocumentAppealService
{
    private string AppealDocumentNumber(Document source) =>
        AppealDocumentNumberText(source, ServerClock.CurrentYear(_clock, _timeZone));

    /// <summary>رقم الملف الفعّال للعرض في نص التنبيه (أو «—» عند غيابه) — مشترك بين
    /// مسار التنبيه داخل الخدمة ومعالج التسطير.</summary>
    internal static string AppealDocumentNumberText(Document source, int currentYear) =>
        EffectiveFileIdentity.Number(source, currentYear) ?? "—";

    /// <summary>يجهّز اسم الدائرة للقالب الذي يضيف «دائرة تنفيذ » قبل الحقل.</summary>
    internal static string AppealCourtBare(string? court)
    {
        var value = Normalize(court);
        if (value is null)
            return string.Empty;

        const string circuitPrefix = "دائرة ";
        if (value.StartsWith(circuitPrefix, StringComparison.Ordinal))
            value = value[circuitPrefix.Length..];

        const string executionPrefix = "تنفيذ ";
        return value.StartsWith(executionPrefix, StringComparison.Ordinal)
            ? value[executionPrefix.Length..]
            : value;
    }

    /// <summary>اسم عرض للملف — مصدره الموحّد `DocumentDisplayName`.</summary>
    internal static string DocumentTitle(Document doc) => DocumentDisplayName.Build(doc);

    /// <summary>عنوان موجز للاستئناف في التذكيرات.</summary>
    private static string AppealTitleOf(DocumentAppeal appeal)
        => $"استئناف قرار رئيس التنفيذ — {AppealDirectionCatalog.ToLabel(appeal.Direction)}";

    private static AppealActionDto ToActionDto(AppealAction action) => new(
        action.Id,
        action.Type,
        action.Text,
        action.ActionDate,
        action.ReminderDuration,
        action.ReminderColor,
        action.CreatedBy?.FullName,
        action.CreatedAt);

    internal static AppealDto ToDto(DocumentAppeal a, int asOfYear)
    {
        var d = a.Document;
        var currentYear = asOfYear;
        var currentIdentity = AppealBaseNumberPolicy.ResolveCurrent(a, currentYear);
        // الأهلية للتدوير: منظور بلا هوية لسنة اليوم — بمن فيه من لا رقم له إطلاقًا.
        // (رقم السنة الحالية = صف تدوير لسنة اليوم أو قيد هذه السنة؛ السنوات المستقبلية ممنوعة.)
        var needsRotation = a.Status == AppealStatusCatalog.Pending
            && (currentIdentity is null || currentIdentity.Year != currentYear);

        // وسم تلف اللقطات: التالف يُعرض موسومًا لا قائمة فارغة نظيفة تُقرأ خطأً كنقص إدخال.
        var (appellants, appellantsCorrupted) = AppealSnapshotSerializer.TryDeserializeParties(a.AppellantsJson);
        var (appellees, appelleesCorrupted) = AppealSnapshotSerializer.TryDeserializeParties(a.AppelleesJson);

        return new AppealDto(
            a.Id,
            a.DocumentId,
            DocumentTitle(d),
            d.FileNumber,
            d.FileType,
            d.FileYear,
            d.Court,
            a.Direction,
            AppealDirectionCatalog.ToLabel(a.Direction),
            a.Status,
            AppealStatusCatalog.ToLabel(a.Status),
            a.AppealTypeLabel,
            appellants,
            appellees,
            a.AppealedDecisionText,
            a.AppealedDecisionSummary,
            FreeDateParser.ToResponse(a.AppealedDecisionDate),
            a.InspectionBookNumber,
            FreeDateParser.ToResponse(a.InspectionBookDate),
            a.GroundsSummary,
            a.NoticeNumber,
            FreeDateParser.ToResponse(a.NoticeDate),
            a.AppellateCourt,
            a.AppealBaseNumber,
            a.AppealYear,
            a.DepositBookNumber,
            FreeDateParser.ToResponse(a.DepositBookDate),
            a.DefenseOpinion,
            FreeDateParser.ToResponse(a.RegistrationDate),
            a.DecisionNumber,
            FreeDateParser.ToResponse(a.DecisionDate),
            a.DecisionRuling,
            a.Outcome,
            a.Outcome is null ? null : AppealOutcomeCatalog.ToLabel(a.Outcome),
            FreeDateParser.ToResponse(a.StruckOffDate),
            a.StruckOffDecisionNumber,
            a.Notes,
            needsRotation,
            currentIdentity?.Number,
            a.AssignedLawyerId,
            a.AssignedLawyer?.FullName,
            a.CreatedAt,
            a.CreatedBy?.FullName,
            a.CreatedById,
            EffectiveFileIdentity.Number(d, asOfYear),
            EffectiveFileIdentity.Year(d, asOfYear),
            PartiesDegraded: appellantsCorrupted || appelleesCorrupted,
            ForwardState: a.ForwardState,
            SectionId: d.ExecutionCircuit?.SectionId,
            ExecutionCircuitId: d.ExecutionCircuitId,
            Version: a.Version,
            CurrentBaseYear: currentIdentity?.Year?.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>خيار طرف داخل لقطات الاستئناف (بناء داخلي قبل التسلسل).</summary>
    private sealed record PartyOption(string Kind, int PartyId, string Name);
}
