using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.DTOs;

/// <summary>
/// طرف ضمن استئناف (لقطة وقت الإنشاء): نوعه المرجعي ومعرّفه واسمه المعروض.
/// kind: "applicant-entity" / "execution-applicant" / "executed-public" /
/// "executed-natural" / "executed-heir" / "borrower" / "guarantor" / "heir".
/// </summary>
public record AppealPartyDto(string Kind, int PartyId, string Name);

/// <summary>اختيار المستأنف من الواجهة: يُعاد بناء الاسم من الملف الأساس على الخادم دائمًا.</summary>
public record AppealPartySelectionDto(string Kind, int PartyId);

/// <summary>
/// إنشاء/تعديل استئناف قبل الإسناد. التواريخ نصوص حرة تُفسَّر وتُخزَّن زمنيًا
/// («1/8/2026» = 1 آب 2026)؛ الفارغ يعني null وغير الصالح يُرفض برسالة اسم الحقل.
/// حقول مستأنِفين: القرار وملخصه وتاريخه وكتاب المطالعة وموجبات الاستئناف.
/// حقول مستأنف علينا: القرار وتاريخه وسند التبليغ والمحكمة ورقم الأساس والسنة
/// وكتاب الإيداع ورأي المحامي بأسباب الاستئناف.
/// </summary>
public record UpsertAppealRequest(
    string? Direction,
    List<AppealPartySelectionDto>? Appellants,
    string? AppealTypeLabel,
    string? AppealedDecisionText,
    string? AppealedDecisionSummary,
    string? AppealedDecisionDate,
    string? InspectionBookNumber,
    string? InspectionBookDate,
    string? GroundsSummary,
    string? NoticeNumber,
    string? NoticeDate,
    string? AppellateCourt,
    string? AppealBaseNumber,
    string? AppealYear,
    string? DepositBookNumber,
    string? DepositBookDate,
    string? DefenseOpinion,
    string? Notes);

/// <summary>تحديث حقول القيد للاستئناف من المحامي المتابع (رقم الأساس/المحكمة/النوع/تاريخ الإقرار).</summary>
public record UpdateAppealRegistrationRequest(
    string? AppealTypeLabel,
    string? AppellateCourt,
    string? AppealBaseNumber,
    string? AppealYear,
    string? RegistrationDate);

/// <summary>حسم الاستئناف: رقم قرار الحسم وتاريخه ومنطوقه ونتيجته (للصالح/للضد).</summary>
public record DecideAppealRequest(
    string? DecisionNumber,
    string? DecisionDate,
    string? DecisionRuling,
    string? Outcome);

/// <summary>شطب الاستئناف: تاريخ الشطب ورقم قرار الشطب.</summary>
public record StrikeAppealRequest(
    string? StruckOffDecisionNumber,
    string? StruckOffDate);

/// <summary>إسناد الاستئناف إلى محامٍ للمتابعة من رئيس القسم.</summary>
public record AssignAppealRequest(int AssignedLawyerId);

/// <summary>نقل استئناف مفرد بين محامي الفرع (مستقل تمامًا عن نقل الملفات).</summary>
public record TransferAppealRequest(int TargetLawyerId);

/// <summary>نقل كل استئنافات محامٍ إلى محامٍ آخر ضمن الفرع نفسه.</summary>
public record TransferAllAppealsRequest(int SourceLawyerId, int TargetLawyerId);

/// <summary>إدخال رقم الأساس الاستئنافي لسنة التدوير الحالية (نمط تدوير أرقام الملفات).</summary>
public record AppealBaseNumberEntry(string? BaseNumber);

public record SaveAppealBaseNumbersRequest(List<AppealBaseNumberEntry> Entries);

/// <summary>سجل رقم أساس استئنافي لسنة سابقة أو حالية.</summary>
public record AppealBaseNumberHistoryDto(int Year, string BaseNumber);

/// <summary>إجراء/ملاحظة على الاستئناف مع تذكيره الاختياري (مدة + لون).</summary>
public record AddAppealActionRequest(
    string? Type,
    string Text,
    string? ActionDate,
    string? ReminderDuration,
    string? ReminderColor);

public record UpdateAppealActionRequest(
    string? Type,
    string Text,
    string? ActionDate,
    string? ReminderDuration,
    string? ReminderColor);

public record AppealActionDto(
    int Id,
    string Type,
    string Text,
    string? ActionDate,
    string? ReminderDuration,
    string? ReminderColor,
    string? CreatedByName,
    DateTime CreatedAt);

/// <summary>
/// تذكير إجراء على استئناف يتابعه المحامي، بأسلوب بطاقة التذكيرات في لوحة التحكم.
/// </summary>
public record AppealReminderDto(
    int ActionId,
    int AppealId,
    int DocumentId,
    string AppealTitle,
    string ActionText,
    string? ActionDate,
    string? ReminderDuration,
    string? ReminderColor,
    DateTime DueDate);

/// <summary>
/// استئناف للعرض: كامل حقوله ولقطتا الأطراف وبيانات الملف الأساس اللازمة للأعمدة،
/// وحالة التدوير (رقم أساس السنة الحالية وهل يحتاج تدويرًا). التواريخ نصية yyyy-MM-dd.
/// </summary>
public record AppealDto(
    int Id,
    int DocumentId,
    string? DocumentLabel,
    string? FileNumber,
    string? FileType,
    string? FileYear,
    string? Court,
    string Direction,
    string DirectionLabel,
    string Status,
    string StatusLabel,
    string? AppealTypeLabel,
    List<AppealPartyDto> Appellants,
    List<AppealPartyDto> Appellees,
    string? AppealedDecisionText,
    string? AppealedDecisionSummary,
    string? AppealedDecisionDate,
    string? InspectionBookNumber,
    string? InspectionBookDate,
    string? GroundsSummary,
    string? NoticeNumber,
    string? NoticeDate,
    string? AppellateCourt,
    string? AppealBaseNumber,
    string? AppealYear,
    string? DepositBookNumber,
    string? DepositBookDate,
    string? DefenseOpinion,
    string? RegistrationDate,
    string? DecisionNumber,
    string? DecisionDate,
    string? DecisionRuling,
    string? Outcome,
    string? OutcomeLabel,
    string? StruckOffDate,
    string? StruckOffDecisionNumber,
    string? Notes,
    bool NeedsRotation,
    string? CurrentBaseNumber,
    int? AssignedLawyerId,
    string? AssignedLawyerName,
    DateTime CreatedAt,
    string? CreatedByName,
    int CreatedById);
