namespace DocGenerator.Application.DTOs;

/// <summary>
/// فرع للإدارة والعرض: مع الهاتف وحالة التفعيل وأعداد الاستخدام
/// (المستخدمون/المستندات المرتبطة) لاتخاذ قرار الحذف/التعطيل بصورة مطلعة.
/// </summary>
public record BranchDto(
    int Id,
    string Name,
    string Code,
    string? Address,
    string? Phone,
    bool IsActive,
    int UserCount,
    int DocumentCount);

public record DashboardStatsDto(
    int TotalDocuments,
    int TotalDrafts,
    int TotalExecuted,
    int TotalDeferred,
    int TotalActive,
    int TotalBorrowers,
    decimal TotalAmount,
    decimal TotalCollectedAmount);

/// <summary>
/// تذكير إجراء/ملاحظة مرتبط بمستند، مع تاريخ الاستحقاق المحسوب
/// (تاريخ الإجراء + مدة التذكير، وإن غاب التاريخ فتاريخ الإنشاء + المدة).
/// </summary>
public record ReminderDto(
    int ActionId,
    int DocumentId,
    string? DocumentType,
    string? BorrowerName,
    string? BorrowerFather,
    string? BorrowerFamily,
    string ActionText,
    string? ActionDate,
    string? ReminderDuration,
    string? ReminderColor,
    DateTime DueDate);

public record MonthlyStatDto(int Year, int Month, int Count);

/// <summary>
/// نطاق الفترة الزمنية في إحصاءات المدير:
/// عام = السنة الحالية، ربعي = الربع الحالي، شهري = الشهر الحالي.
/// </summary>
public enum StatsPeriod
{
    Yearly = 1,
    Quarterly = 2,
    Monthly = 3,
}

/// <summary>
/// بطاقات إحصاءات المدير على الملفات المقيَّدة في نطاق الفترة.
/// إجمالي الملفات = متداول + تحت رفع + تريث (دون المنفذ).
/// مبالغ البطاقات بعملتين: مجموع AmountNumeric (ل.س) وAmount2Numeric (دولار) لملفات كل حالة:
/// TotalAmount = ActiveAmount + DraftsAmount + DeferredAmount، وبالمثل TotalAmount2.
/// بطاقتا وضع «الجهة العامة منفذ عليها» معزولتان عن نظام «طالبة التنفيذ»:
/// «متداول للضد» = ملفات متداول فقط (المنفذ/المشطوب مستبعدان) ومبلغها المطلوب دفعه من الجهة العامة،
/// «منفذ للضد» = ملفات منفذ فقط ومبلغها الذي دفعته الجهة العامة، وفترة البطاقتين من تاريخ ورود الملف.
/// حقول الفترة توضح النطاق المعروض فعليًا على الخادم:
/// شهريًا: PeriodMonth مع PeriodYear، ربعيًا: PeriodQuarter مع PeriodYear، سنويًا: PeriodYear فقط.
/// </summary>
public record ManagerStatsDto(
    int TotalFiles,
    int Active,
    int Drafts,
    int Deferred,
    decimal TotalAmount,
    decimal ActiveAmount,
    decimal DraftsAmount,
    decimal DeferredAmount,
    decimal TotalAmount2,
    decimal ActiveAmount2,
    decimal DraftsAmount2,
    decimal DeferredAmount2,
    int SettledCount,
    decimal SettledCollected,
    int ForcibleCount,
    decimal ForcibleCollected,
    int TradingAgainstCount,
    decimal TradingAgainstAmount,
    int ExecutedAgainstCount,
    decimal ExecutedAgainstAmount,
    int PeriodYear,
    int? PeriodQuarter,
    int? PeriodMonth);

/// <summary>نقطة زمنية داخل نطاق الفترة (شهر) لجدول محامي الفرع.</summary>
public record ManagerPeriodPointDto(int Year, int Month, int Count);

/// <summary>
/// إحصاء محامي الفرع في نطاق الفترة،
/// مع توزيعه الشهري داخل النطاق (شهر واحد للفترة الشهرية، وثلاثة للربعية، واثنا عشر للسنوية).
/// </summary>
public record ManagerLawyerStatDto(
    int LawyerId,
    string LawyerName,
    int TotalCount,
    List<ManagerPeriodPointDto> Points);

public record BranchSummaryDto(
    int BranchId,
    string BranchName,
    int TotalDocuments,
    int TotalDrafts,
    decimal TotalAmount);

public record UserActivityDto(
    string Username,
    string FullName,
    int DocumentCount,
    int ViewCount);
